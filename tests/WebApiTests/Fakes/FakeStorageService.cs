using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions.Storage;

namespace WebApiTests.Fakes;

/// <summary>
/// In-memory <see cref="IStorageService"/> double for integration tests. Records uploaded keys,
/// returns presigned-looking URLs against a configurable public endpoint (default localhost:9000),
/// and lets tests assert deletions. Idempotent delete mirrors the production contract.
/// </summary>
public sealed class FakeStorageService : IStorageService
{
    private readonly ConcurrentDictionary<string, byte[]> _objects = new();

    public string PublicHost { get; set; } = "localhost:9000";
    public string Scheme { get; set; } = "http";

    /// <summary>Optional hook to force a failure on delete (e.g. to test rollback paths).</summary>
    public Func<string, bool>? FailDeleteWhen { get; set; }

    public bool Exists(string objectKey) => _objects.ContainsKey(objectKey);

    public int Count => _objects.Count;

    public async Task<string> UploadFileAsync(Stream stream, string objectKey, string contentType, long? size, CancellationToken ct)
    {
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct);
        _objects[objectKey] = ms.ToArray();
        return objectKey;
    }

    public Task<(string Url, IReadOnlyDictionary<string, string> Fields)> GeneratePresignedPostAsync(
        string objectKey, string contentType, TimeSpan ttl, CancellationToken ct)
    {
        var url = $"{Scheme}://{PublicHost}/cars";
        IReadOnlyDictionary<string, string> fields = new System.Collections.Generic.Dictionary<string, string>
        {
            ["key"] = objectKey,
            ["Content-Type"] = contentType,
        };
        return Task.FromResult((url, fields));
    }

    public Task DeleteFileAsync(string objectKey, CancellationToken ct)
    {
        if (FailDeleteWhen is not null && FailDeleteWhen(objectKey))
        {
            throw new InvalidOperationException($"Simulated storage failure deleting {objectKey}");
        }

        // Idempotent: removing a missing key is a no-op (matches MinioStorageService 404 swallow).
        _objects.TryRemove(objectKey, out _);
        return Task.CompletedTask;
    }

    public Task<Uri> GetPresignedUrlAsync(string objectKey, TimeSpan ttl, CancellationToken ct)
    {
        // Presigned-looking URL whose host is always the PUBLIC endpoint (never minio:9000).
        var uri = new Uri($"{Scheme}://{PublicHost}/cars/{objectKey}?X-Amz-Signature=fake&X-Amz-Expires={(int)ttl.TotalSeconds}");
        return Task.FromResult(uri);
    }
}
