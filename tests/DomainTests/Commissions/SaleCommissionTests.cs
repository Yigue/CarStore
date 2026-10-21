using Domain.Commissions;
using Domain.Shared.ValueObjects;

namespace DomainTests.Commissions;

/// <summary>
/// sfc B4.1 / RN-008 / RN-009 — the <see cref="SaleCommission"/> snapshot. Base/Amount computation
/// itself is covered by CommissionCalculatorTests; these tests cover the entity that freezes a
/// CommissionResult for one sale (D-COMMISSION, Status lifecycle, idempotency boundary).
/// </summary>
public class SaleCommissionTests
{
    private static readonly Guid DealerId = Guid.NewGuid();
    private static readonly Guid SaleId = Guid.NewGuid();
    private static readonly Guid SalespersonId = Guid.NewGuid();
    private static readonly DateTime NowUtc = new(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);

    private static Money Ars(decimal amount) => new(amount, "ARS");

    [Fact]
    public void Create_SnapshotsBaseExcludingTradeIn()
    {
        Result<CommissionResult> calculation = CommissionCalculator.Calculate(
            Ars(3_000_000m), Ars(500_000m), CommissionRule.Percentage(2m));

        SaleCommission commission = SaleCommission.Create(DealerId, SaleId, SalespersonId, calculation.Value, NowUtc);

        commission.Base.Should().Be(Ars(2_500_000m));
        commission.Amount.Should().Be(Ars(50_000m));
        commission.Rate.Should().Be(2m);
        commission.SaleId.Should().Be(SaleId);
        commission.SalespersonId.Should().Be(SalespersonId);
        commission.DealerId.Should().Be(DealerId);
        commission.CalculatedAtUtc.Should().Be(NowUtc);
    }

    [Fact]
    public void Create_DefaultsToZeroRateWhenNoPolicyConfigured()
    {
        // D-COMMISSION: no CommissionRule (no CommissionPolicy for the dealer) → 0, not an error.
        Result<CommissionResult> calculation = CommissionCalculator.Calculate(Ars(1_000_000m), null, null);

        SaleCommission commission = SaleCommission.Create(DealerId, SaleId, SalespersonId, calculation.Value, NowUtc);

        commission.Amount.Should().Be(Ars(0m));
        commission.Rate.Should().BeNull();
        commission.IsConfigured.Should().BeFalse();
    }

    [Fact]
    public void Create_StartsAsPending()
    {
        Result<CommissionResult> calculation = CommissionCalculator.Calculate(Ars(1_000_000m), null, CommissionRule.Percentage(3m));

        SaleCommission commission = SaleCommission.Create(DealerId, SaleId, SalespersonId, calculation.Value, NowUtc);

        commission.Status.Should().Be(SaleCommissionStatus.Pending);
    }

    [Fact]
    public void Void_TransitionsPendingToVoided()
    {
        Result<CommissionResult> calculation = CommissionCalculator.Calculate(Ars(1_000_000m), null, CommissionRule.Percentage(3m));
        SaleCommission commission = SaleCommission.Create(DealerId, SaleId, SalespersonId, calculation.Value, NowUtc);

        commission.Void();

        commission.Status.Should().Be(SaleCommissionStatus.Voided);
    }

    [Fact]
    public void Void_CalledTwice_IsIdempotent()
    {
        Result<CommissionResult> calculation = CommissionCalculator.Calculate(Ars(1_000_000m), null, CommissionRule.Percentage(3m));
        SaleCommission commission = SaleCommission.Create(DealerId, SaleId, SalespersonId, calculation.Value, NowUtc);

        commission.Void();
        commission.Void();

        commission.Status.Should().Be(SaleCommissionStatus.Voided);
    }

    [Fact]
    public void Create_WithoutSalesperson_AllowsNullSalespersonId()
    {
        Result<CommissionResult> calculation = CommissionCalculator.Calculate(Ars(1_000_000m), null, null);

        SaleCommission commission = SaleCommission.Create(DealerId, SaleId, null, calculation.Value, NowUtc);

        commission.SalespersonId.Should().BeNull();
    }

    /// <summary>
    /// The entity itself does NOT self-deduplicate by SaleId — that is why idempotency is enforced one
    /// layer up: CalculateSaleCommissionHandler checks for an existing row before calling Create, and
    /// the unique index on SaleId (Infrastructure) is the last-resort guard against a concurrent race.
    /// Calling Create twice for the same sale therefore produces two independent in-memory rows,
    /// documenting that the invariant lives outside the entity.
    /// </summary>
    [Fact]
    public void Create_CalledTwiceForSameSale_ProducesIndependentRowsDomainLayerDoesNotDedupe()
    {
        Result<CommissionResult> calculation = CommissionCalculator.Calculate(Ars(1_000_000m), null, CommissionRule.Percentage(3m));

        SaleCommission first = SaleCommission.Create(DealerId, SaleId, SalespersonId, calculation.Value, NowUtc);
        SaleCommission second = SaleCommission.Create(DealerId, SaleId, SalespersonId, calculation.Value, NowUtc);

        first.Id.Should().NotBe(second.Id);
        first.SaleId.Should().Be(second.SaleId);
    }
}
