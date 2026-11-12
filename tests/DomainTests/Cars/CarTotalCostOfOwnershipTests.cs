using Domain.Cars;
using Domain.Cars.Attributes;
using Domain.Shared.ValueObjects;
using SharedKernel;

namespace DomainTests.Cars;

/// <summary>
/// ADR C1 / task B0.1.2 (H2): a car bought in USD whose reconditioning was also recorded in USD
/// must add up in USD. The false mixed-currency failure came from tasks being rehydrated as ARS.
/// A genuinely different currency still cannot be added without an exchange rate.
/// </summary>
public class CarTotalCostOfOwnershipTests
{
    private static Car NewCar() => new(
        Guid.NewGuid(), new Marca("Toyota"), new Modelo("Corolla", Guid.NewGuid()), Color.Blue, TypeCar.Sedan,
        StatusCar.Used, StatusServiceCar.Disponible, 4, 5, 1600, 90_000, 2020, "ABC123", "car",
        10_000m, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), FuelType.Gasolina, false, Transmission.Manual, null);

    [Fact]
    public void Tco_WithUsdPurchaseCostAndUsdReconditioning_Should_SumInUsd()
    {
        var car = NewCar();
        car.SetPurchaseCost(Money.Of(1000m, CurrencyCode.Usd), DateTime.UtcNow);
        var task = car.AddReconditioningTask("paint", Money.Of(200m, CurrencyCode.Usd));
        car.CompleteReconditioningTask(task.Id, DateTime.UtcNow);

        car.GetTotalCostOfOwnership().Should().Be(Money.Of(1200m, CurrencyCode.Usd));
    }

    [Fact]
    public void Tco_WithUsdPurchaseCostAndArsReconditioning_Should_ThrowOnlyBecauseCurrenciesTrulyDiffer()
    {
        var car = NewCar();
        car.SetPurchaseCost(Money.Of(1000m, CurrencyCode.Usd), DateTime.UtcNow);
        var task = car.AddReconditioningTask("paint", Money.Of(200m, CurrencyCode.Ars));
        car.CompleteReconditioningTask(task.Id, DateTime.UtcNow);

        FluentActions.Invoking(() => car.GetTotalCostOfOwnership())
            .Should().Throw<DomainException>().WithMessage("*different currencies*");
    }
}
