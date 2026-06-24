using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Abstractions.Storage;

/// <summary>
/// LEGACY storage abstraction. Used ONLY by Documents (and legacy car-image upload).
/// New code MUST use <see cref="IStorageService"/> instead (backed by MinIO, Clean-Arch compliant).
/// <remarks>
/// This interface leaks <c>Azure.Storage.Blobs.BlobClient</c>, which is a Clean Architecture
/// violation tracked as technical debt. It coexists with <see cref="IStorageService"/> per ADR-2.
/// Consolidation is deferred to the future SDD <c>Storage-Consolidation</c>.
/// </remarks>
/// </summary>
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
 