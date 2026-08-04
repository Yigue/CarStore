using Application.Abstractions.Storage;
using Infrastructure.Services.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel;
using Minio.DataModel.Args;
using Minio.Exceptions;

namespace Infrastructure.Services;

/// <summary>
/// MinIO-backed implementation of <see cref="IStorageService"/>. Sole production storage
/// implementation for the Cars aggregate (ADR-3). Uses the internal endpoint for all
/// server-to-server S3 calls and rewrites presigned URLs to the public endpoint (ADR-4).
/// </summary>
internal sealed class MinioStorageService : IStorageService
{
    private readonly IMinioClient _client;
    private readonly MinioOptions _options;
    private readonly Uri _publicEndpoint;
    private readonly ILogger<MinioStorageService> _logger;

    public MinioStorageService(
        IMinioClient client,
        IOptions<MinioOptions> options,
        ILogger<MinioStorageService> logger)
    {
        _client = client;
        _options = options.Value;
        _logger = logger;

        _publicEndpoint = PresignedUrlRewriter.NormalizeEndpoint(_options.PublicEndpoint);
    }

    public async Task<string> UploadFileAsync(
        Stream stream,
        string objectKey,
        string contentType,
        long? size,
        CancellationToken ct)
    {
        // The Minio SDK needs the object length; if the stream can't report it and size is null, buffer it.
        Stream uploadStream = stream;
        long length;

        if (size.HasValue)
        {
            length = size.Value;
        }
        else if (stream.CanSeek)
        {
            length = stream.Length - stream.Position;
        }
        else
        {
            var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, ct);
            buffer.Position = 0;
            uploadStream = buffer;
            length = buffer.Length;
        }

        var args = new PutObjectArgs()
            .WithBucket(_options.BucketName)
            .WithObject(objectKey)
            .WithStreamData(uploadStream)
            .WithObjectSize(length)
            .WithContentType(contentType);

        await _client.PutObjectAsync(args, ct);

        return objectKey;
    }

    public async Task DeleteFileAsync(string objectKey, CancellationToken ct)
    {
        try
        {
            var args = new RemoveObjectArgs()
                .WithBucket(_options.BucketName)
                .WithObject(objectKey);

            await _client.RemoveObjectAsync(args, ct);
        }
        catch (ObjectNotFoundException)
        {
            // Idempotent: a missing object (orphan / already deleted) is treated as success.
            _logger.LogInformation(
                "DeleteFileAsync: object {ObjectKey} not found in bucket {Bucket}; treating as deleted.",
                objectKey, _options.BucketName);
        }
        catch (MinioException ex) when (IsNoSuchKey(ex))
        {
            _logger.LogInformation(
                "DeleteFileAsync: object {ObjectKey} not found (NoSuchKey) in bucket {Bucket}; treating as deleted.",
                objectKey, _options.BucketName);
        }
    }

    public async Task<Uri> GetPresignedUrlAsync(string objectKey, TimeSpan ttl, CancellationToken ct)
    {
        var args = new PresignedGetObjectArgs()
            .WithBucket(_options.BucketName)
            .WithObject(objectKey)
            .WithExpiry((int)ttl.TotalSeconds);

        string presigned = await _client.PresignedGetObjectAsync(args);

        // ADR-4: never let the internal compose host reach the browser.
        return PresignedUrlRewriter.Rewrite(new Uri(presigned, UriKind.Absolute), _publicEndpoint);
    }

    public async Task<(string Url, IReadOnlyDictionary<string, string> Fields)> GeneratePresignedPostAsync(
        string objectKey,
        string contentType,
        TimeSpan ttl,
        CancellationToken ct)
    {
        var postPolicy = new PostPolicy();
        postPolicy.SetBucket(_options.BucketName);
        postPolicy.SetKey(objectKey);
        postPolicy.SetContentType(contentType);
        postPolicy.SetExpires(DateTime.UtcNow.Add(ttl));

        (Uri url, IDictionary<string, string> fields) = await _client.PresignedPostPolicyAsync(postPolicy);

        // The SDK records a `$Content-Type` policy *condition* but omits the matching form field.
        // Without it the browser POST violates the policy and MinIO answers 403. Emit it explicitly.
        var formFields = new Dictionary<string, string>(fields)
        {
            ["Content-Type"] = contentType,
        };

        // ADR-4: rewrite host to public endpoint
        Uri rewrittenUrl = PresignedUrlRewriter.Rewrite(url, _publicEndpoint);

        return (rewrittenUrl.ToString(), formFields);
    }

    private static bool IsNoSuchKey(MinioException ex) =>
        ex.ServerResponse?.StatusCode == System.Net.HttpStatusCode.NotFound ||
        (ex.Message?.Contains("NoSuchKey", StringComparison.OrdinalIgnoreCase) ?? false);
}
