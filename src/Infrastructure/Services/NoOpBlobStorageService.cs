using Application.Abstractions.Storage;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

/// <summary>
/// PHASE-3: No-op / in-memory blob storage. Returns synthetic blob names and SAS URLs
/// so the document upload flow works end-to-end without an Azure dependency.
///
/// Mirrors the pattern of <see cref="NoOpFinancialLedgerService"/>: logs every call,
/// performs no real I/O, and is the default registration for local/dev. A real
/// implementation lives in <see cref="Infrastructure.Storage.AzureBlobStorageService"/>
/// and gets wired up when <c>AzureBlob:ConnectionString</c> is configured.
/// </summary>
internal sealed class NoOpBlobStorageService : IBlobStorageService
{
    private readonly ILogger<NoOpBlobStorageService> _logger;

    public NoOpBlobStorageService(ILogger<NoOpBlobStorageService> logger)
    {
        _logger = logger;
    }

    public async Task<string> UploadAsync(
        Stream fileStream,
        string fileName,
        string contentType,
        CancellationToken ct)
    {
        // Drain the stream so the caller doesn't think we silently dropped it.
        long bytesRead = 0;
        var buffer = new byte[8192];
        int read;
        while ((read = await fileStream.ReadAsync(buffer.AsMemory(0, buffer.Length), ct)) > 0)
        {
            bytesRead += read;
        }

        var blobName = $"stub/{Guid.NewGuid():N}-{fileName}";

        _logger.LogInformation(
            "[NoOp Blob] UploadAsync called. FileName={FileName} ContentType={ContentType} BytesRead={BytesRead} GeneratedBlobName={BlobName}",
            fileName, contentType, bytesRead, blobName);

        return blobName;
    }

    public Task<Uri> GenerateSasUrlAsync(string blobName, TimeSpan ttl, CancellationToken ct)
    {
        var uri = new Uri($"https://stub.local/blob/{Uri.EscapeDataString(blobName)}?sas=stub&ttl={ttl.TotalMinutes:F0}min");

        _logger.LogInformation(
            "[NoOp Blob] GenerateSasUrlAsync called. BlobName={BlobName} TTLMinutes={TtlMinutes} StubUri={Uri}",
            blobName, ttl.TotalMinutes, uri);

        return Task.FromResult(uri);
    }

    // The legacy IBlobStorageService surface includes container-scoped helpers used by
    // older code paths. They're not needed for Phase 3 (which uses the stream-based
    // UploadAsync overload), so we return no-op / synthetic results and log a warning.

    public Task<string> UploadAsync(string containerName, string blobName, byte[] data, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("[NoOp Blob] Legacy UploadAsync(container, blob, byte[]) called — returning synthetic URL.");
        return Task.FromResult($"https://stub.local/{containerName}/{blobName}");
    }

    public Task<byte[]> DownloadAsync(string containerName, string blobName, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("[NoOp Blob] Legacy DownloadAsync called — returning empty payload.");
        return Task.FromResult(Array.Empty<byte>());
    }

    public Task DeleteAsync(string containerName, string blobName, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[NoOp Blob] Legacy DeleteAsync called. Container={Container} Blob={Blob}", containerName, blobName);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string containerName, string blobName, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(false);
    }

    public string GenerateSasUrl(string containerName, string blobName)
        => $"https://stub.local/{containerName}/{blobName}?sas=stub";

    public Uri GenerateSasUri(BlobClient blobClient)
        => new($"https://stub.local/blob/{Uri.EscapeDataString(blobClient.Name)}?sas=stub");
}
