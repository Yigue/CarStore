using Application.Cars.GetById;
using Domain.Cars;
using Domain.Cars.Atribbutes;
using Infrastructure.Database;

namespace WebApiTests;

public class CarsEndpointsTests
{
    [Fact]
    public async Task CreateCar_AddsCarToDatabase()
    {
        await using var factory = new CustomWebApplicationFactory();
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var marca = new Marca("Toyota") { Id = Guid.NewGuid() };
        var modelo = new Modelo("Corolla", marca.Id) { Id = Guid.NewGuid(), Marca = marca };
        context.AddRange(marca, modelo);
        await context.SaveChangesAsync();

        var client = factory.CreateClient();
        var request = new
        {
            Marca = marca.Id.ToString(),
            Modelo = modelo.Id.ToString(),
            Color = (int)Color.White,
            CarType = (int)TypeCar.Sedan,
            CarStatus = (int)StatusCar.New,
            ServiceCar = (int)statusServiceCar.Disponible,
            CantidadPuertas = 4,
            CantidadAsientos = 5,
            Cilindrada = 2000,
            Kilometraje = 10000,
            Año = 2020,
            Patente = "ABC123",
            Descripcion = "Test",
            Precio = 10000m
        };

        var response = await client.PostAsJsonAsync("/cars", request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var id = await response.Content.ReadFromJsonAsync<Guid>();

        context.Cars.Should().ContainSingle(c => c.Id == id);
    }

    [Fact]
    public async Task CreateCar_ReturnsBadRequest_ForInvalidColor()
    {
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        var request = new
        {
            Marca = Guid.NewGuid().ToString(),
            Modelo = Guid.NewGuid().ToString(),
            Color = 999,
            CarType = (int)TypeCar.Sedan,
            CarStatus = (int)StatusCar.New,
            ServiceCar = (int)statusServiceCar.Disponible,
            CantidadPuertas = 4,
            CantidadAsientos = 5,
            Cilindrada = 2000,
            Kilometraje = 10000,
            Año = 2020,
            Patente = "ABC123",
            Descripcion = "Test",
            Precio = 10000m
        };

        var response = await client.PostAsJsonAsync("/cars", request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetCarById_ReturnsCar_WhenExists()
    {
        await using var factory = new CustomWebApplicationFactory();
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var marca = new Marca("Ford") { Id = Guid.NewGuid() };
        var modelo = new Modelo("Fiesta", marca.Id) { Id = Guid.NewGuid(), Marca = marca };
        var car = new Car(marca, modelo, Color.Blue, TypeCar.Sedan, StatusCar.New, statusServiceCar.Disponible, 4,5,1600,5000,2019,"XYZ987","desc",15000m, DateTime.UtcNow);
        context.AddRange(marca, modelo, car);
        await context.SaveChangesAsync();

        var client = factory.CreateClient();
        var response = await client.GetAsync($"/cars/{car.Id}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<CarGetByIdResponse>();
        result!.Id.Should().Be(car.Id);
        result.Marca.Should().Be("Ford");
    }

    [Fact]
    public async Task GetCarById_ReturnsNotFound_WhenMissing()
    {
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        var response = await client.GetAsync($"/cars/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
