using Application.Financial.GetAll;
using Domain.Cars;
using Domain.Cars.Attributes;
using Domain.Clients;
using Domain.Financial;
using Domain.Financial.Attributes;
using Domain.Sales;
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

/// <summary>
/// Tests de integraciÃ³n para endpoints de Financial usando datos seedeados
/// </summary>
public class FinancialIntegrationTests
{
    private sealed record CreateResponse(Guid id);

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
            .IgnoreQueryFilters()
            .FirstAsync(c => c.Name == "Venta de Auto");
        var request = new
        {
            Type = (int)TransactionType.Income,
            Amount = 25000m,
            Description = "Venta de vehÃ­culo",
            PaymentMethod = (int)PaymentMethod.Cash,
            ReferenceNumber = "REF-2024-001",
            TransactionDate = DateTime.UtcNow,
            CategoryId = category.Id.ToString(),
            CarId = (string?)null,
            ClientId = (string?)null,
            SaleId = (string?)null
        };

        var response = await client.PostAsJsonAsync("/api/v1/financial", request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var result = await response.Content.ReadFromJsonAsync<CreateResponse>();
        var transactionId = result!.id;
        transactionId.Should().NotBe(Guid.Empty);

        var createdTransaction = await context.Transactions
            .IgnoreQueryFilters()
            .Include(t => t.Category)
            .FirstAsync(t => t.Id == transactionId);

        createdTransaction.Type.Should().Be(TransactionType.Income);
        createdTransaction.Amount.Amount.Should().Be(25000m);
        createdTransaction.Category.Name.Should().Be("Venta de Auto");
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

        var toyota = await context.Marca.IgnoreQueryFilters().FirstAsync(m => m.Nombre == "Toyota");     
        var camry = await context.Modelo.IgnoreQueryFilters().FirstAsync(m => m.Nombre == "Camry" && m.MarcaId == toyota.Id);

        var dealerId = Guid.Parse(CustomWebApplicationFactory.AdminDealerId);
        var car = new Car(
            dealerId,
            toyota,
            camry,
            Color.Blue,
            TypeCar.Sedan,
            StatusCar.New,
            StatusServiceCar.Disponible,
            4,
            5,
            2500,
            0,
            2024,
            "ABC123",
            "Toyota Camry nuevo",
            30000m,
            DateTime.UtcNow);

        var category = await context.TransactionCategories
            .IgnoreQueryFilters()
            .FirstAsync(c => c.Name == "Servicio TÃ©cnico");
        context.Cars.Add(car);
        await context.SaveChangesAsync();

        var request = new
        {
            Type = (int)TransactionType.Income,
            Amount = 5000m,
            Description = "Servicio tÃ©cnico de Toyota Camry",
            PaymentMethod = (int)PaymentMethod.CreditCard,
            ReferenceNumber = "SRV-2024-001",
            TransactionDate = DateTime.UtcNow,
            CategoryId = category.Id.ToString(),
            CarId = car.Id.ToString(),
            ClientId = (string?)null,
            SaleId = (string?)null
        };

        var response = await client.PostAsJsonAsync("/api/v1/financial", request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var result = await response.Content.ReadFromJsonAsync<CreateResponse>();
        var transactionId = result!.id;

        var createdTransaction = await context.Transactions
            .IgnoreQueryFilters()
            .Include(t => t.Car)
            .Include(t => t.Category)
            .FirstAsync(t => t.Id == transactionId);

        createdTransaction.CarId.Should().Be(car.Id);
        createdTransaction.Category.Name.Should().Be("Servicio TÃ©cnico");
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
            .IgnoreQueryFilters()
            .FirstAsync(c => c.Name == "Gastos Operativos");

        var transaction = new FinancialTransaction(
            Guid.Parse(CustomWebApplicationFactory.AdminDealerId),
            TransactionType.Expense,
            1500m,
            "Gastos de oficina",
            PaymentMethod.BankTransfer,
            category);

        context.Transactions.Add(transaction);
        await context.SaveChangesAsync();

        var response = await client.GetAsync("/api/v1/financial");
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

        var volkswagen = await context.Marca.IgnoreQueryFilters().FirstAsync(m => m.Nombre == "Volkswagen");
        var polo = await context.Modelo.IgnoreQueryFilters().FirstAsync(m => m.Nombre == "Polo" && m.MarcaId == volkswagen.Id);

        var dealerId = Guid.Parse(CustomWebApplicationFactory.AdminDealerId);
        var car = new Car(
            dealerId,
            volkswagen,
            polo,
            Color.Red,
            TypeCar.Hatchback,
            StatusCar.Used,
            StatusServiceCar.Disponible,
            4,
            5,
            1400,
            40000,
            2020,
            "ABC123",
            "Volkswagen Polo usado",
            16000m,
            DateTime.UtcNow);

        var testClient = new Client(
            dealerId,
            "Andrea",
            "Vargas",
            "88990011",
            "andrea.vargas@example.com",
            "+54 11 6666-5555",
            "Av. Belgrano 1234",
            DateTime.UtcNow);

        context.Cars.Add(car);
        context.Clients.Add(testClient);
        await context.SaveChangesAsync();

        var sale = new Sale(
            Guid.Parse(CustomWebApplicationFactory.AdminDealerId),
            car.Id,
            testClient.Id,
            16000m,
            PaymentMethod.Cash,
            "VTA-2024-004",
            "Venta de Volkswagen Polo",
            DateTime.UtcNow);

        sale.Complete();
        context.Sales.Add(sale);
        await context.SaveChangesAsync();

        var category = await context.TransactionCategories
            .IgnoreQueryFilters()
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

        var response = await client.PostAsJsonAsync("/api/v1/financial", request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var result = await response.Content.ReadFromJsonAsync<CreateResponse>();
        var transactionId = result!.id;

        var createdTransaction = await context.Transactions
            .IgnoreQueryFilters()
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
