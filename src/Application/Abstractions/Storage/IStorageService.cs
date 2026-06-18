namespace Application.Abstractions.Storage;

/// <summary>
/// Modern, Clean-Architecture-compliant storage abstraction backed by MinIO.
/// Serves the <c>Cars</c> aggregate (vehicle images).
/// <para>
/// Coexists intentionally with <see cref="IBlobStorageService"/> (used only by Documents).
/// This is documented technical debt: consolidation is deferred to the future SDD
/// <c>Storage-Consolidation</c>. See ADR-2 of the Inventario-Overhaul design.
/// </para>
/// <remarks>
/// New code that persists files MUST use this interface, not <see cref="IBlobStorageService"/>.
/// Signatures MUST NOT leak any infrastructure type (no <c>IMinioClient</c>, no <c>BlobClient</c>).
/// </remarks>
/// </summary>
public interface IStorageService
{
    /// <summary>
    /// Uploads a file to object storage under the given <paramref name="objectKey"/>.
    /// </summary>
    /// <param name="stream">The file content. The caller owns disposal.</param>
    /// <param name="objectKey">
    /// Full object key, e.g. <c>cars/{dealerId}/{carId}/{imageId}.jpg</c> (see ADR-6).
    /// </param>
    /// <param name="contentType">MIME content type, e.g. <c>image/jpeg</c>.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The stored object key.</returns>
    Task<string> UploadFileAsync(
        Stream stream,
        string objectKey,
        string contentType,
        long? size,
        CancellationToken ct);

    /// <summary>
    /// Deletes the object with the given key. MUST be idempotent: a missing object
    /// (e.g. already deleted / orphan) MUST NOT throw — it is treated as success.
    /// </summary>
    Task DeleteFileAsync(
        string objectKey,
        CancellationToken ct);

    /// <summary>
    /// Generates a presigned download URL for the object. The returned URI host MUST be
    /// the configured public endpoint (never the internal compose endpoint) — see ADR-4.
    /// </summary>
    /// <param name="ttl">Time-to-live of the presigned URL.</param>
    Task<Uri> GetPresignedUrlAsync(
        string objectKey,
        TimeSpan ttl,
        CancellationToken ct);

    /// <summary>
    /// Generates a presigned POST URL and fields for direct client-side upload.
    /// </summary>
    Task<(string Url, IReadOnlyDictionary<string, string> Fields)> GeneratePresignedPostAsync(
        string objectKey,
        string contentType,
        TimeSpan ttl,
        CancellationToken ct);
}
