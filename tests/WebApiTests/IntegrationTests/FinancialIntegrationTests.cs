using Application.Financial.GetAll;
using Application.Queries.Financial.GetSummary;
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
/// Tests de integración para endpoints de Financial usando datos seedeados
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
            Description = "Venta de vehículo",
            PaymentMethod = (int)PaymentMethod.Cash,
            ReferenceNumber = "REF-2024-001",
            TransactionDate = DateTime.UtcNow,
            categoryId = category.Id.ToString(),
            CarId = (string?)null,
            ClientId = (string?)null,
            SaleId = (string?)null
        };

        var response = await client.PostAsJsonAsync("/api/v1/financial", request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var result = await response.Content.ReadFromJsonAsync<CreateResponse>(IntegrationTestHelpers.JsonOptions);
        var transactionId = result!.id;
        transactionId.Should().NotBe(Guid.Empty);

        var createdTransaction = await context.Transactions
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(t => t.Category)
            .FirstAsync(t => t.Id == transactionId);

        createdTransaction.Type.Should().Be(TransactionType.Income);
        createdTransaction.Amount.Amount.Should().Be(25000m);
        createdTransaction.Category.Name.Should().Be("Venta de Auto");
    }

    [Fact]
    public async Task CreateFinancial_WithLegacyCategoryField_Returns400()
    {
        // REQ-FIN-FIELD-001: legacy `category` field must be rejected with 400.
        // The endpoint Request DTO field is `categoryId`; clients that still send
        // `category` (a previous FE bug) MUST NOT silently bind to the new field.
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
            Amount = 100m,
            Description = "Legacy payload",
            PaymentMethod = (int)PaymentMethod.Cash,
            ReferenceNumber = "LEG-001",
            TransactionDate = DateTime.UtcNow,
            // Legacy field name — must NOT bind to `categoryId`.
            category = category.Id.ToString(),
            CarId = (string?)null,
            ClientId = (string?)null,
            SaleId = (string?)null
        };

        var response = await client.PostAsJsonAsync("/api/v1/financial", request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateFinancialCategories_WithDescription_Persists()
    {
        // REQ-FIN-FORM-001 (financial/spec.md + entity-cruds/spec.md):
        // TransactionCategory.Description is persisted and returned by GET.
        await using var factory = new CustomWebApplicationFactory();
        var token = await IntegrationTestHelpers.GetAdminTokenAsync(factory);
        var client = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(client, token);

        var newName = $"CAT-{Guid.NewGuid():N}".Substring(0, 16);
        var request = new
        {
            Name = newName,
            Description = "Servicios profesionales a clientes",
            Type = (int)TransactionType.Income,
        };

        var response = await client.PostAsJsonAsync("/api/v1/financial/categories", request, IntegrationTestHelpers.JsonOptions);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Follow-up GET: assert the description echoes back
        var getResponse = await client.GetAsync("/api/v1/financial/categories");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var categories = await getResponse.Content.ReadFromJsonAsync<List<TransactionCategory>>(IntegrationTestHelpers.JsonOptions);
        categories.Should().NotBeNull();
        var created = categories!.FirstOrDefault(c => c.Name == newName);
        created.Should().NotBeNull();
        created!.Description.Should().Be("Servicios profesionales a clientes");
    }

    [Fact]
    public async Task CreateFinancialCategories_WithDescriptionExceeding500Chars_Returns400()
    {
        // REQ-FIN-FORM-001 + entity-cruds/spec.md: server-side MaximumLength(500).
        await using var factory = new CustomWebApplicationFactory();
        var token = await IntegrationTestHelpers.GetAdminTokenAsync(factory);
        var client = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(client, token);

        var newName = $"OVR-{Guid.NewGuid():N}".Substring(0, 12);
        var request = new
        {
            Name = newName,
            Description = new string('x', 501),
            Type = (int)TransactionType.Income,
        };

        var response = await client.PostAsJsonAsync("/api/v1/financial/categories", request, IntegrationTestHelpers.JsonOptions);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
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
        var corolla = await context.Modelo.IgnoreQueryFilters().FirstAsync(m => m.Nombre == "Corolla" && m.MarcaId == toyota.Id);

        var dealerId = Guid.Parse(CustomWebApplicationFactory.AdminDealerId);
        var car = new Car(
            dealerId,
            toyota,
            corolla,
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
            "Toyota Corolla nuevo",
            30000m,
            DateTime.UtcNow);

        var category = await context.TransactionCategories
            .IgnoreQueryFilters()
            .FirstAsync(c => c.Name == "Servicio Técnico");
        context.Cars.Add(car);
        await context.SaveChangesAsync();

        var request = new
        {
            Type = (int)TransactionType.Income,
            Amount = 5000m,
            Description = "Servicio técnico de Toyota Corolla",
            PaymentMethod = (int)PaymentMethod.CreditCard,
            ReferenceNumber = "SRV-2024-001",
            TransactionDate = DateTime.UtcNow,
            categoryId = category.Id.ToString(),
            CarId = car.Id.ToString(),
            ClientId = (string?)null,
            SaleId = (string?)null
        };

        var response = await client.PostAsJsonAsync("/api/v1/financial", request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var result = await response.Content.ReadFromJsonAsync<CreateResponse>(IntegrationTestHelpers.JsonOptions);
        var transactionId = result!.id;

        var createdTransaction = await context.Transactions
            .IgnoreQueryFilters()
            .Include(t => t.Car)
            .Include(t => t.Category)
            .FirstAsync(t => t.Id == transactionId);

        createdTransaction.CarId.Should().Be(car.Id);
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

        var transactions = await response.Content.ReadFromJsonAsync<List<Application.Financial.GetAll.FinancialResponses>>(IntegrationTestHelpers.JsonOptions);
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
            categoryId = category.Id.ToString(),
            CarId = car.Id.ToString(),
            ClientId = testClient.Id.ToString(),
            SaleId = sale.Id.ToString()
        };

        var response = await client.PostAsJsonAsync("/api/v1/financial", request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var result = await response.Content.ReadFromJsonAsync<CreateResponse>(IntegrationTestHelpers.JsonOptions);
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

    [Fact]
    public async Task GET_FinancialSummary_WithFromToQueryParams_ReturnsNarrowedTotals()
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

        var dealerId = Guid.Parse(CustomWebApplicationFactory.AdminDealerId);

        // Add transaction within the window
        var txInWindow = new FinancialTransaction(
            dealerId,
            TransactionType.Income,
            2000m,
            "In window",
            PaymentMethod.Cash,
            category,
            null,
            null,
            null,
            new DateTime(2050, 6, 15, 12, 0, 0, DateTimeKind.Utc));

        // Add transaction outside the window (before)
        var txBeforeWindow = new FinancialTransaction(
            dealerId,
            TransactionType.Income,
            5000m,
            "Before window",
            PaymentMethod.Cash,
            category,
            null,
            null,
            null,
            new DateTime(2050, 5, 20, 12, 0, 0, DateTimeKind.Utc));

        // Add transaction outside the window (after)
        var txAfterWindow = new FinancialTransaction(
            dealerId,
            TransactionType.Expense,
            1000m,
            "After window",
            PaymentMethod.Cash,
            category,
            null,
            null,
            null,
            new DateTime(2050, 7, 5, 12, 0, 0, DateTimeKind.Utc));

        context.Transactions.AddRange(txInWindow, txBeforeWindow, txAfterWindow);
        await context.SaveChangesAsync();

        // Query the endpoint
        var response = await client.GetAsync("/api/v1/financial/summary?from=2050-06-01&to=2050-06-30");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var summary = await response.Content.ReadFromJsonAsync<FinancialSummaryResponse>(IntegrationTestHelpers.JsonOptions);
        summary.Should().NotBeNull();
        summary!.TotalIncome.Should().Be(2000m); // only txInWindow
        summary.TotalExpenses.Should().Be(0m);
        summary.EntryCount.Should().Be(1);
    }

    [Fact]
    public async Task GET_FinancialSummary_WithInvalidDate_Returns400()
    {
        await using var factory = new CustomWebApplicationFactory();
        var token = await IntegrationTestHelpers.GetAdminTokenAsync(factory);
        var client = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(client, token);

        // Query with invalid date
        var response = await client.GetAsync("/api/v1/financial/summary?from=invalid-date");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
