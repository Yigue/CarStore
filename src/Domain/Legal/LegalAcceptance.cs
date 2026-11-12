using System.Text.RegularExpressions;
using SharedKernel;

namespace Domain.Legal;

/// <summary>
/// Append-only record that a user accepted a specific version of a legal document (RN-016).
/// It is tenant-scoped through <see cref="DealerId"/> but intentionally NOT an <see cref="Entity"/>
/// and has no tenant query filter: acceptances are written while a dealer is being provisioned,
/// before a tenant context exists. There are no mutators; the DbContext rejects updates/deletes.
/// </summary>
public sealed partial class LegalAcceptance
{
    private LegalAcceptance()
    {
    }

    public Guid Id { get; private set; }
    public Guid DealerId { get; private set; }
    public Guid UserId { get; private set; }
    public LegalDocumentKind DocumentKind { get; private set; }
    public string Version { get; private set; } = string.Empty;
    public string ContentSha256 { get; private set; } = string.Empty;
    public DateTime AcceptedAtUtc { get; private set; }
    public string? Ip { get; private set; }
    public string? UserAgent { get; private set; }

    public static LegalAcceptance Create(
        Guid dealerId,
        Guid userId,
        LegalDocumentKind documentKind,
        string version,
        string contentSha256,
        DateTime acceptedAtUtc,
        string? ip,
        string? userAgent)
    {
        if (dealerId == Guid.Empty)
        {
            throw new DomainException("DealerId cannot be empty");
        }

        if (userId == Guid.Empty)
        {
            throw new DomainException("UserId cannot be empty");
        }

        if (string.IsNullOrWhiteSpace(version) || !VersionPattern().IsMatch(version))
        {
            throw new DomainException("Legal document version must have the format YYYY-MM-DD");
        }

        if (string.IsNullOrWhiteSpace(contentSha256) || !Sha256Pattern().IsMatch(contentSha256))
        {
            throw new DomainException("Legal document hash must be a 64-character hex SHA-256");
        }

        return new LegalAcceptance
        {
            Id = Guid.NewGuid(),
            DealerId = dealerId,
            UserId = userId,
            DocumentKind = documentKind,
            Version = version,
            ContentSha256 = contentSha256.ToLowerInvariant(),
            AcceptedAtUtc = acceptedAtUtc,
            Ip = Truncate(ip, 64),
            UserAgent = Truncate(userAgent, 512)
        };
    }

    private static string? Truncate(string? value, int max) =>
        value is null || value.Length <= max ? value : value[..max];

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}$")]
    private static partial Regex VersionPattern();

    [GeneratedRegex("^[0-9a-fA-F]{64}$")]
    private static partial Regex Sha256Pattern();
}
