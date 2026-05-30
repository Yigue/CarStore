using Application.Abstractions.Storage;
using Infrastructure.Services.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minio;
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
        IOptions<MinioOptions> options,
        ILogger<MinioStorageService> logger)
    {
        _options = options.Value;
        _logger = logger;

        Uri internalEndpoint = PresignedUrlRewriter.NormalizeEndpoint(_options.InternalEndpoint);
        _publicEndpoint = PresignedUrlRewriter.NormalizeEndpoint(_options.PublicEndpoint);

        string endpoint = internalEndpoint.IsDefaultPort
            ? internalEndpoint.Host
            : $"{internalEndpoint.Host}:{internalEndpoint.Port}";

        _client = new MinioClient()
            .WithEndpoint(endpoint)
            .WithCredentials(_options.AccessKey, _options.SecretKey)
            .WithRegion(_options.Region)
            .WithSSL(_options.UseSsl)
            .Build();
    }

    public async Task<string> UploadFileAsync(
        Stream stream,
        string objectKey,
        string contentType,
        CancellationToken ct)
    {
        // The Minio SDK needs the object length; if the stream can't report it, buffer it.
        Stream uploadStream = stream;
        long length;

        if (stream.CanSeek)
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

    private static bool IsNoSuchKey(MinioException ex) =>
        ex.ServerResponse?.StatusCode == System.Net.HttpStatusCode.NotFound ||
        (ex.Message?.Contains("NoSuchKey", StringComparison.OrdinalIgnoreCase) ?? false);
}
