using Application.Sales.Get;
using Application.Sales.GetById;
using Domain.Cars;
using Domain.Cars.Atribbutes;
using Domain.Clients;
using Domain.Financial.Attributes;
using Domain.Sales;
using Domain.Sales.Attributes;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;

namespace WebApiTests.IntegrationTests;

/// <summary>
/// Tests de integración para endpoints de Sales usando datos seedeados
/// </summary>
public class SalesIntegrationTests
{
    [Fact]
    public async Task CreateSale_WithSeededData_ShouldSucceed()
    {
        await using var factory = new CustomWebApplicationFactory();
        var token = await IntegrationTestHelpers.GetAdminTokenAsync(factory);
        var client = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(client, token);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        // Crear carro y cliente usando datos seedeados
        var volkswagen = await context.Marca.FirstAsync(m => m.Nombre == "Volkswagen");
        var gol = await context.Modelo.FirstAsync(m => m.Nombre == "Gol" && m.MarcaId == volkswagen.Id);
        
        var car = new Car(
            volkswagen,
            gol,
            Color.White,
            TypeCar.Hatchback,
            StatusCar.New,
            statusServiceCar.Disponible,
            4,
            5,
            1600,
            0,
            2024,
            "VW1234",
            "Volkswagen Gol nuevo",
            22000m,
            DateTime.UtcNow);
        
        var testClient = new Client(
            "Roberto",
            "Silva",
            "33445566",
            "roberto.silva@example.com",
            "+54 11 2222-1111",
            "Av. Rivadavia 1234");
        
        context.Cars.Add(car);
        context.Clients.Add(testClient);
        await context.SaveChangesAsync();

        var request = new
        {
            CarId = car.Id.ToString(),
            ClientId = testClient.Id.ToString(),
            FinalPrice = 22000m,
            PaymentMethod = (int)PaymentMethod.Cash,
            ContractNumber = "VTA-2024-001",
            Comments = "Venta de Volkswagen Gol"
        };

        var response = await client.PostAsJsonAsync("/sales", request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var saleId = await response.Content.ReadFromJsonAsync<Guid>();
        saleId.Should().NotBe(Guid.Empty);

        var createdSale = await context.Sales
            .Include(s => s.Car)
            .Include(s => s.Client)
            .FirstAsync(s => s.Id == saleId);
        
        createdSale.CarId.Should().Be(car.Id);
        createdSale.ClientId.Should().Be(testClient.Id);
        createdSale.FinalPrice.Amount.Should().Be(22000m);
        createdSale.PaymentMethod.Should().Be(PaymentMethod.Cash);
        createdSale.Status.Should().Be(SaleStatus.Completed);
        
        // Verificar que el carro se marcó como vendido
        var updatedCar = await context.Cars.FindAsync(car.Id);
        updatedCar!.ServiceCar.Should().Be(statusServiceCar.Vendido);
    }

    [Fact]
    public async Task GetSales_ShouldReturnSales()
    {
        await using var factory = new CustomWebApplicationFactory();
        var token = await IntegrationTestHelpers.GetAdminTokenAsync(factory);
        var client = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(client, token);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        var ford = await context.Marca.FirstAsync(m => m.Nombre == "Ford");
        var focus = await context.Modelo.FirstAsync(m => m.Nombre == "Focus" && m.MarcaId == ford.Id);
        
        var car = new Car(
            ford,
            focus,
            Color.Blue,
            TypeCar.Sedan,
            StatusCar.Used,
            statusServiceCar.Disponible,
            4,
            5,
            2000,
            30000,
            2021,
            "FO4567",
            "Ford Focus usado",
            19000m,
            DateTime.UtcNow);
        
        var testClient = new Client(
            "Patricia",
            "López",
            "77889900",
            "patricia.lopez@example.com",
            "+54 11 1111-2222",
            "Av. 9 de Julio 5678");
        
        context.Cars.Add(car);
        context.Clients.Add(testClient);
        await context.SaveChangesAsync();

        var sale = new Domain.Sales.Sale(
            car.Id,
            testClient.Id,
            19000m,
            PaymentMethod.CreditCard,
            "VTA-2024-002",
            "Venta de Ford Focus");
        
        sale.Complete();
        context.Sales.Add(sale);
        await context.SaveChangesAsync();

        var response = await client.GetAsync("/sales");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var sales = await response.Content.ReadFromJsonAsync<List<Application.Sales.Get.SaleResponse>>();
        sales.Should().NotBeNull();
        sales!.Count.Should().BeGreaterThan(0);
        sales.Should().Contain(s => s.ContractNumber == "VTA-2024-002");
    }

    [Fact]
    public async Task GetSaleById_ShouldReturnSale()
    {
        await using var factory = new CustomWebApplicationFactory();
        var token = await IntegrationTestHelpers.GetAdminTokenAsync(factory);
        var client = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(client, token);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        var chevrolet = await context.Marca.FirstAsync(m => m.Nombre == "Chevrolet");
        var malibu = await context.Modelo.FirstAsync(m => m.Nombre == "Malibu" && m.MarcaId == chevrolet.Id);
        
        var car = new Car(
            chevrolet,
            malibu,
            Color.Silver,
            TypeCar.Sedan,
            StatusCar.New,
            statusServiceCar.Disponible,
            4,
            5,
            2500,
            0,
            2024,
            "CH8901",
            "Chevrolet Malibu nuevo",
            28000m,
            DateTime.UtcNow);
        
        var testClient = new Client(
            "Fernando",
            "García",
            "11223344",
            "fernando.garcia@example.com",
            "+54 11 9999-8888",
            "Av. San Martín 2345");
        
        context.Cars.Add(car);
        context.Clients.Add(testClient);
        await context.SaveChangesAsync();

        var sale = new Domain.Sales.Sale(
            car.Id,
            testClient.Id,
            28000m,
            PaymentMethod.BankTransfer,
            "VTA-2024-003",
            "Venta de Chevrolet Malibu");
        
        sale.Complete();
        context.Sales.Add(sale);
        await context.SaveChangesAsync();

        var response = await client.GetAsync($"/sales/{sale.Id}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<Application.Sales.GetById.SaleResponse>();
        result.Should().NotBeNull();
        result!.Id.Should().Be(sale.Id);
        result.FinalPrice.Should().Be(28000m);
        result.ContractNumber.Should().Be("VTA-2024-003");
        result.CarBrand.Should().Be("Chevrolet");
        result.CarModel.Should().Be("Malibu");
    }
}

