using Domain.Shared.ValueObjects;
using SharedKernel;

namespace Domain.Commissions;

/// <summary>
/// sfc B4 — pure commission calculator (RN-008, RN-009). Base = final price − trade-in
/// (clamped at zero) in the sale currency; no rule means 0 and "not configured".
/// </summary>
public static class CommissionCalculator
{
    private const int Decimals = 2;

    public static Result<CommissionResult> Calculate(Money finalPrice, Money? tradeInValue, CommissionRule? rule)
    {
        ArgumentNullException.ThrowIfNull(finalPrice);

        if (tradeInValue is not null && tradeInValue.Currency != finalPrice.Currency)
            return Result.Failure<CommissionResult>(CommissionErrors.CurrencyMismatch());

        decimal netAmount = Math.Max(0m, finalPrice.Amount - (tradeInValue?.Amount ?? 0m));
        var net = new Money(netAmount, finalPrice.Currency);

        if (rule is null)
            return new CommissionResult(net, null, new Money(0m, finalPrice.Currency), IsConfigured: false);

        switch (rule.Kind)
        {
            case CommissionRuleKind.Percentage:
                if (rule.Percent is < 0m or > 100m)
                    return Result.Failure<CommissionResult>(CommissionErrors.PercentageOutOfRange());

                decimal amount = Round(netAmount * rule.Percent / 100m);
                return new CommissionResult(net, rule.Percent, new Money(amount, finalPrice.Currency), true);

            case CommissionRuleKind.Fixed:
                if (rule.FixedAmount is null)
                    return Result.Failure<CommissionResult>(CommissionErrors.FixedAmountInvalid());
                if (rule.FixedAmount.Currency != finalPrice.Currency)
                    return Result.Failure<CommissionResult>(CommissionErrors.CurrencyMismatch());

                decimal fixedAmount = netAmount > 0m ? Round(rule.FixedAmount.Amount) : 0m;
                return new CommissionResult(net, null, new Money(fixedAmount, finalPrice.Currency), true);

            default:
                return Result.Failure<CommissionResult>(CommissionErrors.FixedAmountInvalid());
        }
    }

    private static decimal Round(decimal value) => Math.Round(value, Decimals, MidpointRounding.ToEven);
}
