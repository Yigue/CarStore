using Domain.Cars;
using Domain.Cars.Atribbutes;
using Domain.Cars.Events;

namespace DomainEventsTests;

public class CarEventsTests
{
    [Fact]
    public void New_car_raises_NewCarDomainEvent()
    {
        var marca = new Marca("Toyota") { Id = Guid.NewGuid() };
        var modelo = new Modelo("Corolla", marca.Id) { Id = Guid.NewGuid(), Marca = marca };

        var car = new Car(
            marca,
            modelo,
            Color.Black,
            TypeCar.Sedan,
            StatusCar.New,
            statusServiceCar.Disponible,
            4,
            5,
            1600,
            0,
            2024,
            "ABC123",
            "desc",
            10000m,
            DateTime.UtcNow);

        car.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<NewCarDomainEvent>()
            .Which.CarId.Should().Be(car.Id);
    }
}
