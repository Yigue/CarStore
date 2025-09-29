using Application.Sales.Get;
using Domain.Cars;
using Domain.Cars.Atribbutes;
using Domain.Clients;
using Domain.Financial.Attributes;
using Infrastructure.Database;

namespace WebApiTests;

public class SalesEndpointsTests
{
    [Fact]
    public async Task CreateSale_AddsSaleAndUpdatesCar()
    {
        await using var factory = new CustomWebApplicationFactory();
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var marca = new Marca("VW") { Id = Guid.NewGuid() };
        var modelo = new Modelo("Golf", marca.Id) { Id = Guid.NewGuid(), Marca = marca };
        var car = new Car(marca, modelo, Color.Gray, TypeCar.Sedan, StatusCar.New, statusServiceCar.Disponible, 4,5,1600,0,2021,"AAA111","desc",20000m, DateTime.UtcNow);
        var clientEntity = new Client("Alice", "Green", "333", "alice@example.com", "789", "Ave");
        context.AddRange(marca, modelo, car, clientEntity);
        await context.SaveChangesAsync();

        var http = factory.CreateClient();
        var request = new
        {
            CarId = car.Id,
            ClientId = clientEntity.Id,
            FinalPrice = 18000m,
            PaymentMethod = PaymentMethod.Cash,
            Status = "Pending",
            ContractNumber = "C123",
            Comments = "none"
        };
        var response = await http.PostAsJsonAsync("/sales", request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var saleId = await response.Content.ReadFromJsonAsync<Guid>();

        context.Sales.Should().ContainSingle(s => s.Id == saleId);
        context.Cars.Single(c => c.Id == car.Id).ServiceCar.Should().Be(statusServiceCar.Vendido);

        var get = await http.GetAsync($"/sales/{saleId}");
        get.StatusCode.Should().Be(HttpStatusCode.OK);
        var saleResponse = await get.Content.ReadFromJsonAsync<SaleResponse>();
        saleResponse!.Id.Should().Be(saleId);
        saleResponse.CarBrand.Should().Be("VW");
    }

    [Fact]
    public async Task CreateSale_ReturnsBadRequest_WhenCarMissing()
    {
        await using var factory = new CustomWebApplicationFactory();
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var clientEntity = new Client("Bob", "Brown", "444", "bob@example.com", "555", "Street");
        context.Clients.Add(clientEntity);
        await context.SaveChangesAsync();

        var http = factory.CreateClient();
        var request = new
        {
            CarId = Guid.NewGuid(),
            ClientId = clientEntity.Id,
            FinalPrice = 1000m,
            PaymentMethod = PaymentMethod.Cash,
            Status = "Pending",
            ContractNumber = "C1",
            Comments = "none"
        };
        var response = await http.PostAsJsonAsync("/sales", request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetSaleById_ReturnsNotFound_WhenMissing()
    {
        await using var factory = new CustomWebApplicationFactory();
        var http = factory.CreateClient();
        var response = await http.GetAsync($"/sales/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
