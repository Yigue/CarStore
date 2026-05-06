using Application.Sales.Get;
using Domain.Cars;
using Domain.Cars.Attributes;
using Domain.Clients;
using Domain.Financial.Attributes;
using Domain.Sales;
using Infrastructure.Database;
using System.Net.Http.Json;
using System.Net;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;

namespace WebApiTests;

public class SalesEndpointsTests
{
    private sealed record CreateResponse(Guid id);

    [Fact]
    public async Task CreateSale_AddsSaleAndUpdatesCar()
    {
        await using var factory = new CustomWebApplicationFactory();
        var token = await IntegrationTestHelpers.GetAdminTokenAsync(factory);
        
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var marca = new Marca("VW");
        var modelo = new Modelo("Golf", marca.Id) { Marca = marca };
        var dealerId = Guid.Parse(CustomWebApplicationFactory.AdminDealerId);
        var car = new Car(dealerId, marca, modelo, Color.Gray, TypeCar.Sedan, StatusCar.New, StatusServiceCar.Disponible, 4,5,1600,0,2021,"ABC123","desc",20000m, DateTime.UtcNow);
        var clientEntity = new Client(dealerId, "Alice", "Green", "333", "alice@example.com", "789", "Ave", DateTime.UtcNow);
        context.AddRange(marca, modelo, car, clientEntity);
        await context.SaveChangesAsync();

        var http = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(http, token);
        
        var request = new
        {
            CarId = car.Id,
            ClientId = clientEntity.Id,
            FinalPrice = 18000m,
            PaymentMethod = (int)PaymentMethod.Cash,
            ContractNumber = "C123",
            Description = "none"
        };
        var response = await http.PostAsJsonAsync("/api/v1/sales", request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<CreateResponse>();
        var saleId = result!.id;

        context.Sales.IgnoreQueryFilters().Should().ContainSingle(s => s.Id == saleId);
        var updatedCar = await context.Cars.IgnoreQueryFilters().FirstAsync(c => c.Id == car.Id);
        updatedCar.ServiceCar.Should().Be(StatusServiceCar.Vendido);

        var get = await http.GetAsync($"/api/v1/sales/{saleId}");
        get.StatusCode.Should().Be(HttpStatusCode.OK);
        var saleResponse = await get.Content.ReadFromJsonAsync<SaleResponse>();
        saleResponse!.Id.Should().Be(saleId);
        saleResponse.CarBrand.Should().Be("VW");
    }

    [Fact]
    public async Task CreateSale_ReturnsBadRequest_WhenCarMissing()
    {
        await using var factory = new CustomWebApplicationFactory();
        var token = await IntegrationTestHelpers.GetAdminTokenAsync(factory);
        
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var dealerId = Guid.Parse(CustomWebApplicationFactory.AdminDealerId);
        var clientEntity = new Client(dealerId, "Bob", "Brown", "444", "bob@example.com", "555", "Street", DateTime.UtcNow);
        context.Clients.Add(clientEntity);
        await context.SaveChangesAsync();

        var http = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(http, token);
        
        var request = new
        {
            CarId = Guid.NewGuid(),
            ClientId = clientEntity.Id,
            FinalPrice = 1000m,
            PaymentMethod = (int)PaymentMethod.Cash,
            ContractNumber = "C1",
            Description = "none"
        };
        var response = await http.PostAsJsonAsync("/api/v1/sales", request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetSaleById_ReturnsNotFound_WhenMissing()
    {
        await using var factory = new CustomWebApplicationFactory();
        var token = await IntegrationTestHelpers.GetAdminTokenAsync(factory);
        
        var http = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(http, token);
        
        var response = await http.GetAsync($"/api/v1/sales/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
