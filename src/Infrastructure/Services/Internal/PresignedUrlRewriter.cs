namespace Infrastructure.Services.Internal;

/// <summary>
/// Rewrites the host/port/scheme of a presigned URL produced against the internal MinIO
/// endpoint (e.g. <c>minio:9000</c>) to the public endpoint reachable by the browser
/// (e.g. <c>localhost:9000</c>), preserving the path and the full query string (signature).
/// See ADR-4 / REQ-VMS-4.
/// </summary>
internal static class PresignedUrlRewriter
{
    /// <summary>
    /// Returns a copy of <paramref name="presigned"/> whose scheme/host/port match
    /// <paramref name="publicEndpoint"/>. The path and query (including the AWS signature)
    /// are left intact. The internal host MUST NOT survive in the result.
    /// </summary>
    public static Uri Rewrite(Uri presigned, Uri publicEndpoint)
    {
        ArgumentNullException.ThrowIfNull(presigned);
        ArgumentNullException.ThrowIfNull(publicEndpoint);

        var builder = new UriBuilder(presigned)
        {
            Scheme = publicEndpoint.Scheme,
            Host = publicEndpoint.Host,
            // -1 (default) when no explicit port is given; UriBuilder treats -1 as "use default for scheme".
            Port = publicEndpoint.IsDefaultPort ? -1 : publicEndpoint.Port,
        };

        return builder.Uri;
    }

    /// <summary>Convenience overload accepting the public endpoint as a string.</summary>
    public static Uri Rewrite(Uri presigned, string publicEndpoint) =>
        Rewrite(presigned, NormalizeEndpoint(publicEndpoint));

    /// <summary>
    /// Normalizes a possibly scheme-less endpoint (e.g. <c>minio:9000</c>) into an absolute Uri.
    /// Defaults to <c>http</c> when no scheme is present.
    /// </summary>
    public static Uri NormalizeEndpoint(string endpoint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);

        if (!endpoint.Contains("://", StringComparison.Ordinal))
        {
            endpoint = "http://" + endpoint;
        }

        return new Uri(endpoint, UriKind.Absolute);
    }
}
