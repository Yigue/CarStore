using Application.Abstractions.Pricing;
using Application.Cars.Create;
using Application.Quotes.Create;
using Application.Sales.Create;
using Domain.Cars.Attributes;
using QuotePaymentMethod = Domain.Quotes.Attributes.PaymentMethod;
using SalePaymentMethod = Domain.Financial.Attributes.PaymentMethod;

namespace ApplicationTests.Pricing;

/// <summary>sfc B0.2.3 / VB.2: <c>Money:RequireExplicitCurrency</c> turns the optional currency into a required one.</summary>
public class RequireExplicitCurrencyTests
{
    private sealed class FakePolicy(bool require) : IMoneyPolicy
    {
        public bool RequireExplicitCurrency => require;
    }

    private static CreateCarCommand Car(string? currency) =>
        new(Guid.NewGuid(), Guid.NewGuid(), Color.Black, TypeCar.Sedan, StatusCar.New, StatusServiceCar.Service,
            4, 5, 2000, 10000, 2020, "ABC123", "Test", 10000m, Currency: currency);

    private static CreateQuoteCommand Quote(string? currency) =>
        new(Guid.NewGuid(), Guid.NewGuid(), null, 1000m, QuotePaymentMethod.Contado, DateTime.UtcNow.AddDays(1), "Ok", currency);

    private static CreateSaleCommand Sale(string? currency) =>
        new(Guid.NewGuid(), Guid.NewGuid(), 1000m, SalePaymentMethod.Cash, "CN1", "ok", Currency: currency);

    [Fact]
    public void Off_ByDefault_AcceptsAMissingCurrency()
    {
        new CreateCarCommandValidator().Validate(Car(null)).IsValid.Should().BeTrue();
        new CreateQuoteCommandValidator().Validate(Quote(null)).IsValid.Should().BeTrue();
        new CreateSaleCommandValidator().Validate(Sale(null)).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Off_ExplicitPolicy_AcceptsAMissingCurrency()
    {
        var policy = new FakePolicy(false);
        new CreateCarCommandValidator(policy).Validate(Car(null)).IsValid.Should().BeTrue();
        new CreateQuoteCommandValidator(policy).Validate(Quote(null)).IsValid.Should().BeTrue();
        new CreateSaleCommandValidator(policy).Validate(Sale(null)).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void On_RejectsAMissingCurrency(string? currency)
    {
        var policy = new FakePolicy(true);

        new CreateCarCommandValidator(policy).Validate(Car(currency)).Errors
            .Should().ContainSingle(e => e.PropertyName == nameof(CreateCarCommand.Currency));
        new CreateQuoteCommandValidator(policy).Validate(Quote(currency)).Errors
            .Should().ContainSingle(e => e.PropertyName == nameof(CreateQuoteCommand.Currency));
        new CreateSaleCommandValidator(policy).Validate(Sale(currency)).Errors
            .Should().ContainSingle(e => e.PropertyName == nameof(CreateSaleCommand.Currency));
    }

    [Fact]
    public void On_AcceptsAnExplicitCurrency()
    {
        var policy = new FakePolicy(true);

        new CreateCarCommandValidator(policy).Validate(Car("USD")).IsValid.Should().BeTrue();
        new CreateQuoteCommandValidator(policy).Validate(Quote("ARS")).IsValid.Should().BeTrue();
        new CreateSaleCommandValidator(policy).Validate(Sale("USD")).IsValid.Should().BeTrue();
    }
}
