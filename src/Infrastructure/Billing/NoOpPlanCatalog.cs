using Application.Abstractions.Billing;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Infrastructure.Billing;

/// <summary>
/// spr PR 1.1 (ADR B1): mirrors <see cref="NoOpSubscriptionGateway"/> — used when billing is disabled
/// (no Stripe secret key configured, e.g. local/CI). Never calls Stripe; returns deterministic stub
/// values for any plan declared in <see cref="StripeOptions.ResolvePlans"/>.
/// </summary>
internal sealed class NoOpPlanCatalog : IPlanCatalog
{
    private readonly StripeOptions _options;

    public NoOpPlanCatalog(Microsoft.Extensions.Options.IOptions<StripeOptions> options)
    {
        _options = options.Value;
    }

    public Task<PlanDetails?> GetAsync(string code, CancellationToken ct = default)
    {
        var plans = _options.ResolvePlans();
        if (string.IsNullOrWhiteSpace(code) || !plans.TryGetValue(code, out var plan))
        {
            return Task.FromResult<PlanDetails?>(null);
        }

        return Task.FromResult<PlanDetails?>(new PlanDetails(code, plan.PriceId, 0, "usd", "month", _options.TrialDays));
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
