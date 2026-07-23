namespace Application.Dealers.Provision;

/// <summary>
/// Response payload for a successful <see cref="ProvisionDealerCommand"/>.
/// </summary>
public sealed record ProvisionDealerResponse(
    Guid DealerId,
    Guid AdminUserId,
    string Subdomain,
    string CheckoutUrl);