using Domain.Currency;
using Domain.Shared.ValueObjects;
using FluentAssertions;

namespace DomainTests.Currency;

/// <summary>
/// sfc B0.2.1 / ADR C2 / D-CURRENCY: an operation in a currency different from the vehicle's needs a manual exchange rate,
/// captured once as a snapshot and never recalculated.
/// </summary>
public class ExchangeRateSnapshotTests
{
    private static readonly Guid DealerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid UserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTime T0 = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    private static DealerExchangeRate Rate(decimal value, CurrencyCode? baseCurrency = null, CurrencyCode? quote = null) =>
        DealerExchangeRate.Record(DealerId, baseCurrency ?? CurrencyCode.Usd, quote ?? CurrencyCode.Ars, value, T0, UserId).Value;

    [Fact]
    public void Capture_Should_NotRequireARate_WhenOperationAndVehicleShareTheCurrency()
    {
        var result = ExchangeRateSnapshot.Capture(CurrencyCode.Ars, CurrencyCode.Ars, rate: null, T0);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
    }

    [Fact]
    public void Capture_Should_Fail_WhenCurrenciesDifferAndNoRateIsGiven()
    {
        var result = ExchangeRateSnapshot.Capture(CurrencyCode.Usd, CurrencyCode.Ars, rate: null, T0);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Currency.ExchangeRateRequired");
    }

    [Fact]
    public void Capture_Should_SnapshotTheRate_WhenCurrenciesDifferAndAMatchingRateExists()
    {
        var rate = Rate(1050.5m);

        var result = ExchangeRateSnapshot.Capture(CurrencyCode.Usd, CurrencyCode.Ars, rate, T0.AddHours(1));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Rate.Should().Be(1050.5m);
        result.Value.Base.Should().Be("USD");
        result.Value.Quote.Should().Be("ARS");
        result.Value.SourceRateId.Should().Be(rate.Id);
        result.Value.CapturedAtUtc.Should().Be(T0.AddHours(1));
    }

    [Fact]
    public void Record_Should_RejectASameCurrencyPair()
    {
        var result = DealerExchangeRate.Record(DealerId, CurrencyCode.Ars, CurrencyCode.Ars, 1m, T0, UserId);

        result.IsFailure.Should().BeTrue("a currency cannot be quoted against itself");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Record_Should_RejectNonPositiveRates(decimal rate)
    {
        var result = DealerExchangeRate.Record(DealerId, CurrencyCode.Usd, CurrencyCode.Ars, rate, T0, UserId);

        result.IsFailure.Should().BeTrue();
    }

    [Theory]
    [InlineData(1_000_000_000_000d)]   // above decimal(18,6) integer digits (12)
    [InlineData(1.0000001d)]            // more than 6 decimals
    public void Record_Should_RejectRatesThatDoNotFitTheColumn(double value)
    {
        var result = DealerExchangeRate.Record(DealerId, CurrencyCode.Usd, CurrencyCode.Ars, (decimal)value, T0, UserId);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Currency.RateOutOfRange");
    }

    [Fact]
    public void Snapshot_Should_NotChange_WhenANewRateIsLoadedLater()
    {
        var original = Rate(1000m);
        var snapshot = ExchangeRateSnapshot.Capture(CurrencyCode.Usd, CurrencyCode.Ars, original, T0.AddHours(1)).Value!;

        _ = Rate(1200m);

        snapshot.Rate.Should().Be(1000m);
        snapshot.SourceRateId.Should().Be(original.Id);
    }

    // sfc B1.3: a deposit taken in one currency counts towards a sale in the other, converted with its own snapshot.
    [Fact]
    public void Convert_Should_UseTheSnapshotRate_InBothDirections_AndKeepTheSameCurrency()
    {
        var snapshot = new ExchangeRateSnapshot("USD", "ARS", 1100m, T0, Guid.NewGuid());

        snapshot.Convert(Money.Of(10m, CurrencyCode.Usd), CurrencyCode.Ars).Should().Be(Money.Of(11_000m, CurrencyCode.Ars));
        snapshot.Convert(Money.Of(11_000m, CurrencyCode.Ars), CurrencyCode.Usd).Should().Be(Money.Of(10m, CurrencyCode.Usd));
        snapshot.Convert(Money.Of(5m, CurrencyCode.Usd), CurrencyCode.Usd).Should().Be(Money.Of(5m, CurrencyCode.Usd));
    }
}
