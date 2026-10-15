using Domain.Cars;
using Domain.Cars.Attributes;
using Domain.Shared.ValueObjects;
using FluentAssertions;

namespace DomainTests.Cars;

/// <summary>
/// sfc B7.1 / RN-036: a consigned vehicle belongs to a third party. The dealer's income on its sale is only the agreed
/// consignment commission.
/// </summary>
public class ConsignmentTests
{
    private static Car NewCar()
    {
        var marca = new Marca("Toyota");
        var modelo = new Modelo("Corolla", marca.Id);
        return new Car(Guid.NewGuid(), marca, modelo, Color.White, TypeCar.Sedan, StatusCar.Used, StatusServiceCar.Disponible,
            4, 5, 1800, 40000, 2020, "CSG001", "desc", 2_000_000m, DateTime.UtcNow);
    }

    [Fact]
    public void A_consigned_car_earns_only_the_commission()
    {
        var car = NewCar();
        var owner = Guid.NewGuid();

        car.PlaceOnConsignment(owner, 0.10m).IsSuccess.Should().BeTrue();

        car.IsConsigned.Should().BeTrue();
        car.ConsignmentOwnerClientId.Should().Be(owner);
        car.DealerIncomeFrom(Money.Of(2_000_000m, CurrencyCode.Ars)).Should().Be(Money.Of(200_000m, CurrencyCode.Ars));
    }

    [Fact]
    public void An_own_car_earns_the_whole_sale_price()
    {
        var car = NewCar();

        car.IsConsigned.Should().BeFalse();
        car.DealerIncomeFrom(Money.Of(2_000_000m, CurrencyCode.Ars)).Should().Be(Money.Of(2_000_000m, CurrencyCode.Ars));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void The_commission_rate_must_be_above_zero_and_at_most_one(double rate)
    {
        NewCar().PlaceOnConsignment(Guid.NewGuid(), (decimal)rate).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Ending_the_consignment_makes_the_car_own_stock_again()
    {
        var car = NewCar();
        car.PlaceOnConsignment(Guid.NewGuid(), 0.1m);

        car.EndConsignment();

        car.IsConsigned.Should().BeFalse();
        car.ConsignmentCommissionRate.Should().BeNull();
    }
}
