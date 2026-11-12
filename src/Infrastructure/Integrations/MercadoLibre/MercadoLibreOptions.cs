using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Integrations.MercadoLibre;

/// <summary>
/// sfc C1.2 / ADR C25: MercadoLibre's own OAuth2 app registration (one shared app, many dealer
/// accounts connect to it individually via <c>DealerIntegration</c>) plus the REST API base used
/// both for the push connector (pause/close) and the inbound webhook's pull-to-confirm.
/// </summary>
public sealed class MercadoLibreOptions
{
    public const string SectionName = "Integrations:MercadoLibre";

    [Required(AllowEmptyStrings = false)]
    public string ClientId { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>MercadoLibre's REST API — same host for OAuth token exchange and resource calls.</summary>
    public string ApiBaseUrl { get; set; } = "https://api.mercadolibre.com";
}
