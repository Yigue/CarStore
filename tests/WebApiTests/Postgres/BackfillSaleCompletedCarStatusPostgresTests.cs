using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Domain.Cars;
using Domain.Cars.Attributes;
using Domain.Sales;
using Domain.Sales.Attributes;
using Domain.Shared.ValueObjects;
using FluentAssertions;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace WebApiTests.Postgres;

/// <summary>
/// qa-p1-integridad PR4, Slice 9 (D5, REQ: sale-completion-inventory-sync).
/// Tests the admin backfill endpoint for setting service_car = Vendido on cars with completed sales.
/// </summary>
[Collection("PostgresCollection")]
[Trait("Category", "Postgres")]
public class BackfillSaleCompletedCarStatusPostgresTests : IAsyncLifetime
{
    private const string AdminDealerId = "11111111-1111-1111-1111-111111111111";

    private readonly PostgresWebApplicationFactory _factory;

    public BackfillSaleCompletedCarStatusPostgresTests(PostgresFixture fixture)
    {
        _factory = new PostgresWebApplicationFactory(fixture.GetConnectionString());
    }

    public async Task InitializeAsync() => await _factory.InitializeDatabaseAsync();

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    private async Task<string> GetAdminTokenAsync()
    {
        var client = _factory.CreateClient();
        var loginRequest = new { Email = "admin@carstore.com", Password = "Admin123!" };

        var loginResponse = await client.PostAsJsonAsync("/api/v1/users/login", loginRequest, IntegrationTestHelpers.JsonOptions);
        loginResponse.EnsureSuccessStatusCode();

        var result = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>(IntegrationTestHelpers.JsonOptions);
        return result!.Token;
    }

    private sealed record LoginResponse(string Token);

    private sealed record BackfillResponse(
        Guid AuditId,
        string Action,
        int AffectedRowCount,
        Guid[] AffectedCarIds);

    [Fact]
    public async Task BackfillSaleCompletedCarStatus_DryRunAndApply_BehavesIdempotentlyAndAudits()
    {
        var token = await GetAdminTokenAsync();
        var client = _factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(client, token);

        var dealerId = Guid.Parse(AdminDealerId);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var marca = new Marca("Marca " + Guid.NewGuid());
        var modelo = new Modelo("Modelo " + Guid.NewGuid(), marca.Id);
        db.Marca.Add(marca);
        db.Modelo.Add(modelo);

        // Car 1: Has a Completed sale, but service_car = Reservado
        var car1 = new Car(
            dealerId, marca, modelo, Color.Blue, TypeCar.Sedan, StatusCar.New,
            StatusServiceCar.Reservado, 4, 5, 2000, 1000, 2023,
            "ABC" + Random.Shared.Next(100, 999), "desc 1", 20000m, DateTime.UtcNow);
        db.Cars.Add(car1);

        // Car 2: Has only a Pending sale, service_car = Reservado
        var car2 = new Car(
            dealerId, marca, modelo, Color.Red, TypeCar.Sedan, StatusCar.New,
            StatusServiceCar.Reservado, 4, 5, 2000, 1000, 2023,
            "XYZ" + Random.Shared.Next(100, 999), "desc 2", 25000m, DateTime.UtcNow);
        db.Cars.Add(car2);

        await db.SaveChangesAsync();

        // Create Completed sale for Car 1
        var client1 = new Domain.Clients.Client(dealerId, "John", "Doe", "12345678", "john@example.com", "12345678", "Address", DateTime.UtcNow);
        db.Clients.Add(client1);
        var sale1 = new Sale(dealerId, car1.Id, client1.Id, 20000m, Domain.Financial.Attributes.PaymentMethod.Cash, "CN-001", "sale 1", DateTime.UtcNow);
        sale1.Complete();
        db.Sales.Add(sale1);

        // Create Pending sale for Car 2
        var client2 = new Domain.Clients.Client(dealerId, "Jane", "Doe", "87654321", "jane@example.com", "87654321", "Address 2", DateTime.UtcNow);
        db.Clients.Add(client2);
        var sale2 = new Sale(dealerId, car2.Id, client2.Id, 25000m, Domain.Financial.Attributes.PaymentMethod.Cash, "CN-002", "sale 2", DateTime.UtcNow);
        db.Sales.Add(sale2);

        await db.SaveChangesAsync();

        int auditCountBefore = await db.BackfillAudits.IgnoreQueryFilters().CountAsync(a => a.DealerId == dealerId);

        // 1. Dry-Run: Reports car1, mutates nothing in DB
        var dryRunResponse = await client.PostAsJsonAsync(
            "/api/v1/admin/backfill/sale-completed-car-status",
            new { dryRun = true, confirmed = false },
            IntegrationTestHelpers.JsonOptions);

        dryRunResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var dryRunResult = await dryRunResponse.Content.ReadFromJsonAsync<BackfillResponse>(IntegrationTestHelpers.JsonOptions);
        dryRunResult.Should().NotBeNull();
        dryRunResult!.Action.Should().Be("DryRun");
        dryRunResult.AffectedRowCount.Should().Be(1);
        dryRunResult.AffectedCarIds.Should().ContainSingle(id => id == car1.Id);

        // Assert Car 1 is still Reservado in DB
        using (var checkScope = _factory.Services.CreateScope())
        {
            var checkDb = checkScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var c1 = await checkDb.Cars.FindAsync(car1.Id);
            c1!.ServiceCar.Should().Be(StatusServiceCar.Reservado, "Dry-run must not mutate car status");
        }

        // 2. Apply without confirmed -> Expect 400 Validation Problem
        var unconfirmedResponse = await client.PostAsJsonAsync(
            "/api/v1/admin/backfill/sale-completed-car-status",
            new { dryRun = false, confirmed = false },
            IntegrationTestHelpers.JsonOptions);

        unconfirmedResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest, "Apply without Confirmed=true must be rejected");

