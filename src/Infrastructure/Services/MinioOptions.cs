using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Services;

/// <summary>
/// Strongly-typed options bound from the <c>Storage:Minio</c> configuration section.
/// See REQ-VMS-3 and ADR-4 (dual endpoint).
/// </summary>
public sealed class MinioOptions
{
    public const string SectionName = "Storage:Minio";

    /// <summary>Endpoint used by the backend Minio client for server-to-server calls (e.g. <c>http://minio:9000</c>).</summary>
    [Required(AllowEmptyStrings = false)]
    public string InternalEndpoint { get; set; } = string.Empty;

    /// <summary>Endpoint the browser can reach; presigned URLs are rewritten to this host (e.g. <c>http://localhost:9000</c>).</summary>
    [Required(AllowEmptyStrings = false)]
    public string PublicEndpoint { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    public string AccessKey { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    public string SecretKey { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    public string BucketName { get; set; } = string.Empty;

    public string Region { get; set; } = "us-east-1";

    public bool UseSsl { get; set; }

    public int PresignedReadTtlMinutes { get; set; } = 15;

    public int PresignedUploadTtlMinutes { get; set; } = 5;

    public int MaxUploadSizeMb { get; set; } = 10;

    public string[] AllowedContentTypes { get; set; } =
        ["image/jpeg", "image/png", "image/webp"];
}
