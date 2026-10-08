using Domain.Cars.Attributes;
using Domain.Deposits.Events;
using Domain.Shared.ValueObjects;
using SharedKernel;

namespace Domain.Deposits;

/// <summary>
/// sfc B1 / ADR C4 — seña as its own tenant-scoped aggregate (RN-001..RN-004).
/// States: Active → Expired / Refunded / Applied (see <see cref="DepositStatus"/>).
/// Pure domain: persistence (table, partial unique index) and jobs are wiring, pending.
/// </summary>
public sealed class Deposit : Entity
{
    /// <summary>RN-002: default validity in calendar days (D-DEPOSIT, configurable per dealer).</summary>
    public const int DefaultValidityDays = 7;

    /// <summary>Default upper bound for the dealer-configurable validity.</summary>
    public const int DefaultMaxValidityDays = 90;

    public Guid CarId { get; private set; }
    public Guid? QuoteId { get; private set; }
    public Guid? ClientId { get; private set; }
    public Guid? LeadId { get; private set; }
    public Money Amount { get; private set; } = null!;

    /// <summary>D-CURRENCY: rate at creation, set when the deposit currency differs from the car's.</summary>
    public decimal? ExchangeRateSnapshot { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public DepositStatus Status { get; private set; }
    public Money? RefundedAmount { get; private set; }
    public string? RefundReason { get; private set; }
    public DateTime? RefundedAtUtc { get; private set; }
    public Guid? AppliedSaleId { get; private set; }

    private Deposit() { }

    public static Result<Deposit> Create(
        Guid dealerId,
        Guid carId,
        Guid? clientId,
        Guid? quoteId,
        decimal amount,
        string currency,
        decimal? exchangeRateSnapshot,
        DateTime nowUtc,
        DepositCarContext car,
        int validityDays = DefaultValidityDays,
        int maxValidityDays = DefaultMaxValidityDays,
        Guid? leadId = null)
    {
        ArgumentNullException.ThrowIfNull(car);

        if (amount <= 0)
            return Result.Failure<Deposit>(DepositErrors.AmountMustBePositive());

        if (string.IsNullOrWhiteSpace(currency))
            return Result.Failure<Deposit>(DepositErrors.CurrencyRequired());

        if (clientId is null && leadId is null)
            return Result.Failure<Deposit>(DepositErrors.BuyerRequired());

        if (validityDays <= 0 || validityDays > maxValidityDays)
            return Result.Failure<Deposit>(DepositErrors.InvalidValidity(maxValidityDays));

        // RN-004
        if (car.HasActiveDeposit)
            return Result.Failure<Deposit>(DepositErrors.AlreadyActive(carId));

        if (!IsCarAvailable(car, quoteId))
            return Result.Failure<Deposit>(DepositErrors.CarNotAvailable(carId));

        string normalizedCurrency = currency.Trim().ToUpperInvariant();
        bool differs = !string.Equals(normalizedCurrency, car.CarCurrency?.Trim(), StringComparison.OrdinalIgnoreCase);
        if (differs && exchangeRateSnapshot is not > 0)
            return Result.Failure<Deposit>(DepositErrors.ExchangeRateRequired());

        var deposit = new Deposit
        {
            Id = Guid.NewGuid(),
            CarId = carId,
            QuoteId = quoteId,
            ClientId = clientId,
            LeadId = leadId,
            Amount = new Money(amount, normalizedCurrency),
            ExchangeRateSnapshot = differs ? exchangeRateSnapshot : null,
            CreatedAtUtc = nowUtc,
            ExpiresAtUtc = nowUtc.AddDays(validityDays),
            Status = DepositStatus.Active,
        };
        deposit.SetDealer(dealerId);
        deposit.Raise(new DepositCreatedDomainEvent(deposit.Id, carId, quoteId));
        return deposit;
    }

    /// <summary>RN-002/RN-003: Active → Expired once <see cref="ExpiresAtUtc"/> is reached. No refund.</summary>
    public Result Expire(DateTime nowUtc)
    {
        if (Status != DepositStatus.Active)
            return Result.Failure(DepositErrors.InvalidState(Id, Status));

        if (nowUtc < ExpiresAtUtc)
            return Result.Failure(DepositErrors.NotYetExpired(Id));

        Status = DepositStatus.Expired;
        Raise(new DepositExpiredDomainEvent(Id, CarId));
        return Result.Success();
    }

    /// <summary>Active → Applied when the sale over the car completes.</summary>
    public Result Apply(Guid saleId)
    {
        if (Status != DepositStatus.Active)
            return Result.Failure(DepositErrors.InvalidState(Id, Status));

        Status = DepositStatus.Applied;
        AppliedSaleId = saleId;
        Raise(new DepositAppliedDomainEvent(Id, CarId, saleId));
        return Result.Success();
    }

    /// <summary>Manual refund record from Active or Expired. Moves no money (RN-003).</summary>
    public Result Refund(decimal amount, string reason, DateTime nowUtc)
    {
        if (Status is not (DepositStatus.Active or DepositStatus.Expired))
            return Result.Failure(DepositErrors.InvalidState(Id, Status));

        if (amount <= 0)
            return Result.Failure(DepositErrors.AmountMustBePositive());

        if (string.IsNullOrWhiteSpace(reason))
            return Result.Failure(DepositErrors.RefundReasonRequired());

        if (amount > Amount.Amount)
            return Result.Failure(DepositErrors.RefundExceedsAmount(Id));

        Status = DepositStatus.Refunded;
        RefundedAmount = new Money(amount, Amount.Currency);
        RefundReason = reason.Trim();
        RefundedAtUtc = nowUtc;
        Raise(new DepositRefundedDomainEvent(Id, CarId, amount, RefundReason));
        return Result.Success();
    }

    private static bool IsCarAvailable(DepositCarContext car, Guid? quoteId) => car.Status switch
    {
        StatusServiceCar.Disponible or StatusServiceCar.EnVenta => true,
        StatusServiceCar.Reservado => quoteId is not null && quoteId == car.ReservedByQuoteId,
        _ => false,
    };
}
