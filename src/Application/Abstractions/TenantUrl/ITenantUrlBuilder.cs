namespace Application.Abstractions.TenantUrl;

/// <summary>
/// Builds tenant-aware frontend URLs. spr PR 3.3 (B11): replaces the hardcoded
/// <c>carstore.com</c> dashboard domain previously inlined in
/// <c>ProvisionDealerCommandHandler</c> — the template now lives in config
/// (<c>Frontend:TenantUrlTemplate</c>, <c>Frontend:BaseUrl</c>) so it can change per
/// environment without a code deploy.
/// </summary>
public interface ITenantUrlBuilder
{
    /// <summary>Dashboard URL for a given tenant subdomain slug, e.g. <c>https://acme.carstore.com/dashboard</c>.</summary>
    string BuildDashboardUrl(string slug);

    /// <summary>
    /// Stripe Checkout success URL pointing at the central "felicitaciones" page. Not tenant-specific —
    /// uses <c>Frontend:BaseUrl</c>. Includes the <c>{CHECKOUT_SESSION_ID}</c> placeholder Stripe
    /// substitutes at redirect time.
    /// </summary>
    string BuildCheckoutSuccessUrl();
}
