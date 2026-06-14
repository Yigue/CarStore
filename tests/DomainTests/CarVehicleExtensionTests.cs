using Domain.Cars;
using Domain.Cars.Attributes;

public class CarVehicleExtensionTests
{
    private static (Marca Marca, Modelo Modelo) CreateMarcaModelo()
    {
        var marca = new Marca("Toyota");
        var modelo = new Modelo("Hilux", marca.Id) { Marca = marca };
        return (marca, modelo);
    }

    [Fact]
    public void Constructor_ShouldPersist_FuelTypeFeaturedTransmissionAndPurchaseCost()
    {
        var (marca, modelo) = CreateMarcaModelo();

        var car = new Car(
            Guid.NewGuid(), marca, modelo, Color.Black, TypeCar.Sedan,
            StatusCar.New, StatusServiceCar.Disponible, 4, 5, 2000, 10000, 2020,
            "ABC123", "Test", 15000m, DateTime.UtcNow,
            fuelType: FuelType.Diesel, featured: true,
            transmission: Transmission.Automatic, purchaseCost: 9000m);

        car.FuelType.Should().Be(FuelType.Diesel);
        car.Featured.Should().BeTrue();
        car.Transmission.Should().Be(Transmission.Automatic);
        car.PurchaseCost.Should().NotBeNull();
        car.PurchaseCost!.Amount.Should().Be(9000m);
    }

    [Fact]
    public void Constructor_ShouldDefault_FeaturedFalseTransmissionManual_WhenNotProvided()
    {
        var (marca, modelo) = CreateMarcaModelo();

        var car = new Car(
            Guid.NewGuid(), marca, modelo, Color.Black, TypeCar.Sedan,
            StatusCar.New, StatusServiceCar.Disponible, 4, 5, 2000, 10000, 2020,
            "ABC124", "Test", 15000m, DateTime.UtcNow);

        car.Featured.Should().BeFalse();
        car.Transmission.Should().Be(Transmission.Manual);
        car.PurchaseCost.Should().BeNull();
    }

    [Fact]
    public void UpdateDetails_ShouldUpdate_FuelTypeFeaturedAndTransmission()
    {
        var (marca, modelo) = CreateMarcaModelo();
        var car = new Car(
            Guid.NewGuid(), marca, modelo, Color.Black, TypeCar.Sedan,
            StatusCar.New, StatusServiceCar.Disponible, 4, 5, 2000, 10000, 2020,
            "ABC125", "Test", 15000m, DateTime.UtcNow);

        car.UpdateDetails(
            marca, modelo, Color.White, TypeCar.Sedan, StatusCar.New,
            StatusServiceCar.Disponible, 4, 5, 2000, 12000, 2020, "ABC125", "Updated",
            DateTime.UtcNow,
            fuelType: FuelType.Electrico, featured: true, transmission: Transmission.CVT);

        car.FuelType.Should().Be(FuelType.Electrico);
        car.Featured.Should().BeTrue();
        car.Transmission.Should().Be(Transmission.CVT);
    }
}
