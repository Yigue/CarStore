using Domain.Currency;
using Domain.Financial.Attributes;
using Domain.Sales;
using Domain.Sales.Attributes;
using Domain.Shared.ValueObjects;
using FluentAssertions;

namespace DomainTests.Sales;

/// <summary>
/// sfc B0.2.3 (phase 1): a sale is priced in one explicit currency, and every amount of the sale (down payment, trade-in,
/// financed amount, installment) is in that same currency. Hard-coding ARS used to mix currencies inside one sale.
/// </summary>
public class SaleCurrencyTests
{
    private static readonly Guid DealerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTime T0 = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    private static Sale UsdSale(ExchangeRateSnapshot? snapshot = null) =>
        new(DealerId, Guid.NewGuid(), Guid.NewGuid(), 20000m, PaymentMethod.Cash, "C-1", "c", T0,
            currency: CurrencyCode.Usd, exchangeRate: snapshot);

    [Fact]
    public void Create_Should_DefaultToArs_WhenNoCurrencyIsGiven()
    {
        var sale = new Sale(DealerId, Guid.NewGuid(), Guid.NewGuid(), 20000m, PaymentMethod.Cash, "C-1", "c", T0);

        sale.FinalPrice.Currency.Should().Be("ARS");
        sale.ExchangeRate.Should().BeNull();
    }

    [Fact]
    public void Create_Should_UseTheExplicitCurrency_AndKeepTheSnapshot()
    {
        var snapshot = new ExchangeRateSnapshot("USD", "ARS", 1000m, T0, Guid.NewGuid());

        var sale = UsdSale(snapshot);

        sale.FinalPrice.Currency.Should().Be("USD");
        sale.ExchangeRate.Should().Be(snapshot);
    }

    [Fact]
    public void Update_Should_KeepTheSaleCurrency()
    {
        var sale = UsdSale();

        sale.Update(21000m, PaymentMethod.Cash, "C-1", "c");

        sale.FinalPrice.Should().Be(new Money(21000m, "USD"));
    }

    [Fact]
    public void SetPaymentTerms_Should_ExpressEveryPartInTheSaleCurrency()
    {
        var sale = UsdSale();

        var result = sale.SetPaymentTerms(5000m, Guid.NewGuid(), 3000m, 12000m, 12, 1000m, "Banco");

        result.IsSuccess.Should().BeTrue();
        sale.DownPayment!.Currency.Should().Be("USD");
        sale.TradeInValue!.Currency.Should().Be("USD");
        sale.FinancedAmount!.Currency.Should().Be("USD");
        sale.InstallmentAmount!.Currency.Should().Be("USD");
    }
}
