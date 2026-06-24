using Application.Abstractions.Storage;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Infrastructure.Storage;

public class LocalFileStorageService : IBlobStorageService
{
    private readonly string _basePath;
    private readonly string _baseUrl;

    public LocalFileStorageService(IConfiguration configuration)
    {
        _basePath = configuration["LocalStorage:BasePath"] ?? Path.Combine(Directory.GetCurrentDirectory(), "uploads");
        _baseUrl = configuration["LocalStorage:BaseUrl"] ?? "/uploads";

        // Asegurarse de que el directorio base exista
        if (!Directory.Exists(_basePath))
        {
            Directory.CreateDirectory(_basePath);
        }
    }

    public async Task<string> UploadAsync(string containerName, string blobName, byte[] data, CancellationToken cancellationToken = default)
    {
        // Crear directorio para el contenedor si no existe
        string containerPath = Path.Combine(_basePath, containerName);
        if (!Directory.Exists(containerPath))
        {
            Directory.CreateDirectory(containerPath);
        }

        // Guardar el archivo
        string filePath = Path.Combine(containerPath, blobName);
        await File.WriteAllBytesAsync(filePath, data, cancellationToken);

        // Retornar URL relativa
        return $"{_baseUrl}/{containerName}/{blobName}";
    }

    public async Task<byte[]> DownloadAsync(string containerName, string blobName, CancellationToken cancellationToken = default)
    {
        string filePath = Path.Combine(_basePath, containerName, blobName);
        
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"El archivo {blobName} no se encontró en el contenedor {containerName}");
        }

        return await File.ReadAllBytesAsync(filePath, cancellationToken);
    }

    public Task DeleteAsync(string containerName, string blobName, CancellationToken cancellationToken = default)
    {
        string filePath = Path.Combine(_basePath, containerName, blobName);
        
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string containerName, string blobName, CancellationToken cancellationToken = default)
    {
        string filePath = Path.Combine(_basePath, containerName, blobName);
        bool exists = File.Exists(filePath);
        
        return Task.FromResult(exists);
    }

    public string GenerateSasUrl(string containerName, string blobName)
    {
        return $"{_baseUrl}/{containerName}/{blobName}";
    }

    public Uri GenerateSasUri(Azure.Storage.Blobs.BlobClient blobClient)
    {
        return blobClient.Uri;
    }

    public async Task<string> UploadAsync(Stream fileStream, string fileName, string contentType, CancellationToken ct)
    {
        var filePath = Path.Combine(_basePath, Guid.NewGuid().ToString(), fileName);
        var dir = Path.GetDirectoryName(filePath)!;
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        using var fs = File.Create(filePath);
        await fileStream.CopyToAsync(fs, ct);
        return filePath;
    }

    public Task<Uri> GenerateSasUrlAsync(string blobName, TimeSpan ttl, CancellationToken ct)
    {
        return Task.FromResult(new Uri($"{_baseUrl}/{blobName}"));
    }
} 