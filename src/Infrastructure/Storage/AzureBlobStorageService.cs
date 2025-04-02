using Application.Abstractions.Storage;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Infrastructure.Storage;

public class AzureBlobStorageService : IBlobStorageService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly string _accountName;
    private readonly string _accountKey;

    public AzureBlobStorageService(IConfiguration configuration)
    {
        string connectionString = configuration["AzureBlobStorage:ConnectionString"] 
            ?? throw new ArgumentNullException(nameof(configuration), "La cadena de conexión de Azure Blob Storage no está configurada");
            
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("La cadena de conexión de Azure Blob Storage está vacía");
        }

        try
        {
            _blobServiceClient = new BlobServiceClient(connectionString);
            
            // Extraer el nombre de la cuenta y la clave de la cadena de conexión
            var connectionStringParts = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries)
                .ToDictionary(
                    part => part.Split('=', 2)[0].Trim(),
                    part => part.Split('=', 2)[1].Trim(),
                    StringComparer.OrdinalIgnoreCase
                );
            
            if (!connectionStringParts.TryGetValue("AccountName", out string accountName))
            {
                throw new ArgumentException("No se encontró AccountName en la cadena de conexión");
            }
            
            if (!connectionStringParts.TryGetValue("AccountKey", out string accountKey))
            {
                throw new ArgumentException("No se encontró AccountKey en la cadena de conexión");
            }
            
            _accountName = accountName;
            _accountKey = accountKey;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error al inicializar Azure Blob Storage: {ex.Message}", ex);
        }
    }

    public async Task<string> UploadAsync(string containerName, string blobName, byte[] data, CancellationToken cancellationToken = default)
    {
        BlobContainerClient containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        BlobClient blobClient = containerClient.GetBlobClient(blobName);

        using var stream = new MemoryStream(data);
        await blobClient.UploadAsync(stream, new BlobUploadOptions(), cancellationToken);

        // Generar URL con token SAS
        return GenerateSasUri(blobClient).ToString();
    }

    public async Task<byte[]> DownloadAsync(string containerName, string blobName, CancellationToken cancellationToken = default)
    {
        BlobContainerClient containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        BlobClient blobClient = containerClient.GetBlobClient(blobName);

        using var memoryStream = new MemoryStream();
        await blobClient.DownloadToAsync(memoryStream, cancellationToken);
        return memoryStream.ToArray();
    }

    public async Task DeleteAsync(string containerName, string blobName, CancellationToken cancellationToken = default)
    {
        BlobContainerClient containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        BlobClient blobClient = containerClient.GetBlobClient(blobName);

        await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
    }

    public async Task<bool> ExistsAsync(string containerName, string blobName, CancellationToken cancellationToken = default)
    {
        BlobContainerClient containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        BlobClient blobClient = containerClient.GetBlobClient(blobName);

        return await blobClient.ExistsAsync(cancellationToken);
    }

    public Uri GenerateSasUri(BlobClient blobClient)
    {
        try
        {
            Console.WriteLine($"Generando SAS para blob: {blobClient.Uri}, Container: {blobClient.BlobContainerName}, Blob: {blobClient.Name}");
            Console.WriteLine($"AccountName: {_accountName}, AccountKey length: {_accountKey?.Length ?? 0}");
            
            // Crear un token SAS que expire en 1 año
            var sasBuilder = new BlobSasBuilder
            {
                BlobContainerName = blobClient.BlobContainerName,
                BlobName = blobClient.Name,
                Resource = "b",
                ExpiresOn = DateTimeOffset.UtcNow.AddYears(1),
                StartsOn = DateTimeOffset.UtcNow.AddMinutes(-5) // Permitir un pequeño margen de tiempo
            };

            // Establecer permisos de lectura
            sasBuilder.SetPermissions(BlobSasPermissions.Read);
            
            if (string.IsNullOrEmpty(_accountName) || string.IsNullOrEmpty(_accountKey))
            {
                throw new InvalidOperationException($"Credenciales de Azure inválidas. AccountName: {_accountName}, AccountKey: {(_accountKey != null ? "presente" : "ausente")}");
            }

            // Crear las credenciales de almacenamiento de forma más directa
            var storageSharedKeyCredential = new StorageSharedKeyCredential(_accountName, _accountKey);

            // Generar el token SAS con opciones específicas
            var sasToken = sasBuilder.ToSasQueryParameters(storageSharedKeyCredential).ToString();

            // Construir la URI con el token SAS de forma más segura
            var uriBuilder = new UriBuilder(blobClient.Uri) { Query = sasToken };
            Console.WriteLine($"SAS generado correctamente: {uriBuilder.Uri}");
            return uriBuilder.Uri;
        }
        catch (Exception ex)
        {
            // Registrar el error específico para diagnóstico
            Console.WriteLine($"Error detallado al generar SAS en funcion: {ex}");
            Console.WriteLine($"StackTrace: {ex.StackTrace}");
            
            if (ex.InnerException != null)
            {
                Console.WriteLine($"InnerException: {ex.InnerException.Message}");
                Console.WriteLine($"InnerException StackTrace: {ex.InnerException.StackTrace}");
            }
            
            throw new InvalidOperationException($"Error al generar URL con SAS en funcion: {ex.Message}", ex);
        }
    }
}