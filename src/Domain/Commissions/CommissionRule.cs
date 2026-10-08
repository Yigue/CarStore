using Domain.Shared.ValueObjects;

namespace Domain.Commissions;

public enum CommissionRuleKind
{
    Percentage,
    Fixed
}

/// <summary>
/// Commission rule of a salesperson / dealer: a percentage of the net price, or a fixed amount.
/// </summary>
public sealed record CommissionRule
{
    public CommissionRuleKind Kind { get; }

    /// <summary>Percentage in [0, 100]; only meaningful for <see cref="CommissionRuleKind.Percentage"/>.</summary>
    public decimal Percent { get; }

    /// <summary>Fixed amount; only meaningful for <see cref="CommissionRuleKind.Fixed"/>.</summary>
    public Money? FixedAmount { get; }

    private CommissionRule(CommissionRuleKind kind, decimal percent, Money? fixedAmount)
    {
        Kind = kind;
        Percent = percent;
        FixedAmount = fixedAmount;
    }

    public static CommissionRule Percentage(decimal percent) => new(CommissionRuleKind.Percentage, percent, null);

    public static CommissionRule Fixed(Money amount) => new(CommissionRuleKind.Fixed, 0m, amount);
}
