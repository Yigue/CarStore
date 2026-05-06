using Application.Cars.GetById;
using Domain.Cars;
using Domain.Cars.Attributes;
using Infrastructure.Database;
using System.Net.Http.Json;
using System.Net;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;

namespace WebApiTests;

public class CarsEndpointsTests
{
    private sealed record CreateResponse(Guid id);

    [Fact]
    public async Task DebugTest_ReturnsOk()
    {
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        var response = await client.GetAsync("/debug-test");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Be("OK");
    }

    [Fact]
    public async Task CreateCar_AddsCarToDatabase()
    {
        await using var factory = new CustomWebApplicationFactory();
        var token = await IntegrationTestHelpers.GetAdminTokenAsync(factory);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var marca = new Marca("Toyota");
        var modelo = new Modelo("Corolla", marca.Id) { Marca = marca };
        context.AddRange(marca, modelo);
        await context.SaveChangesAsync();

        var client = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(client, token);

        var request = new
        {
            Marca = marca.Id.ToString(),
            Modelo = modelo.Id.ToString(),
            Color = (int)Color.White,
            CarType = (int)TypeCar.Sedan,
            CarStatus = (int)StatusCar.New,
            ServiceCar = (int)StatusServiceCar.Disponible,
            CantidadPuertas = 4,
            CantidadAsientos = 5,
            Cilindrada = 2000,
            Kilometraje = 10000,
            Anio = 2020,
            Patente = "ABC123",
            Descripcion = "Test",
            Precio = 10000m
        };

        var response = await client.PostAsJsonAsync("/api/v1/cars", request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<CreateResponse>();
        var id = result!.id;

        context.Cars.IgnoreQueryFilters().Should().ContainSingle(c => c.Id == id);
    }

    [Fact]
    public async Task CreateCar_ReturnsBadRequest_ForInvalidColor()
    {
        await using var factory = new CustomWebApplicationFactory();
        var token = await IntegrationTestHelpers.GetAdminTokenAsync(factory);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var marca = new Marca("Toyota");
        var modelo = new Modelo("Corolla", marca.Id) { Marca = marca };
        context.AddRange(marca, modelo);
        await context.SaveChangesAsync();

        var client = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(client, token);

        var request = new
        {
            Marca = marca.Id.ToString(),
            Modelo = modelo.Id.ToString(),
            Color = 999,
            CarType = (int)TypeCar.Sedan,
            CarStatus = (int)StatusCar.New,
            ServiceCar = (int)StatusServiceCar.Disponible,
            CantidadPuertas = 4,
            CantidadAsientos = 5,
            Cilindrada = 2000,
            Kilometraje = 10000,
            Anio = 2020,
            Patente = "ABC123",
            Descripcion = "Test",
            Precio = 10000m
        };

        var response = await client.PostAsJsonAsync("/api/v1/cars", request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetCarById_ReturnsCar_WhenExists()
    {
        await using var factory = new CustomWebApplicationFactory();
        var token = await IntegrationTestHelpers.GetAdminTokenAsync(factory);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var marca = new Marca("Ford");
        var modelo = new Modelo("Fiesta", marca.Id) { Marca = marca };

        var dealerId = Guid.Parse(CustomWebApplicationFactory.AdminDealerId);
        var car = new Car(dealerId, marca, modelo, Color.Blue, TypeCar.Sedan, StatusCar.New, StatusServiceCar.Disponible, 4,5,1600,5000,2019,"ABC123","desc",15000m, DateTime.UtcNow);
        context.AddRange(marca, modelo, car);
        await context.SaveChangesAsync();

        var client = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(client, token);

        var response = await client.GetAsync($"/api/v1/cars/{car.Id}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<CarGetByIdResponse>();
        result!.Id.Should().Be(car.Id);
        result.Marca.Should().Be("Ford");
    }

    [Fact]
    public async Task GetCarById_ReturnsNotFound_WhenMissing()
    {
        await using var factory = new CustomWebApplicationFactory();
        var token = await IntegrationTestHelpers.GetAdminTokenAsync(factory);

        var client = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(client, token);

        var response = await client.GetAsync($"/api/v1/cars/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
