using Application.Cars.GetById;
using Application.Cars.GetAll;
using Domain.Cars;
using Domain.Cars.Attributes;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace WebApiTests.IntegrationTests;

public class CarsIntegrationTests
{
    private sealed record CreateResponse(Guid id);

    [Fact]
    public async Task CreateCar_WithSeededBrandAndModel_ShouldSucceed()
    {
        await using var factory = new CustomWebApplicationFactory();
        var token = await IntegrationTestHelpers.GetAdminTokenAsync(factory);
        var client = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(client, token);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        var toyota = await context.Marca.IgnoreQueryFilters().FirstAsync(m => m.Nombre == "Toyota");
        var corolla = await context.Modelo.IgnoreQueryFilters().FirstAsync(m => m.Nombre == "Corolla" && m.MarcaId == toyota.Id);

        var request = new
        {
            Marca = toyota.Id.ToString(),
            Modelo = corolla.Id.ToString(),
            Color = (int)Color.Blue,
            CarType = (int)TypeCar.Sedan,
            CarStatus = (int)StatusCar.New,
            ServiceCar = (int)StatusServiceCar.Disponible,
            CantidadPuertas = 4,
            CantidadAsientos = 5,
            Cilindrada = 2000,
            Kilometraje = 0,
            Anio = 2024,
            Patente = "ABC123",
            Descripcion = "Nuevo Toyota Corolla",
            Precio = 25000m
        };

        var response = await client.PostAsJsonAsync("/api/v1/cars", request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var result = await response.Content.ReadFromJsonAsync<CreateResponse>();
        var carId = result!.id;

        var createdCar = await context.Cars
            .IgnoreQueryFilters()
            .Include(c => c.Marca)
            .Include(c => c.Modelo)
            .FirstAsync(c => c.Id == carId);
        
        createdCar.Marca.Nombre.Should().Be("Toyota");
        createdCar.Modelo.Nombre.Should().Be("Corolla");
    }

    [Fact]
    public async Task GetCars_WithSeededData_ShouldReturnCars()
    {
        await using var factory = new CustomWebApplicationFactory();
        var token = await IntegrationTestHelpers.GetAdminTokenAsync(factory);
        var client = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(client, token);

        var response = await client.GetAsync("/api/v1/cars");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var cars = await response.Content.ReadFromJsonAsync<List<CarsResponses>>();
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
        
        var chevrolet = await context.Marca.IgnoreQueryFilters().FirstAsync(m => m.Nombre == "Chevrolet");
        var cruze = await context.Modelo.IgnoreQueryFilters().FirstAsync(m => m.Nombre == "Cruze" && m.MarcaId == chevrolet.Id);
        
        var dealerId = Guid.Parse(CustomWebApplicationFactory.AdminDealerId);
        var car = new Domain.Cars.Car(dealerId, chevrolet, cruze, Color.Black, TypeCar.Sedan, StatusCar.Used, StatusServiceCar.Disponible, 4, 5, 1800, 50000, 2020, "ABC123", "Desc", 15000m, DateTime.UtcNow);
        context.Cars.Add(car);
        await context.SaveChangesAsync();

        var response = await client.GetAsync($"/api/v1/cars/{car.Id}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<Application.Cars.GetById.CarGetByIdResponse>();
        result!.Id.Should().Be(car.Id);
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
            Marca = Guid.NewGuid().ToString(),
            Modelo = Guid.NewGuid().ToString(),
            Color = (int)Color.White,
            CarType = (int)TypeCar.Sedan,
            CarStatus = (int)StatusCar.New,
            ServiceCar = (int)StatusServiceCar.Disponible,
            CantidadPuertas = 4,
            CantidadAsientos = 5,
            Cilindrada = 2000,
            Kilometraje = 0,
            Anio = 2024,
            Patente = "ABC123",
            Descripcion = "Invalid",
            Precio = 20000m
        };

        var response = await client.PostAsJsonAsync("/api/v1/cars", request);
        response.StatusCode.Should().Match(s => s == HttpStatusCode.BadRequest || s == HttpStatusCode.NotFound);
    }
}
