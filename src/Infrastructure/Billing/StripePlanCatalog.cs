using Application.Abstractions.Billing;
using Infrastructure.Caching;
using Microsoft.Extensions.Options;
using Stripe;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Infrastructure.Billing;

/// <summary>
/// spr PR 1.1 (ADR B1, D1): reads the live price from Stripe and caches it 1h in <see cref="ICacheService"/>
/// (same pattern as <c>PermissionProvider</c>/<c>CachedBrandService</c>) — the landing/checkout/provisioning
/// never need to call Stripe directly.
/// </summary>
internal sealed class StripePlanCatalog : IPlanCatalog
{
    private readonly IStripeClient _stripeClient;
    private readonly ICacheService _cacheService;
    private readonly StripeOptions _options;

    public StripePlanCatalog(IStripeClient stripeClient, ICacheService cacheService, IOptions<StripeOptions> options)
    {
        _stripeClient = stripeClient;
        _cacheService = cacheService;
        _options = options.Value;
    }

    public async Task<PlanDetails?> GetAsync(string code, CancellationToken ct = default)
    {
        var plans = _options.ResolvePlans();
        if (string.IsNullOrWhiteSpace(code) || !plans.TryGetValue(code, out var plan))
        {
            return null;
        }

        var cacheKey = CacheKeys.BillingPlan(code);
        var cached = await _cacheService.GetAsync<PlanDetails>(cacheKey, ct);
        if (cached is not null)
        {
            return cached;
        }

        var priceService = new PriceService(_stripeClient);
        var price = await priceService.GetAsync(plan.PriceId, cancellationToken: ct);

        var details = new PlanDetails(
            code,
            plan.PriceId,
            price.UnitAmount ?? 0,
            price.Currency ?? "usd",
            price.Recurring?.Interval ?? "month",
            _options.TrialDays);

        await _cacheService.SetAsync(cacheKey, details, CacheTTL.BillingPlan, ct);
        return details;
    }

    public async Task<IReadOnlyList<PlanDetails>> GetAllAsync(CancellationToken ct = default)
    {
        var codes = _options.ResolvePlans().Keys.ToArray();
        var result = new List<PlanDetails>(codes.Length);

        foreach (var code in codes)
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
}
