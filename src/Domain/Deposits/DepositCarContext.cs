using Domain.Cars.Attributes;

namespace Domain.Deposits;

/// <summary>
/// Facts about the car the caller must load before creating a deposit. Keeps the aggregate
/// pure: the "one active deposit per car" rule (RN-004) is evaluated here and backed by the
/// partial unique index <c>ux_deposits_one_active_per_car</c> (pending migration).
/// </summary>
/// <param name="Status">Current <c>Car.ServiceCar</c>.</param>
/// <param name="ReservedByQuoteId">Quote currently holding the reservation, if any.</param>
/// <param name="HasActiveDeposit">True when the car already has an Active deposit.</param>
/// <param name="CarCurrency">Currency of the car price (for the exchange-rate snapshot rule).</param>
public sealed record DepositCarContext(
    StatusServiceCar Status,
    Guid? ReservedByQuoteId,
    bool HasActiveDeposit,
    string CarCurrency);
