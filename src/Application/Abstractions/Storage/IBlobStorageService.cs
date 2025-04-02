using System;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Abstractions.Storage;

public interface IBlobStorageService
{
    Task<string> UploadAsync(string containerName, string blobName, byte[] data, CancellationToken cancellationToken = default);
    Task<byte[]> DownloadAsync(string containerName, string blobName, CancellationToken cancellationToken = default);
    Task DeleteAsync(string containerName, string blobName, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string containerName, string blobName, CancellationToken cancellationToken = default);
    Uri GenerateSasUri(Azure.Storage.Blobs.BlobClient blobClient);
} 