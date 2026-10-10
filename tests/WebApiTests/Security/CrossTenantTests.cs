using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Application.Abstractions.Authentication;
using Domain.Cars;
using Domain.Cars.Attributes;
using Domain.Clients;
using Domain.DealerSettings;
using Domain.Documents;
using Domain.Leads;
using Domain.Sales;
using Domain.Sales.Attributes;
using Domain.Users;
using FluentAssertions;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WebApiTests.Postgres;
using Xunit;

namespace WebApiTests.Security;

[Collection("PostgresCollection")]
[Trait("Category", "Postgres")]
public class CrossTenantTests : IAsyncLifetime
{
    private static readonly Guid Dealer1Id = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Dealer2Id = Guid.Parse("bbbbbbbb-2222-2222-2222-222222222222");

    private readonly PostgresWebApplicationFactory _factory;
    private Guid _dealer1LeadId;
    private Guid _dealer1SaleId;
    private Guid _dealer1CarId;
    private Guid _dealer1CarImageId;
    private Guid _dealer1DocId;

    public CrossTenantTests(PostgresFixture fixture)
    {
        _factory = new PostgresWebApplicationFactory(fixture.GetConnectionString());
    }

    public async Task InitializeAsync()
    {
        await _factory.InitializeDatabaseAsync();

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        // 1. Seed resources for Dealer 1
        var now = DateTime.UtcNow;

        // Lead for Dealer 1
        var lead = Lead.Create(Dealer1Id, "Client One", "c1@test.com", "12345", LeadSource.Web, now);
        context.Leads.Add(lead);
        _dealer1LeadId = lead.Id;

        // Client & Car for Dealer 1
        var client1 = new Client(Dealer1Id, "Dealer1", "Client", Random.Shared.Next(10000000, 99999999).ToString(), $"d1{Guid.NewGuid():N}@test.com", "123", "Addr 1", now);
        context.Clients.Add(client1);

        var marca = new Marca($"Marca{Guid.NewGuid():N}");
        var modelo = new Modelo("Yaris", marca.Id);
        context.Marca.Add(marca);
        context.Modelo.Add(modelo);

        var car1 = new Car(
            Dealer1Id, marca, modelo, Color.Black, TypeCar.Sedan, StatusCar.New,
            StatusServiceCar.Disponible, 4, 5, 1600, 0, 2023, $"{(char)(65 + Random.Shared.Next(26))}{(char)(65 + Random.Shared.Next(26))}{(char)(65 + Random.Shared.Next(26))}{Random.Shared.Next(100, 999)}", "Car 1",
            20000m, now, FuelType.Gasolina, false, Transmission.Manual, null);
        var image1 = CarImage.Create(Guid.NewGuid(), car1.Id, $"{Dealer1Id}/{car1.Id}/img1.jpg", "image/jpeg", 1024, 0, true);
        car1.Images.Add(image1);
        context.Cars.Add(car1);
        context.CarImages.Add(image1);
        _dealer1CarId = car1.Id;
        _dealer1CarImageId = image1.Id;

        // Sale for Dealer 1
        var sale1 = new Sale(Dealer1Id, car1.Id, client1.Id, 20000m, Domain.Financial.Attributes.PaymentMethod.Cash, "CON-001", "Sale 1", now);
        context.Sales.Add(sale1);
        _dealer1SaleId = sale1.Id;

        // Document for Dealer 1
        var doc1 = Document.Create(client1.Id, DocumentType.DNI, "doc1.jpg", "dni.jpg", "image/jpeg", Dealer1Id);
        context.Documents.Add(doc1);
        _dealer1DocId = doc1.Id;

        // 2. Seed Dealer 2 and user
        if (!await context.DealerSettings.AnyAsync(d => d.DealerId == Dealer2Id))
        {
            var ds2 = new DealerSettings(Dealer2Id, "Dealer Two", "dealer2@test.com", hostName: "dealer2.com", slug: "dealer2");
            context.DealerSettings.Add(ds2);
        }

        // spr ADR B8: the subscription gate is fail-closed — a dealer with no (confirmed) subscription row
        // gets 402 before any tenant check runs. Dealer 2 is a paying customer here so these tests keep
        // exercising what they are about: tenant isolation, not billing.
        if (!await context.DealerSubscriptions.IgnoreQueryFilters().AnyAsync(s => s.DealerId == Dealer2Id))
        {
            var sub2 = Domain.Billing.DealerSubscription.Create(Dealer2Id, "cus_dealer2", "sub_dealer2", "price_test");
            sub2.Activate("sub_dealer2", now, now.AddDays(30));
            context.DealerSubscriptions.Add(sub2);
        }

        var dealer2Role = await context.Roles.FirstOrDefaultAsync(r => r.DealerId == Dealer2Id && r.Name == "Admin");
        if (dealer2Role is null)
        {
            dealer2Role = new Role(Dealer2Id, "Admin", "Admin Dealer 2");
            dealer2Role.ReplacePermissions(PermissionCatalog.All.Select(d => d.Value));
            context.Roles.Add(dealer2Role);
        }

        if (!await context.Users.AnyAsync(u => u.Email == "admin2@carstore.com"))
        {
            var user2 = new User(Dealer2Id, "admin2@carstore.com", "Dealer2", "Admin", passwordHasher.Hash("Admin123!"), dealer2Role.Id);
            context.Users.Add(user2);
        }

        await context.SaveChangesAsync();
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    private async Task<HttpClient> CreateDealer2ClientAsync()
    {
        var client = _factory.CreateClient();
        var loginResponse = await client.PostAsJsonAsync(
            "/api/v1/users/login",
            new { Email = "admin2@carstore.com", Password = "Admin123!" },
            IntegrationTestHelpers.JsonOptions);
        loginResponse.EnsureSuccessStatusCode();

        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>(IntegrationTestHelpers.JsonOptions);
        IntegrationTestHelpers.SetAuthToken(client, login!.Token);
        return client;
    }

    private sealed record LoginResponse(string Token);

    [Fact]
    public async Task Dealer2_AccessingDealer1Lead_Returns404()
    {
        var client = await CreateDealer2ClientAsync();

        // GET
        var getRes = await client.GetAsync($"/api/v1/leads/{_dealer1LeadId}");
        getRes.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // PUT notes
        var putRes = await client.PatchAsJsonAsync(
            $"/api/v1/leads/{_dealer1LeadId}/notes",
            new { Notes = "Malicious update" },
            IntegrationTestHelpers.JsonOptions);
        putRes.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // DELETE
        var delRes = await client.DeleteAsync($"/api/v1/leads/{_dealer1LeadId}");
        delRes.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Dealer2_AccessingDealer1Sale_Returns404()
    {
        var client = await CreateDealer2ClientAsync();

        // GET
        var getRes = await client.GetAsync($"/api/v1/sales/{_dealer1SaleId}");
        getRes.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // PUT
        var putRes = await client.PutAsJsonAsync(
            $"/api/v1/sales/{_dealer1SaleId}",
            new { Id = _dealer1SaleId, FinalPrice = 15000m, PaymentMethod = 0, Status = 0, Comments = "Malicious" },
            IntegrationTestHelpers.JsonOptions);
        putRes.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // DELETE
        var delRes = await client.DeleteAsync($"/api/v1/sales/{_dealer1SaleId}");
        delRes.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Dealer2_DeletingDealer1CarImage_Returns404()
    {
        var client = await CreateDealer2ClientAsync();

        var delRes = await client.DeleteAsync($"/api/v1/cars/{_dealer1CarId}/images/{_dealer1CarImageId}");
        delRes.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Dealer2_VerifyingDealer1Document_Returns404()
    {
        var client = await CreateDealer2ClientAsync();

        var res = await client.PostAsync($"/api/v1/documents/{_dealer1DocId}/verify", null);
        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
