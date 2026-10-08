using SharedKernel;

namespace Domain.Deposits;

public static class DepositErrors
{
    public static Error AmountMustBePositive() => Error.Validation(
        "Deposits.AmountMustBePositive", "El monto de la seña debe ser mayor a cero");

    public static Error CurrencyRequired() => Error.Validation(
        "Deposits.CurrencyRequired", "Debe indicar la moneda de la seña");

    public static Error BuyerRequired() => Error.Validation(
        "Deposits.BuyerRequired", "La seña requiere un cliente o un lead");

    public static Error ExchangeRateRequired() => Error.Validation(
        "Deposits.ExchangeRateRequired",
        "La moneda de la seña difiere de la del vehículo: se requiere el tipo de cambio.");

    public static Error InvalidValidity(int maxDays) => Error.Validation(
        "Deposits.InvalidValidity",
        $"La fecha de vencimiento debe ser futura y no superar {maxDays} días.");

    /// <summary>RN-004. HTTP 409 DEPOSIT_ALREADY_ACTIVE.</summary>
    public static Error AlreadyActive(Guid carId) => Error.Conflict(
        "DEPOSIT_ALREADY_ACTIVE",
        $"The car with Id = '{carId}' already has an active deposit.");

    public static Error CarNotAvailable(Guid carId) => Error.Conflict(
        "Deposits.CarNotAvailable",
        $"The car with Id = '{carId}' is not available to take a deposit.");

    public static Error NotYetExpired(Guid depositId) => Error.Conflict(
        "Deposits.NotYetExpired", $"The deposit '{depositId}' has not reached its expiry date.");

    public static Error InvalidState(Guid depositId, DepositStatus current) => Error.Conflict(
        "Deposits.InvalidState", $"The deposit '{depositId}' is '{current}' and cannot perform this transition.");

    public static Error RefundReasonRequired() => Error.Validation(
        "Deposits.RefundReasonRequired", "A reason is required to refund a deposit.");

    public static Error RefundExceedsAmount(Guid depositId) => Error.Validation(
        "Deposits.RefundExceedsAmount", $"The refund exceeds the amount of deposit '{depositId}'.");
}
