using Domain.Cars;
using Domain.Cars.Atribbutes;
using Domain.Cars.Events;
using SharedKernel;

public class CarTests
{
    private readonly Faker _faker = new();

    [Fact]
    public void Constructor_ShouldInitializeProperties()
    {
        var marca = new Marca(_faker.Vehicle.Manufacturer()) { Id = Guid.NewGuid() };
        var modelo = new Modelo(_faker.Vehicle.Model(), marca.Id) { Id = Guid.NewGuid(), Marca = marca };
        var color = _faker.PickRandom<Color>();
        var typeCar = _faker.PickRandom<TypeCar>();
        var statusCar = _faker.PickRandom<StatusCar>();
        var serviceCar = _faker.PickRandom<statusServiceCar>();
        var puertas = _faker.Random.Int(2, 5);
        var asientos = _faker.Random.Int(2, 7);
        var cilindrada = _faker.Random.Int(1000, 5000);
        var kilometraje = _faker.Random.Int(0, 200000);
        var anio = _faker.Date.Past(20, DateTime.UtcNow).Year;
        var patente = _faker.Random.AlphaNumeric(6);
        var descripcion = _faker.Lorem.Sentence();
        var price = _faker.Random.Decimal(1000, 100000);
        var now = DateTime.UtcNow;

        var car = new Car(
            marca,
            modelo,
            color,
            typeCar,
            statusCar,
            serviceCar,
            puertas,
            asientos,
            cilindrada,
            kilometraje,
            anio,
            patente,
            descripcion,
            price,
            now);

        car.Id.Should().NotBe(Guid.Empty);
        car.Marca.Should().Be(marca);
        car.MarcaId.Should().Be(marca.Id);
        car.Modelo.Should().Be(modelo);
        car.ModeloId.Should().Be(modelo.Id);
        car.Color.Should().Be(color);
        car.CarType.Should().Be(typeCar);
        car.CarStatus.Should().Be(statusCar);
        car.ServiceCar.Should().Be(serviceCar);
        car.CantidadPuertas.Should().Be(puertas);
        car.CantidadAsientos.Should().Be(asientos);
        car.Cilindrada.Should().Be(cilindrada);
        car.Kilometraje.Should().Be(kilometraje);
        car.Año.Should().Be(anio);
        car.Patente.Should().Be(patente);
        car.Descripcion.Should().Be(descripcion);
        car.Price.Should().Be(price);
        car.CreatedAt.Should().Be(now);
        car.UpdatedAt.Should().Be(now);
        car.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void CarErrors_ShouldReturnExpectedValues()
    {
        var carId = Guid.NewGuid();
        var imageId = Guid.NewGuid();

        var alreadySold = CarErrors.AlreadySold(carId);
        alreadySold.Code.Should().Be("Cars.AlreadySold");
        alreadySold.Description.Should().Be($"The car with Id = '{carId}' is already sold.");
        alreadySold.Type.Should().Be(ErrorType.Problem);

        var notFound = CarErrors.NotFound(carId);
        notFound.Code.Should().Be("Cars.NotFound");
        notFound.Description.Should().Be($"The car with the Id = '{carId}' was not found");
        notFound.Type.Should().Be(ErrorType.NotFound);

        var notAll = CarErrors.NotAllAtributes(carId);
        notAll.Code.Should().Be("Cars.NotAllAttributes");
        notAll.Description.Should().Be($"The car with the Id = '{carId}' was not found");
        notAll.Type.Should().Be(ErrorType.NotFound);

        var invalid = CarErrors.AtributesInvalid();
        invalid.Code.Should().Be("Cars.AtributesInvalid");
        invalid.Description.Should().Be("Atributes are invalid");
        invalid.Type.Should().Be(ErrorType.NotFound);

        var imageNotFound = CarErrors.ImageNotFound(imageId);
        imageNotFound.Code.Should().Be("Cars.ImageNotFound");
        imageNotFound.Description.Should().Be($"The car image with the Id = '{imageId}' was not found");
        imageNotFound.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public void CarEvents_ShouldContainCarId()
    {
        var carId = Guid.NewGuid();

        var newEvent = new NewCarDomainEvent(carId);
        newEvent.CarId.Should().Be(carId);

        var soldEvent = new CarSoldDomainEvent(carId);
        soldEvent.CarId.Should().Be(carId);

        var deleteEvent = new CarDeleteDomainEvent(carId);
        deleteEvent.CarId.Should().Be(carId);

        var statusEvent = new CarChangeStatusDomainEvent(carId);
        statusEvent.CarId.Should().Be(carId);
    }
}
