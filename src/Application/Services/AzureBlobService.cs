using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Configuration;

namespace Application.Services;
public class AzureBlobService
{
    private readonly BlobContainerClient _containerClient;

    public AzureBlobService(IConfiguration configuration)
    {
        string connectionString = configuration["AzureStorage:ConnectionString"];
        string containerName = configuration["AzureStorage:ContainerName"];
        _containerClient = new BlobContainerClient(connectionString, containerName);
        _containerClient.CreateIfNotExists();
    }

    public async Task<string> UploadImageAsync(byte[] imageBytes, string fileName)
    {
        BlobClient blobClient = _containerClient.GetBlobClient(fileName);
        using var stream = new MemoryStream(imageBytes);
        await blobClient.UploadAsync(stream, overwrite: true);

        return blobClient.Uri.ToString(); // Retorna la URL pública de la imagen
    }

    public async Task DeleteImageAsync(string fileName)
    {
        BlobClient blobClient = _containerClient.GetBlobClient(fileName);
        await blobClient.DeleteIfExistsAsync();
    }
}
