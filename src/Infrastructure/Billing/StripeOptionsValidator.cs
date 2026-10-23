using Microsoft.Extensions.Options;

namespace Infrastructure.Billing;

/// <summary>
/// spr PR 1.1 (SPR-02/BE-BILLING-07, ADR B1, D1): "plan único" is a hard invariant until multi-plan
/// support ships — exactly one sellable plan. Fails fast on startup instead of silently selling the
/// wrong price or letting the landing page disagree with Stripe.
/// </summary>
public sealed class StripeOptionsValidator : IValidateOptions<StripeOptions>
{
    public ValidateOptionsResult Validate(string? name, StripeOptions options)
    {
        var plans = options.ResolvePlans();

        if (plans.Count != 1)
        {
            return ValidateOptionsResult.Fail(
                $"Stripe:Plans must expose exactly one sellable plan (found {plans.Count}). " +
                "Multi-plan support is out of scope for this release (ADR B1).");
        }

        return ValidateOptionsResult.Success;
    }
}
