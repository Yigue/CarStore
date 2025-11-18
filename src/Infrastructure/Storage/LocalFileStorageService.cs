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

    public Task<string> GenerateAccessUrlAsync(string containerName, string blobName, CancellationToken cancellationToken = default)
    {
        string filePath = Path.Combine(_basePath, containerName, blobName);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"El archivo {blobName} no se encontró en el contenedor {containerName}");
        }

        string sanitizedBaseUrl = _baseUrl.TrimEnd('/');
        return Task.FromResult($"{sanitizedBaseUrl}/{containerName}/{blobName}");
    }
}