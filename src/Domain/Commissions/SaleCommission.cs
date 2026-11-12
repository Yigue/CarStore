using Domain.Shared.ValueObjects;
using SharedKernel;

namespace Domain.Commissions;

/// <summary>
/// sfc B4.1 / RN-008 / RN-009 — snapshot of the salesperson commission for one completed sale.
/// The amount is frozen at calculation time: a later change to <see cref="CommissionPolicy"/> never
/// recalculates it. One row per <see cref="SaleId"/> (unique index, Infrastructure) is the real
/// idempotency guard; <see cref="Create"/> itself does not deduplicate (see
/// CalculateSaleCommissionHandler, which checks before inserting).
/// </summary>
public sealed class SaleCommission : Entity
{
    public Guid SaleId { get; private set; }

    /// <summary>Null when the sale had no salesperson assigned.</summary>
    public Guid? SalespersonId { get; private set; }

    /// <summary>Net price: FinalPrice − TradeInValue, never negative, in the sale currency.</summary>
    public Money Base { get; private set; } = null!;

    /// <summary>Percentage applied; null for a fixed rule or when no policy was configured (RN-009).</summary>
    public decimal? Rate { get; private set; }

    /// <summary>Commission amount, in the sale currency. Zero (not an error) when unconfigured.</summary>
    public Money Amount { get; private set; } = null!;

    /// <summary>RN-009: false when no <see cref="CommissionPolicy"/> existed for the dealer at calculation time.</summary>
    public bool IsConfigured { get; private set; }

    public SaleCommissionStatus Status { get; private set; }

    public DateTime CalculatedAtUtc { get; private set; }

    private SaleCommission() { }

    /// <summary>Builds the frozen snapshot from a <see cref="CommissionResult"/> already computed by <see cref="CommissionCalculator"/>.</summary>
    public static SaleCommission Create(
        Guid dealerId,
        Guid saleId,
        Guid? salespersonId,
        CommissionResult result,
        DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(result);

        var commission = new SaleCommission
        {
            Id = Guid.NewGuid(),
            SaleId = saleId,
            SalespersonId = salespersonId,
            Base = result.Base,
            Rate = result.Rate,
            Amount = result.Amount,
            IsConfigured = result.IsConfigured,
            Status = SaleCommissionStatus.Pending,
            CalculatedAtUtc = nowUtc,
        };
        commission.SetDealer(dealerId);
        return commission;
    }

    /// <summary>
    /// Closes the B1.2.2 placeholder: a voided sale voids its commission. Idempotent — voiding an
    /// already-voided row (e.g. a replayed event) is a no-op, never an error.
    /// </summary>
    public void Void()
    {
        if (Status == SaleCommissionStatus.Voided)
            return;

        Status = SaleCommissionStatus.Voided;
    }
}

/// <summary>Lifecycle of a <see cref="SaleCommission"/> snapshot (sfc B4.1).</summary>
public enum SaleCommissionStatus
{
    Pending,
    Voided
}
