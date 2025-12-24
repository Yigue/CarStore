using Application.Cars.GetById;
using Domain.Cars;
using Domain.Cars.Atribbutes;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;

namespace WebApiTests.IntegrationTests;

/// <summary>
/// Tests de integración para endpoints de Cars usando datos seedeados
/// </summary>
public class CarsIntegrationTests
{
    [Fact]
    public async Task CreateCar_WithSeededBrandAndModel_ShouldSucceed()
    {
        await using var factory = new CustomWebApplicationFactory();
        var token = await IntegrationTestHelpers.GetAdminTokenAsync(factory);
        var client = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(client, token);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        var toyota = await context.Marca.FirstAsync(m => m.Nombre == "Toyota");
        var corolla = await context.Modelo.FirstAsync(m => m.Nombre == "Corolla" && m.MarcaId == toyota.Id);

        var request = new
        {
            Marca = toyota.Id.ToString(),
            Modelo = corolla.Id.ToString(),
            Color = (int)Color.Blue,
            CarType = (int)TypeCar.Sedan,
            CarStatus = (int)StatusCar.New,
            ServiceCar = (int)statusServiceCar.Disponible,
            CantidadPuertas = 4,
            CantidadAsientos = 5,
            Cilindrada = 2000,
            Kilometraje = 0,
            Año = 2024,
            Patente = "ABC123",
            Descripcion = "Nuevo Toyota Corolla",
            Precio = 25000m
        };

        var response = await client.PostAsJsonAsync("/cars", request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var carId = await response.Content.ReadFromJsonAsync<Guid>();
        carId.Should().NotBe(Guid.Empty);

        var createdCar = await context.Cars
            .Include(c => c.Marca)
            .Include(c => c.Modelo)
            .FirstAsync(c => c.Id == carId);
        
        createdCar.Marca.Nombre.Should().Be("Toyota");
        createdCar.Modelo.Nombre.Should().Be("Corolla");
        createdCar.Patente.Value.Should().Be("ABC123");
        createdCar.Price.Amount.Should().Be(25000m);
    }

    [Fact]
    public async Task GetCars_WithSeededData_ShouldReturnCars()
    {
        await using var factory = new CustomWebApplicationFactory();
        var token = await IntegrationTestHelpers.GetAdminTokenAsync(factory);
        var client = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(client, token);

        // Crear un carro usando datos seedeados
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        var ford = await context.Marca.FirstAsync(m => m.Nombre == "Ford");
        var fiesta = await context.Modelo.FirstAsync(m => m.Nombre == "Fiesta" && m.MarcaId == ford.Id);
        
        var car = new Domain.Cars.Car(
            ford,
            fiesta,
            Color.Red,
            TypeCar.Hatchback,
            StatusCar.New,
            statusServiceCar.Disponible,
            4,
            5,
            1600,
            0,
            2023,
            "XYZ789",
            "Ford Fiesta nuevo",
            18000m,
            DateTime.UtcNow);
        
        context.Cars.Add(car);
        await context.SaveChangesAsync();

        var response = await client.GetAsync("/cars");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var cars = await response.Content.ReadFromJsonAsync<List<object>>();
        cars.Should().NotBeNull();
        cars!.Count.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetCarById_WithSeededData_ShouldReturnCar()
    {
        await using var factory = new CustomWebApplicationFactory();
        var token = await IntegrationTestHelpers.GetAdminTokenAsync(factory);
        var client = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(client, token);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        var chevrolet = await context.Marca.FirstAsync(m => m.Nombre == "Chevrolet");
        var cruze = await context.Modelo.FirstAsync(m => m.Nombre == "Cruze" && m.MarcaId == chevrolet.Id);
        
        var car = new Domain.Cars.Car(
            chevrolet,
            cruze,
            Color.Black,
            TypeCar.Sedan,
            StatusCar.Used,
            statusServiceCar.Disponible,
            4,
            5,
            1800,
            50000,
            2020,
            "DEF456",
            "Chevrolet Cruze usado",
            15000m,
            DateTime.UtcNow);
        
        context.Cars.Add(car);
        await context.SaveChangesAsync();

        var response = await client.GetAsync($"/cars/{car.Id}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<Application.Cars.GetById.CarGetByIdResponse>();
        result.Should().NotBeNull();
        result!.Id.Should().Be(car.Id);
        result.Marca.Should().Be("Chevrolet");
        result.Modelo.Should().Be("Cruze");
    }

    [Fact]
    public async Task CreateCar_WithInvalidSeededBrand_ShouldReturnBadRequest()
    {
        await using var factory = new CustomWebApplicationFactory();
        var token = await IntegrationTestHelpers.GetAdminTokenAsync(factory);
        var client = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(client, token);

        var request = new
        {
            Marca = Guid.NewGuid().ToString(), // Marca inexistente
            Modelo = Guid.NewGuid().ToString(),
            Color = (int)Color.White,
            CarType = (int)TypeCar.Sedan,
            CarStatus = (int)StatusCar.New,
            ServiceCar = (int)statusServiceCar.Disponible,
            CantidadPuertas = 4,
            CantidadAsientos = 5,
            Cilindrada = 2000,
            Kilometraje = 0,
            Año = 2024,
            Patente = "GHI789",
            Descripcion = "Carro inválido",
            Precio = 20000m
        };

        var response = await client.PostAsJsonAsync("/cars", request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}

