using Domain.Cars.Attributes;
using Domain.Deposits;
using Domain.Deposits.Events;

namespace DomainTests.Deposits;

/// <summary>sfc B1 / ADR C4 — Deposit (seña) aggregate (RN-001..RN-004).</summary>
public class DepositTests
{
    private static readonly DateTime Now = new(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid DealerId = Guid.NewGuid();
    private static readonly Guid CarId = Guid.NewGuid();

    private static DepositCarContext Available(string currency = "ARS") =>
        new(StatusServiceCar.Disponible, null, false, currency);

    private static Deposit Active()
    {
        Result<Deposit> r = Deposit.Create(DealerId, CarId, Guid.NewGuid(), null, 500_000m, "ARS", null, Now, Available());
        return r.Value;
    }

    // ── RN-001: entity with its own data ─────────────────────────────────────────────────────

    [Fact]
    public void RN001_Create_ValidInput_IsActiveWithData()
    {
        Guid clientId = Guid.NewGuid();
        Guid quoteId = Guid.NewGuid();

        Result<Deposit> result = Deposit.Create(DealerId, CarId, clientId, quoteId, 500_000m, "ars", null, Now, Available());

        result.IsSuccess.Should().BeTrue();
        Deposit d = result.Value;
        d.DealerId.Should().Be(DealerId);
        d.CarId.Should().Be(CarId);
        d.ClientId.Should().Be(clientId);
        d.QuoteId.Should().Be(quoteId);
        d.Amount.Amount.Should().Be(500_000m);
        d.Amount.Currency.Should().Be("ARS");
        d.Status.Should().Be(DepositStatus.Active);
        d.CreatedAtUtc.Should().Be(Now);
        d.DomainEvents.Should().ContainSingle(e => e is DepositCreatedDomainEvent);
    }

    [Fact]
    public void RN001_Create_StoresExchangeRateSnapshot_WhenCurrencyDiffersFromCar()
    {
        Result<Deposit> result = Deposit.Create(DealerId, CarId, Guid.NewGuid(), null, 400m, "USD", 1200m, Now, Available("ARS"));

        result.IsSuccess.Should().BeTrue();
        result.Value.ExchangeRateSnapshot.Should().Be(1200m);
    }

    [Fact]
    public void RN001_Create_CurrencyDiffersFromCar_WithoutSnapshot_Fails()
    {
        Result<Deposit> result = Deposit.Create(DealerId, CarId, Guid.NewGuid(), null, 400m, "USD", null, Now, Available("ARS"));

        result.Error.Should().Be(DepositErrors.ExchangeRateRequired());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void RN001_Create_AmountMustBePositive(double amount)
    {
        Result<Deposit> result = Deposit.Create(DealerId, CarId, Guid.NewGuid(), null, (decimal)amount, "ARS", null, Now, Available());

        result.Error.Should().Be(DepositErrors.AmountMustBePositive());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public void RN001_Create_CurrencyRequired(string? currency)
    {
        Result<Deposit> result = Deposit.Create(DealerId, CarId, Guid.NewGuid(), null, 10m, currency!, null, Now, Available());

        result.Error.Should().Be(DepositErrors.CurrencyRequired());
    }

    [Fact]
    public void RN001_Create_RequiresClientOrLead()
    {
        Result<Deposit> result = Deposit.Create(DealerId, CarId, null, null, 10m, "ARS", null, Now, Available());

        result.Error.Should().Be(DepositErrors.BuyerRequired());
    }

    // ── RN-002: expiry, default 7 days, configurable ─────────────────────────────────────────

    [Fact]
    public void RN002_Create_DefaultExpiryIsSevenDays()
    {
        Deposit d = Active();

        d.ExpiresAtUtc.Should().Be(Now.AddDays(7));
        Deposit.DefaultValidityDays.Should().Be(7);
    }

    [Fact]
    public void RN002_Create_ConfigurableValidityDays()
    {
        Result<Deposit> r = Deposit.Create(DealerId, CarId, Guid.NewGuid(), null, 10m, "ARS", null, Now, Available(), validityDays: 3);

        r.Value.ExpiresAtUtc.Should().Be(Now.AddDays(3));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(91)]
    public void RN002_Create_ValidityMustBeFutureAndWithinDealerMax(int days)
    {
        Result<Deposit> r = Deposit.Create(DealerId, CarId, Guid.NewGuid(), null, 10m, "ARS", null, Now, Available(),
            validityDays: days, maxValidityDays: 90);

        r.Error.Should().Be(DepositErrors.InvalidValidity(90));
    }

    [Fact]
    public void RN002_RN003_Expire_AfterExpiresAt_MarksExpired_AndRaisesEvent_NoRefund()
    {
        Deposit d = Active();
        d.ClearDomainEvents();

        Result r = d.Expire(Now.AddDays(7));

        r.IsSuccess.Should().BeTrue();
        d.Status.Should().Be(DepositStatus.Expired);
        d.RefundedAmount.Should().BeNull();
        d.DomainEvents.OfType<DepositExpiredDomainEvent>().Should().ContainSingle()
            .Which.CarId.Should().Be(CarId);
    }

    [Fact]
    public void RN003_Expire_BeforeExpiresAt_Fails()
    {
        Deposit d = Active();

        d.Expire(Now.AddDays(6)).Error.Should().Be(DepositErrors.NotYetExpired(d.Id));
        d.Status.Should().Be(DepositStatus.Active);
    }

    // ── RN-004: one active deposit per car ───────────────────────────────────────────────────

    [Fact]
    public void RN004_Create_WhenCarAlreadyHasActiveDeposit_Fails()
    {
        var ctx = new DepositCarContext(StatusServiceCar.Reservado, null, true, "ARS");

        Result<Deposit> r = Deposit.Create(DealerId, CarId, Guid.NewGuid(), null, 10m, "ARS", null, Now, ctx);

        r.Error.Should().Be(DepositErrors.AlreadyActive(CarId));
        r.Error.Code.Should().Be("DEPOSIT_ALREADY_ACTIVE");
    }

    [Theory]
    [InlineData(StatusServiceCar.Vendido)]
    [InlineData(StatusServiceCar.Entregado)]
    [InlineData(StatusServiceCar.Ingreso)]
    [InlineData(StatusServiceCar.Reacondicionamiento)]
    public void RN004_Create_CarMustBeAvailable(StatusServiceCar status)
    {
        var ctx = new DepositCarContext(status, null, false, "ARS");

        Deposit.Create(DealerId, CarId, Guid.NewGuid(), null, 10m, "ARS", null, Now, ctx)
            .Error.Should().Be(DepositErrors.CarNotAvailable(CarId));
    }

    [Fact]
    public void RN004_Create_ReservedBySameQuote_IsAllowed()
    {
        Guid quoteId = Guid.NewGuid();
        var ctx = new DepositCarContext(StatusServiceCar.Reservado, quoteId, false, "ARS");

        Deposit.Create(DealerId, CarId, Guid.NewGuid(), quoteId, 10m, "ARS", null, Now, ctx)
            .IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void RN004_Create_ReservedByAnotherQuote_Fails()
    {
        var ctx = new DepositCarContext(StatusServiceCar.Reservado, Guid.NewGuid(), false, "ARS");

        Deposit.Create(DealerId, CarId, Guid.NewGuid(), Guid.NewGuid(), 10m, "ARS", null, Now, ctx)
            .Error.Should().Be(DepositErrors.CarNotAvailable(CarId));
    }

    // ── state machine guards ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Apply_Active_BecomesApplied()
    {
        Deposit d = Active();
        Guid saleId = Guid.NewGuid();
        d.ClearDomainEvents();

        d.Apply(saleId).IsSuccess.Should().BeTrue();

        d.Status.Should().Be(DepositStatus.Applied);
        d.AppliedSaleId.Should().Be(saleId);
        d.DomainEvents.Should().ContainSingle(e => e is DepositAppliedDomainEvent);
    }

    [Fact]
    public void Apply_Expired_Fails()
    {
        Deposit d = Active();
        d.Expire(Now.AddDays(8));

        d.Apply(Guid.NewGuid()).Error.Should().Be(DepositErrors.InvalidState(d.Id, DepositStatus.Expired));
    }

    [Fact]
    public void Refund_Active_RecordsAmountAndReason_NoMoneyMoves()
    {
        Deposit d = Active();

        d.Refund(500_000m, "Cliente desistió", Now.AddDays(1)).IsSuccess.Should().BeTrue();

        d.Status.Should().Be(DepositStatus.Refunded);
        d.RefundedAmount!.Amount.Should().Be(500_000m);
        d.RefundReason.Should().Be("Cliente desistió");
    }

    [Fact]
    public void Refund_Expired_IsAllowed()
    {
        Deposit d = Active();
        d.Expire(Now.AddDays(8));

        d.Refund(100m, "devolución manual", Now.AddDays(9)).IsSuccess.Should().BeTrue();
        d.Status.Should().Be(DepositStatus.Refunded);
    }

    [Fact]
    public void Refund_AmountExceedingDeposit_Fails()
    {
        Deposit d = Active();

        d.Refund(500_001m, "x", Now).Error.Should().Be(DepositErrors.RefundExceedsAmount(d.Id));
        d.Status.Should().Be(DepositStatus.Active);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Refund_NonPositiveAmount_Fails(double amount)
    {
        Deposit d = Active();

        d.Refund((decimal)amount, "x", Now).Error.Should().Be(DepositErrors.AmountMustBePositive());
    }

    [Fact]
    public void Refund_WithoutReason_Fails()
    {
        Deposit d = Active();

        d.Refund(10m, " ", Now).Error.Should().Be(DepositErrors.RefundReasonRequired());
    }

    [Fact]
    public void Refund_AlreadyApplied_Fails()
    {
        Deposit d = Active();
        d.Apply(Guid.NewGuid());

        d.Refund(10m, "x", Now).Error.Should().Be(DepositErrors.InvalidState(d.Id, DepositStatus.Applied));
    }

    [Fact]
    public void Expire_AlreadyRefunded_Fails()
    {
        Deposit d = Active();
        d.Refund(10m, "x", Now);

        d.Expire(Now.AddDays(30)).IsFailure.Should().BeTrue();
    }
}
