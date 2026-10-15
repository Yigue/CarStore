namespace Domain.Leads;

/// <summary>
/// sfc C0.1 / RN-045 / ADR C23: the visitor's first touch (campaign parameters, referrer, landing page and ad click ids),
/// captured by the public site and stored once on the lead. Values come from an anonymous visitor: they are trimmed and
/// capped, never trusted for anything but reporting.
/// </summary>
public sealed record LeadAttribution(
    string? UtmSource,
    string? UtmMedium,
    string? UtmCampaign,
    string? UtmTerm,
    string? UtmContent,
    string? Referrer,
    string? LandingPath,
    string? Gclid,
    string? Fbclid,
    DateTime FirstTouchAtUtc)
{
    public const int MaxLength = 200;

    // Same aliases as the public site's resolver (CarStore-FrontEnd/src/lib/leads/leadSourceResolver.ts).
    private static readonly Dictionary<string, LeadSource> SourceAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["facebook"] = LeadSource.Facebook,
        ["fb"] = LeadSource.Facebook,
        ["instagram"] = LeadSource.Instagram,
        ["ig"] = LeadSource.Instagram,
        ["google"] = LeadSource.Google,
        ["mercadolibre"] = LeadSource.MercadoLibre,
        ["ml"] = LeadSource.MercadoLibre,
        ["whatsapp"] = LeadSource.WhatsApp,
        ["wa"] = LeadSource.WhatsApp,
    };

    public static LeadAttribution Create(
        string? utmSource, string? utmMedium, string? utmCampaign, string? utmTerm, string? utmContent,
        string? referrer, string? landingPath, string? gclid, string? fbclid, DateTime firstTouchAtUtc) =>
        new(Clean(utmSource), Clean(utmMedium), Clean(utmCampaign), Clean(utmTerm), Clean(utmContent),
            Clean(referrer), Clean(landingPath), Clean(gclid), Clean(fbclid), firstTouchAtUtc);

    /// <summary>utm_source alias, else a click id (gclid → Google, fbclid → Facebook), else <paramref name="manual"/>.</summary>
    public LeadSource ResolveSource(LeadSource manual)
    {
        if (UtmSource is not null)
            return SourceAliases.TryGetValue(UtmSource, out LeadSource source) ? source : LeadSource.Web;
        if (Gclid is not null)
            return LeadSource.Google;
        if (Fbclid is not null)
            return LeadSource.Facebook;
        return manual;
    }

    private static string? Clean(string? value)
    {
        string? trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return null;
        return trimmed.Length <= MaxLength ? trimmed : trimmed[..MaxLength];
    }
}
