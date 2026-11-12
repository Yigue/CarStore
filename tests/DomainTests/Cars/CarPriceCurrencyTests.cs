using Domain.Cars;
using Domain.Cars.Attributes;
using Domain.Shared.ValueObjects;
using FluentAssertions;

namespace DomainTests.Cars;

/// <summary>
/// sfc B0.2.3: changing a car's price must not silently change its currency. Without an explicit currency the car keeps
/// the one it already has.
/// </summary>
public class CarPriceCurrencyTests
{
    private static Car NewUsdCar()
    {
        var marca = new Marca("Toyota");
        var modelo = new Modelo("Corolla", marca.Id);
        return new Car(Guid.NewGuid(), marca, modelo, Color.White, TypeCar.Sedan, StatusCar.New, StatusServiceCar.Disponible,
            4, 5, 1800, 0, 2024, "PRC001", "desc", 25000m, DateTime.UtcNow, priceCurrency: CurrencyCode.Usd);
    }

    [Fact]
    public void UpdatePrice_Should_KeepTheCurrentCurrency_WhenNoneIsGiven()
    {
        var car = NewUsdCar();

        car.UpdatePrice(26000m, DateTime.UtcNow);

        car.Price.Amount.Should().Be(26000m);
        car.Price.Currency.Should().Be("USD");
    }

    [Fact]
    public void UpdatePrice_Should_ApplyAnExplicitCurrency()
    {
        var car = NewUsdCar();

        car.UpdatePrice(26000000m, DateTime.UtcNow, CurrencyCode.Ars);

        car.Price.Currency.Should().Be("ARS");
    }
}
