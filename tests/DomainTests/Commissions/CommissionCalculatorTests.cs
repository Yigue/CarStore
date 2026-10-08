using Domain.Commissions;
using Domain.Shared.ValueObjects;

namespace DomainTests.Commissions;

/// <summary>sfc B4 / ADR C5 — commission on net price (RN-008, RN-009).</summary>
public class CommissionCalculatorTests
{
    private static Money Ars(decimal amount) => new(amount, "ARS");

    [Fact]
    public void RN008_NoTradeIn_CalculatesOnFullPrice()
    {
        Result<CommissionResult> r = CommissionCalculator.Calculate(Ars(3_000_000m), null, CommissionRule.Percentage(2m));

        r.IsSuccess.Should().BeTrue();
        r.Value.Base.Should().Be(Ars(3_000_000m));
        r.Value.Amount.Should().Be(Ars(60_000m));
        r.Value.Rate.Should().Be(2m);
    }

    [Fact]
    public void RN008_WithTradeIn_CalculatesOnNet()
    {
        Result<CommissionResult> r = CommissionCalculator.Calculate(Ars(3_000_000m), Ars(500_000m), CommissionRule.Percentage(2m));

        r.Value.Base.Should().Be(Ars(2_500_000m));
        r.Value.Amount.Should().Be(Ars(50_000m));
    }

    [Fact]
    public void RN008_ResultIsInSaleCurrency()
    {
        Result<CommissionResult> r = CommissionCalculator.Calculate(new Money(20_000m, "USD"), null, CommissionRule.Percentage(3m));

        r.Value.Amount.Currency.Should().Be("USD");
        r.Value.Amount.Amount.Should().Be(600m);
    }

    [Fact]
    public void RN008_TradeInInDifferentCurrency_Fails()
    {
        Result<CommissionResult> r = CommissionCalculator.Calculate(Ars(100m), new Money(10m, "USD"), CommissionRule.Percentage(2m));

        r.Error.Should().Be(CommissionErrors.CurrencyMismatch());
    }

    [Fact]
    public void RN008_TradeInGreaterThanPrice_NeverNegative()
    {
        Result<CommissionResult> r = CommissionCalculator.Calculate(Ars(100m), Ars(150m), CommissionRule.Percentage(5m));

        r.Value.Base.Amount.Should().Be(0m);
        r.Value.Amount.Amount.Should().Be(0m);
    }

    [Fact]
    public void RN009_NoRuleConfigured_DefaultsToZeroAndFlagsUnconfigured()
    {
        Result<CommissionResult> r = CommissionCalculator.Calculate(Ars(3_000_000m), null, null);

        r.IsSuccess.Should().BeTrue();
        r.Value.Amount.Amount.Should().Be(0m);
        r.Value.IsConfigured.Should().BeFalse();
    }

    [Fact]
    public void RN009_ExplicitZeroPercent_IsConfigured()
    {
        Result<CommissionResult> r = CommissionCalculator.Calculate(Ars(3_000_000m), null, CommissionRule.Percentage(0m));

        r.Value.Amount.Amount.Should().Be(0m);
        r.Value.IsConfigured.Should().BeTrue();
    }

    [Fact]
    public void RN009_ConfiguredThreePercent_AppliesToFutureSales()
    {
        Result<CommissionResult> r = CommissionCalculator.Calculate(Ars(1_000_000m), null, CommissionRule.Percentage(3m));

        r.Value.Amount.Amount.Should().Be(30_000m);
        r.Value.IsConfigured.Should().BeTrue();
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(100.01)]
    public void RN009_PercentageOutOfRange_Fails(double pct)
    {
        Result<CommissionResult> r = CommissionCalculator.Calculate(Ars(100m), null, CommissionRule.Percentage((decimal)pct));

        r.Error.Should().Be(CommissionErrors.PercentageOutOfRange());
    }

    [Fact]
    public void RN009_PercentageBounds_0_And_100_AreValid()
    {
        CommissionCalculator.Calculate(Ars(100m), null, CommissionRule.Percentage(0m)).IsSuccess.Should().BeTrue();
        CommissionCalculator.Calculate(Ars(100m), null, CommissionRule.Percentage(100m)).Value.Amount.Amount.Should().Be(100m);
    }

    [Fact]
    public void RN008_FixedRule_PaysFixedAmount()
    {
        Result<CommissionResult> r = CommissionCalculator.Calculate(Ars(3_000_000m), Ars(500_000m), CommissionRule.Fixed(Ars(75_000m)));

        r.Value.Amount.Should().Be(Ars(75_000m));
        r.Value.Rate.Should().BeNull();
    }

    [Fact]
    public void RN008_FixedRule_CurrencyMustMatchSale()
    {
        CommissionCalculator.Calculate(Ars(100m), null, CommissionRule.Fixed(new Money(5m, "USD")))
            .Error.Should().Be(CommissionErrors.CurrencyMismatch());
    }

    [Fact]
    public void RN008_FixedRule_NoNetValue_PaysNothing()
    {
        CommissionCalculator.Calculate(Ars(100m), Ars(100m), CommissionRule.Fixed(Ars(5m)))
            .Value.Amount.Amount.Should().Be(0m);
    }

    // Rounding: MidpointRounding.ToEven, 2 decimals.
    [Theory]
    [InlineData(12.5, 1, 0.12)]      // 0.125 -> 0.12 (tie to even)
    [InlineData(13.5, 1, 0.14)]      // 0.135 -> 0.14 (tie to even)
    [InlineData(100.01, 2.5, 2.50)]  // 2.50025 -> 2.50
    [InlineData(33.33, 3, 1.00)]     // 0.9999 -> 1.00
    public void RN008_RoundsToEvenWithTwoDecimals(double price, double pct, double expected)
    {
        Result<CommissionResult> r = CommissionCalculator.Calculate(Ars((decimal)price), null, CommissionRule.Percentage((decimal)pct));

        r.Value.Amount.Amount.Should().Be((decimal)expected);
    }
}
