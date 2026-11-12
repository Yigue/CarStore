using Domain.Shared.ValueObjects;
using SharedKernel;

namespace DomainTests.ValueObjects;

/// <summary>ADR C1 / B0.1.3: closed set of ISO-4217 codes, normalized to upper case.</summary>
public class CurrencyCodeTests
{
    [Theory]
    [InlineData("usd", "USD")]
    [InlineData(" Ars ", "ARS")]
    [InlineData("USD", "USD")]
    public void Parse_Should_NormalizeToUpperCase(string input, string expected) =>
        CurrencyCode.Parse(input).Value.Should().Be(expected);

    [Theory]
    [InlineData("EUR")]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("US")]
    public void Parse_Should_RejectUnknownCodes(string input) =>
        FluentActions.Invoking(() => CurrencyCode.Parse(input)).Should().Throw<DomainException>();

    [Fact]
    public void MoneyOf_Should_UseTheGivenCurrency() =>
        Money.Of(10m, CurrencyCode.Usd).Currency.Should().Be("USD");

    // Review finding: new Money(100, "usd") used to be accepted and then broke same-currency arithmetic.
    [Fact]
    public void Constructor_WithLowerCaseCurrency_Should_NormalizeSoArithmeticWorks()
    {
        var sum = new Money(100m, "usd") + Money.Of(1m, CurrencyCode.Usd);
        sum.Should().Be(Money.Of(101m, CurrencyCode.Usd));
    }

    [Fact]
    public void Constructor_WithUnknownCurrency_Should_Throw() =>
        FluentActions.Invoking(() => new Money(1m, "EUR")).Should().Throw<DomainException>();
}
