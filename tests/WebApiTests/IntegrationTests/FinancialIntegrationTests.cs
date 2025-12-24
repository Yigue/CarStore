using Application.Financial.GetAll;
using Domain.Cars;
using Domain.Cars.Atribbutes;
using Domain.Clients;
using Domain.Financial;
using Domain.Financial.Attributes;
using Domain.Sales;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;

namespace WebApiTests.IntegrationTests;

/// <summary>
/// Tests de integración para endpoints de Financial usando datos seedeados
/// </summary>
public class FinancialIntegrationTests
{
    [Fact]
    public async Task CreateFinancialTransaction_WithSeededCategory_ShouldSucceed()
    {
        await using var factory = new CustomWebApplicationFactory();
        var token = await IntegrationTestHelpers.GetAdminTokenAsync(factory);
        var client = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(client, token);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        var category = await context.TransactionCategories
            .FirstAsync(c => c.Name == "Venta de Auto");

        var request = new
        {
            Type = (int)TransactionType.Income,
            Amount = 25000m,
            Description = "Venta de vehículo",
            PaymentMethod = (int)PaymentMethod.Cash,
            ReferenceNumber = "REF-2024-001",
            TransactionDate = DateTime.UtcNow,
            CategoryId = category.Id.ToString(),
            CarId = (string?)null,
            ClientId = (string?)null,
            SaleId = (string?)null
        };

        var response = await client.PostAsJsonAsync("/financial", request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var transactionId = await response.Content.ReadFromJsonAsync<Guid>();
        transactionId.Should().NotBe(Guid.Empty);

        var createdTransaction = await context.Transactions
            .Include(t => t.Category)
            .FirstAsync(t => t.Id == transactionId);
        
        createdTransaction.Type.Should().Be(TransactionType.Income);
        createdTransaction.Amount.Amount.Should().Be(25000m);
        createdTransaction.Category.Name.Should().Be("Venta de Auto");
        createdTransaction.Description.Should().Be("Venta de vehículo");
    }

    [Fact]
    public async Task CreateFinancialTransaction_WithSeededCategoryAndCar_ShouldSucceed()
    {
        await using var factory = new CustomWebApplicationFactory();
        var token = await IntegrationTestHelpers.GetAdminTokenAsync(factory);
        var client = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(client, token);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        var toyota = await context.Marca.FirstAsync(m => m.Nombre == "Toyota");
        var camry = await context.Modelo.FirstAsync(m => m.Nombre == "Camry" && m.MarcaId == toyota.Id);
        
        var car = new Car(
            toyota,
            camry,
            Color.Blue,
            TypeCar.Sedan,
            StatusCar.New,
            statusServiceCar.Disponible,
            4,
            5,
            2500,
            0,
            2024,
            "CA1234",
            "Toyota Camry nuevo",
            30000m,
            DateTime.UtcNow);
        
        var category = await context.TransactionCategories
            .FirstAsync(c => c.Name == "Servicio Técnico");
        
        context.Cars.Add(car);
        await context.SaveChangesAsync();

        var request = new
        {
            Type = (int)TransactionType.Income,
            Amount = 5000m,
            Description = "Servicio técnico de Toyota Camry",
            PaymentMethod = (int)PaymentMethod.CreditCard,
            ReferenceNumber = "SRV-2024-001",
            TransactionDate = DateTime.UtcNow,
            CategoryId = category.Id.ToString(),
            CarId = car.Id.ToString(),
            ClientId = (string?)null,
            SaleId = (string?)null
        };

        var response = await client.PostAsJsonAsync("/financial", request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var transactionId = await response.Content.ReadFromJsonAsync<Guid>();
        
        var createdTransaction = await context.Transactions
            .Include(t => t.Car)
            .Include(t => t.Category)
            .FirstAsync(t => t.Id == transactionId);
        
        createdTransaction.CarId.Should().Be(car.Id);
        createdTransaction.Car!.Marca.Nombre.Should().Be("Toyota");
        createdTransaction.Category.Name.Should().Be("Servicio Técnico");
    }

    [Fact]
    public async Task GetFinancialTransactions_ShouldReturnTransactions()
    {
        await using var factory = new CustomWebApplicationFactory();
        var token = await IntegrationTestHelpers.GetAdminTokenAsync(factory);
        var client = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(client, token);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        var category = await context.TransactionCategories
            .FirstAsync(c => c.Name == "Gastos Operativos");

        var transaction = new FinancialTransaction(
            TransactionType.Expense,
            1500m,
            "Gastos de oficina",
            PaymentMethod.BankTransfer,
            category);
        
        context.Transactions.Add(transaction);
        await context.SaveChangesAsync();

        var response = await client.GetAsync("/financial");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var transactions = await response.Content.ReadFromJsonAsync<List<Application.Financial.GetAll.FinancialResponses>>();
        transactions.Should().NotBeNull();
        transactions!.Count.Should().BeGreaterThan(0);
        transactions.Should().Contain(t => t.Id == transaction.Id);
    }

    [Fact]
    public async Task CreateFinancialTransaction_WithAllRelations_ShouldSucceed()
    {
        await using var factory = new CustomWebApplicationFactory();
        var token = await IntegrationTestHelpers.GetAdminTokenAsync(factory);
        var client = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(client, token);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        var volkswagen = await context.Marca.FirstAsync(m => m.Nombre == "Volkswagen");
        var polo = await context.Modelo.FirstAsync(m => m.Nombre == "Polo" && m.MarcaId == volkswagen.Id);
        
        var car = new Car(
            volkswagen,
            polo,
            Color.Red,
            TypeCar.Hatchback,
            StatusCar.Used,
            statusServiceCar.Disponible,
            4,
            5,
            1400,
            40000,
            2020,
            "PO5678",
            "Volkswagen Polo usado",
            16000m,
            DateTime.UtcNow);
        
        var testClient = new Client(
            "Andrea",
            "Vargas",
            "88990011",
            "andrea.vargas@example.com",
            "+54 11 6666-5555",
            "Av. Belgrano 1234");
        
        context.Cars.Add(car);
        context.Clients.Add(testClient);
        await context.SaveChangesAsync();

        var sale = new Sale(
            car.Id,
            testClient.Id,
            16000m,
            PaymentMethod.Cash,
            "VTA-2024-004",
            "Venta de Volkswagen Polo");
        
        sale.Complete();
        context.Sales.Add(sale);
        await context.SaveChangesAsync();

        var category = await context.TransactionCategories
            .FirstAsync(c => c.Name == "Venta de Auto");

        var request = new
        {
            Type = (int)TransactionType.Income,
            Amount = 16000m,
            Description = "Venta de Volkswagen Polo",
            PaymentMethod = (int)PaymentMethod.Cash,
            ReferenceNumber = "VTA-2024-004",
            TransactionDate = DateTime.UtcNow,
            CategoryId = category.Id.ToString(),
            CarId = car.Id.ToString(),
            ClientId = testClient.Id.ToString(),
            SaleId = sale.Id.ToString()
        };

        var response = await client.PostAsJsonAsync("/financial", request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var transactionId = await response.Content.ReadFromJsonAsync<Guid>();
        
        var createdTransaction = await context.Transactions
            .Include(t => t.Car)
            .Include(t => t.Client)
            .Include(t => t.Sale)
            .Include(t => t.Category)
            .FirstAsync(t => t.Id == transactionId);
        
        createdTransaction.CarId.Should().Be(car.Id);
        createdTransaction.ClientId.Should().Be(testClient.Id);
        createdTransaction.SaleId.Should().Be(sale.Id);
        createdTransaction.Category.Name.Should().Be("Venta de Auto");
    }
}

