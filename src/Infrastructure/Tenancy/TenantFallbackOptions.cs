namespace Infrastructure.Tenancy;

/// <summary>
/// Binds the <c>Tenant</c> configuration section in <c>appsettings.json</c>:
/// <code>
/// "Tenant": {
///   "DevFallbackDealerId": "00000000-0000-0000-0000-000000000000"
/// }
/// </code>
/// </summary>
/// <remarks>
/// <para>
/// <b>PR1 (saas-custom-domains) ADR-1:</b> the dev fallback that the previous
/// implementation silently used in any environment is now opt-in
/// <b>AND</b> gated by <see cref="Microsoft.Extensions.Hosting.IHostEnvironment.IsDevelopment"/>.
/// </para>
/// <para>
/// <b>Spec</b>: <c>openspec/changes/saas-custom-domains/specs/tenant-safety-default-deny/spec.md</c>.
/// The startup assertion in <c>Web.Api/Program.cs</c> throws
/// <see cref="InvalidOperationException"/> when a non-empty <see cref="DevFallbackDealerId"/>
/// is supplied outside Development, so a misconfigured Production build fails fast
/// instead of leaking cross-tenant data.
/// </para>
/// </remarks>
public sealed record TenantFallbackOptions
{
    /// <summary>Parameterless constructor required by <see cref="Microsoft.Extensions.Options.OptionsFactory{TOptions}"/>
    /// (<see cref="Activator.CreateInstance{T}"/> needs a public ctor).</summary>
    public TenantFallbackOptions()
    {
        DevFallbackDealerId = null;
    }

    public TenantFallbackOptions(Guid? devFallbackDealerId)
    {
        DevFallbackDealerId = devFallbackDealerId;
    }

    /// <summary>
    /// DealerId used by <c>CurrentTenantService</c> as a last-resort convenience
    /// when no JWT, header, or DB match resolves a tenant AND
    /// <see cref="Microsoft.Extensions.Hosting.IHostEnvironment.IsDevelopment"/> returns true.
    /// MUST be null outside Development (enforced at startup by Program.cs).
    /// </summary>
    public Guid? DevFallbackDealerId { get; init; }

    /// <summary>The configuration section name in <c>appsettings*.json</c>.</summary>
    public const string SectionName = "Tenant";
}
