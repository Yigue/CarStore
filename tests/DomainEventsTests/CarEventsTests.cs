using Domain.Cars;
using Domain.Cars.Attributes;
using Domain.Cars.Events;

namespace DomainEventsTests;

public class CarEventsTests
{
    [Fact]
    public void New_car_raises_NewCarDomainEvent()
    {
        var dealerId = Guid.NewGuid();
        var marca = new Marca("Toyota");
        var modelo = new Modelo("Corolla", marca.Id) { Marca = marca };

        var car = new Car(
            dealerId,
            marca,
            modelo,
            Color.Black,
            TypeCar.Sedan,
            StatusCar.New,
            StatusServiceCar.Disponible,
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
