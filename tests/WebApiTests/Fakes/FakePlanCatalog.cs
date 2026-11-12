using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions.Billing;
using Application.Billing.Entitlements;
using Infrastructure.Billing;
using Microsoft.Extensions.Options;

namespace WebApiTests.Fakes;

/// <summary>
/// Stands in for <c>StripePlanCatalog</c> in the API integration tests.
/// <para>
/// The test host is configured with <c>Stripe:SecretKey = sk_test_mock</c>, which turns billing on and
/// registers the real Stripe-backed catalog. Anything that asks for a plan's price (platform MRR, the
/// public plans endpoint) then calls Stripe and dies with "Invalid API Key provided". This fake keeps
/// the same config-driven notion of which plans exist and gives every one of them a fixed, non-zero
/// price so amounts computed from it can be asserted exactly.
/// </para>
/// </summary>
public sealed class FakePlanCatalog(IOptions<StripeOptions> options) : IPlanCatalog
{
    public const long AmountMinor = 4900;
    public const string Currency = "usd";

    private readonly StripeOptions _options = options.Value;

    public Task<PlanDetails?> GetAsync(string code, CancellationToken ct = default)
    {
        var plans = _options.ResolvePlans();
        if (string.IsNullOrWhiteSpace(code) || !plans.TryGetValue(code, out var plan))
        {
            return Task.FromResult<PlanDetails?>(null);
        }

        return Task.FromResult<PlanDetails?>(
            new PlanDetails(code, plan.PriceId, AmountMinor, Currency, "month", _options.TrialDays));
    }

    public async Task<IReadOnlyList<PlanDetails>> GetAllAsync(CancellationToken ct = default)
    {
        var result = new List<PlanDetails>();
        foreach (var code in _options.ResolvePlans().Keys.ToArray())
        {
            var details = await GetAsync(code, ct);
            if (details is not null)
            {
                result.Add(details);
            }
        }

        return result;
    }

    public bool IsSellable(string code) =>
        !string.IsNullOrWhiteSpace(code) && _options.ResolvePlans().ContainsKey(code);

    /// <summary>Config-only, exactly like the real catalog — entitlement tests configure limits per plan.</summary>
    public Task<PlanLimits?> GetEntitlementsAsync(string code, CancellationToken ct = default)
    {
        var plans = _options.ResolvePlans();
        if (string.IsNullOrWhiteSpace(code) || !plans.TryGetValue(code, out var plan) || plan.Limits is null)
        {
            return Task.FromResult<PlanLimits?>(null);
        }

        var limits = plan.Limits;
        return Task.FromResult<PlanLimits?>(
            new PlanLimits(limits.MaxVehicles, limits.MaxUsers, limits.MaxApiKeys, limits.MaxWebhooks));
    }

    public Task<IReadOnlyList<string>> GetFeaturesAsync(string code, CancellationToken ct = default)
    {
        var plans = _options.ResolvePlans();
        if (string.IsNullOrWhiteSpace(code) || !plans.TryGetValue(code, out var plan))
        {
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        }

        return Task.FromResult<IReadOnlyList<string>>(plan.Features);
    }
}
