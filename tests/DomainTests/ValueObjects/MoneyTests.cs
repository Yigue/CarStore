using Domain.Shared.ValueObjects;
using SharedKernel;

namespace DomainTests.ValueObjects;

public class MoneyTests
{
    [Fact]
    public void Constructor_WithValidAmount_ShouldCreateMoney()
    {
        var amount = 1000.50m;
        var money = new Money(amount);

        money.Amount.Should().Be(amount);
        money.Currency.Should().Be(Money.DefaultCurrency);
    }

    // ADR C1: the implicit currency is the one the platform has always served (ARS). It must match
    // the backfill of the new *_currency columns, otherwise rows created through the
    // single-argument constructor would be stored as USD while legacy rows read as ARS.
    [Fact]
    public void DefaultCurrency_ShouldBeArs()
    {
        Money.DefaultCurrency.Should().Be("ARS");
        new Money(10m).Currency.Should().Be("ARS");
    }

    [Fact]
    public void Constructor_WithValidAmountAndCurrency_ShouldCreateMoney()
    {
        var amount = 1000.50m;
        var currency = "ARS";
        var money = new Money(amount, currency);

        money.Amount.Should().Be(amount);
        money.Currency.Should().Be(currency);
    }

    [Fact]
    public void Constructor_WithNegativeAmount_ShouldThrowDomainException()
    {
        var action = () => new Money(-100m);

        action.Should().Throw<DomainException>()
            .WithMessage("Money amount cannot be negative");
    }

    [Fact]
    public void Zero_ShouldReturnMoneyWithZeroAmount()
    {
        var zero = Money.Zero;

        zero.Amount.Should().Be(0m);
        zero.Currency.Should().Be(Money.DefaultCurrency);
    }

    [Fact]
    public void Addition_WithSameCurrency_ShouldReturnSum()
    {
        var money1 = new Money(100m);
        var money2 = new Money(50m);

        var result = money1 + money2;

        result.Amount.Should().Be(150m);
        result.Currency.Should().Be(Money.DefaultCurrency);
    }

    [Fact]
    public void Addition_WithDifferentCurrencies_ShouldThrowDomainException()
    {
        var money1 = new Money(100m, "USD");
        var money2 = new Money(50m, "ARS");

        var action = () => money1 + money2;

        action.Should().Throw<DomainException>()
            .WithMessage("Cannot add money with different currencies");
    }

    [Fact]
    public void Subtraction_WithSameCurrency_ShouldReturnDifference()
    {
        var money1 = new Money(100m);
        var money2 = new Money(30m);

        var result = money1 - money2;

        result.Amount.Should().Be(70m);
        result.Currency.Should().Be(Money.DefaultCurrency);
    }

    [Fact]
    public void Subtraction_WithDifferentCurrencies_ShouldThrowDomainException()
    {
        var money1 = new Money(100m, "USD");
        var money2 = new Money(50m, "ARS");

        var action = () => money1 - money2;

        action.Should().Throw<DomainException>()
            .WithMessage("Cannot subtract money with different currencies");
    }

    [Fact]
    public void Multiplication_ShouldReturnMultipliedAmount()
    {
        var money = new Money(100m);
        var multiplier = 1.5m;

        var result = money * multiplier;

        result.Amount.Should().Be(150m);
        result.Currency.Should().Be(Money.DefaultCurrency);
    }

    [Fact]
    public void GreaterThan_WithSameCurrency_ShouldCompareCorrectly()
    {
        var money1 = new Money(100m);
        var money2 = new Money(50m);

        (money1 > money2).Should().BeTrue();
        (money2 > money1).Should().BeFalse();
    }

    [Fact]
    public void LessThan_WithSameCurrency_ShouldCompareCorrectly()
    {
        var money1 = new Money(50m);
        var money2 = new Money(100m);

        (money1 < money2).Should().BeTrue();
        (money2 < money1).Should().BeFalse();
    }

    [Fact]
    public void Comparison_WithDifferentCurrencies_ShouldThrowDomainException()
    {
        var money1 = new Money(100m, "USD");
        var money2 = new Money(50m, "ARS");

        var action1 = () => money1 > money2;
        var action2 = () => money1 < money2;

        action1.Should().Throw<DomainException>()
            .WithMessage("Cannot compare money with different currencies");
        action2.Should().Throw<DomainException>()
            .WithMessage("Cannot compare money with different currencies");
    }
}

