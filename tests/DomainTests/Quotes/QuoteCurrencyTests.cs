using Domain.Cars;
using Domain.Cars.Attributes;
using Domain.Clients;
using Domain.Quotes;
using Domain.Quotes.Attributes;
using Domain.Shared.ValueObjects;
using FluentAssertions;

namespace DomainTests.Quotes;

/// <summary>
/// sfc B0.2.3 (phase 1): a quote can be priced in an explicit currency, and editing its price keeps that currency
/// unless another one is given. Hard-coding ARS on update used to convert a USD quote silently.
/// </summary>
public class QuoteCurrencyTests
{
    private static readonly Guid DealerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTime T0 = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    private static Car NewCar()
    {
        var marca = new Marca("Toyota");
        var modelo = new Modelo("Corolla", marca.Id);
        return new Car(DealerId, marca, modelo, Color.White, TypeCar.Sedan, StatusCar.New, StatusServiceCar.Disponible,
            4, 5, 1800, 0, 2024, "QTE001", "desc", 25000m, T0);
    }

    private static Client NewClient() =>
        new(DealerId, "Ana", "Pérez", "30111222", "ana@test.com", "1155550000", "Av. Test 123", T0);

    [Fact]
    public void Create_Should_DefaultToArs_WhenNoCurrencyIsGiven()
    {
        var quote = new Quote(DealerId, NewCar(), NewClient(), null, 21500m, PaymentMethod.Contado, T0.AddDays(7), "c", T0);

        quote.ProposedPrice.Currency.Should().Be("ARS");
    }

    [Fact]
    public void Create_Should_UseTheExplicitCurrency()
    {
        var quote = new Quote(DealerId, NewCar(), NewClient(), null, 21500m, PaymentMethod.Contado, T0.AddDays(7), "c", T0,
            proposedCurrency: CurrencyCode.Usd);

        quote.ProposedPrice.Currency.Should().Be("USD");
    }

    [Fact]
    public void Update_Should_KeepTheCurrency_WhenNoneIsGiven()
    {
        var quote = new Quote(DealerId, NewCar(), NewClient(), null, 21500m, PaymentMethod.Contado, T0.AddDays(7), "c", T0,
            proposedCurrency: CurrencyCode.Usd);

        quote.Update(22000m, T0.AddDays(8), "c2", T0.AddHours(1));

        quote.ProposedPrice.Amount.Should().Be(22000m);
        quote.ProposedPrice.Currency.Should().Be("USD");
    }
}
