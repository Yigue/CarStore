using SharedKernel;

namespace Domain.Commissions;

public static class CommissionErrors
{
    public static Error PercentageOutOfRange() => Error.Validation(
        "Commissions.PercentageOutOfRange", "El porcentaje debe estar entre 0 y 100");

    public static Error CurrencyMismatch() => Error.Validation(
        "Commissions.CurrencyMismatch",
        "The trade-in value and fixed commission must be in the sale currency.");

    public static Error FixedAmountInvalid() => Error.Validation(
        "Commissions.FixedAmountInvalid", "A fixed commission needs an amount.");
}