        // 3. Apply with confirmed -> Flips car1 to Vendido, car2 untouched
        var applyResponse = await client.PostAsJsonAsync(
            "/api/v1/admin/backfill/sale-completed-car-status",
            new { dryRun = false, confirmed = true },
            IntegrationTestHelpers.JsonOptions);

        applyResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var applyResult = await applyResponse.Content.ReadFromJsonAsync<BackfillResponse>(IntegrationTestHelpers.JsonOptions);
        applyResult!.Action.Should().Be("Apply");
        applyResult.AffectedRowCount.Should().Be(1);
        applyResult.AffectedCarIds.Should().ContainSingle(id => id == car1.Id);

        using (var checkScope = _factory.Services.CreateScope())
        {
            var checkDb = checkScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var c1 = await checkDb.Cars.FindAsync(car1.Id);
            var c2 = await checkDb.Cars.FindAsync(car2.Id);

            c1!.ServiceCar.Should().Be(StatusServiceCar.Vendido, "Apply must update car with completed sale to Vendido");
            c2!.ServiceCar.Should().Be(StatusServiceCar.Reservado, "Car with only Pending sale must remain untouched");
        }

        // 4. Second Dry-Run -> Reports 0 affected rows
        var dryRun2Response = await client.PostAsJsonAsync(
            "/api/v1/admin/backfill/sale-completed-car-status",
            new { dryRun = true, confirmed = false },
            IntegrationTestHelpers.JsonOptions);

        dryRun2Response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dryRun2Result = await dryRun2Response.Content.ReadFromJsonAsync<BackfillResponse>(IntegrationTestHelpers.JsonOptions);
        dryRun2Result!.Action.Should().Be("DryRun");
        dryRun2Result.AffectedRowCount.Should().Be(0);
        dryRun2Result.AffectedCarIds.Should().BeEmpty();

        // 5. Audit rows appended
        using (var checkScope = _factory.Services.CreateScope())
        {
            var checkDb = checkScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            int auditCountAfter = await checkDb.BackfillAudits.IgnoreQueryFilters().CountAsync(a => a.DealerId == dealerId);
            auditCountAfter.Should().Be(auditCountBefore + 3, "Dry-run 1, Apply, and Dry-run 2 must each append an audit row");
        }
    }
}
