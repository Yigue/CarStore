using Application.Abstractions.Storage;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Infrastructure.Storage;

public class AzureBlobStorageService : IBlobStorageService
{
    private readonly BlobServiceClient blobServiceClient;
    private readonly ILogger<AzureBlobStorageService> logger;
    private readonly string accountName;
    private readonly string accountKey;

    public AzureBlobStorageService(IConfiguration configuration, ILogger<AzureBlobStorageService> logger)
    {
        this.logger = logger;

        string connectionString = configuration["AzureBlobStorage:ConnectionString"]
            ?? throw new ArgumentNullException(nameof(configuration), "La cadena de conexión de Azure Blob Storage no está configurada");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("La cadena de conexión de Azure Blob Storage está vacía");
        }

        try
        {
            blobServiceClient = new BlobServiceClient(connectionString);

            Dictionary<string, string> connectionStringParts = connectionString
                .Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(part => part.Split('=', 2))
                .Where(parts => parts.Length == 2)
                .ToDictionary(parts => parts[0].Trim(), parts => parts[1].Trim(), StringComparer.OrdinalIgnoreCase);

            if (!connectionStringParts.TryGetValue("AccountName", out string? parsedAccountName))
            {
                throw new ArgumentException("No se encontró AccountName en la cadena de conexión");
            }

            if (!connectionStringParts.TryGetValue("AccountKey", out string? parsedAccountKey))
            {
                throw new ArgumentException("No se encontró AccountKey en la cadena de conexión");
            }

            accountName = parsedAccountName;
            accountKey = parsedAccountKey;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error al inicializar Azure Blob Storage: {ex.Message}", ex);
        }
    }

    public async Task<string> UploadAsync(string containerName, string blobName, byte[] data, CancellationToken cancellationToken = default)
    {
        BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(containerName);
        await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        BlobClient blobClient = containerClient.GetBlobClient(blobName);

        using var stream = new MemoryStream(data);
        await blobClient.UploadAsync(stream, new BlobUploadOptions(), cancellationToken);

        return CreateSasUri(blobClient).ToString();
    }

    public async Task<byte[]> DownloadAsync(string containerName, string blobName, CancellationToken cancellationToken = default)
    {
        BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(containerName);
        BlobClient blobClient = containerClient.GetBlobClient(blobName);

        using var memoryStream = new MemoryStream();
        await blobClient.DownloadToAsync(memoryStream, cancellationToken);
        return memoryStream.ToArray();
    }

    public async Task DeleteAsync(string containerName, string blobName, CancellationToken cancellationToken = default)
    {
        BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(containerName);
        BlobClient blobClient = containerClient.GetBlobClient(blobName);

        await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
    }

    public async Task<bool> ExistsAsync(string containerName, string blobName, CancellationToken cancellationToken = default)
    {
        BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(containerName);
        BlobClient blobClient = containerClient.GetBlobClient(blobName);

        return await blobClient.ExistsAsync(cancellationToken);
    }

    public Task<string> GenerateAccessUrlAsync(string containerName, string blobName, CancellationToken cancellationToken = default)
    {
        BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(containerName);
        BlobClient blobClient = containerClient.GetBlobClient(blobName);

        return Task.FromResult(CreateSasUri(blobClient).ToString());
    }

    private Uri CreateSasUri(BlobClient blobClient)
    {
        try
        {
            var sasBuilder = new BlobSasBuilder
            {
                BlobContainerName = blobClient.BlobContainerName,
                BlobName = blobClient.Name,
                Resource = "b",
                ExpiresOn = DateTimeOffset.UtcNow.AddYears(1),
                StartsOn = DateTimeOffset.UtcNow.AddMinutes(-5)
            };

            sasBuilder.SetPermissions(BlobSasPermissions.Read);

            if (string.IsNullOrEmpty(accountName) || string.IsNullOrEmpty(accountKey))
            {
                throw new InvalidOperationException("Credenciales de Azure inválidas");
            }

            var storageSharedKeyCredential = new StorageSharedKeyCredential(accountName, accountKey);
            var sasToken = sasBuilder.ToSasQueryParameters(storageSharedKeyCredential).ToString();
            var uriBuilder = new UriBuilder(blobClient.Uri) { Query = sasToken };

            return uriBuilder.Uri;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error al generar URL con SAS para el blob {BlobUri}", blobClient.Uri);
            throw new InvalidOperationException($"Error al generar URL con SAS: {ex.Message}", ex);
        }
    }
}
