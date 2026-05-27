using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Abstractions.Storage;

public interface IBlobStorageService
{
    Task<string> UploadAsync(string containerName, string blobName, byte[] data, CancellationToken cancellationToken = default);
    Task<byte[]> DownloadAsync(string containerName, string blobName, CancellationToken cancellationToken = default);
    Task DeleteAsync(string containerName, string blobName, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string containerName, string blobName, CancellationToken cancellationToken = default);
    string GenerateSasUrl(string containerName, string blobName);
    Uri GenerateSasUri(Azure.Storage.Blobs.BlobClient blobClient);
    Task<string> UploadAsync(Stream fileStream, string fileName, string contentType, CancellationToken ct);
    Task<Uri> GenerateSasUrlAsync(string blobName, TimeSpan ttl, CancellationToken ct);
}
 