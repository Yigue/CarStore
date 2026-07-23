using Domain.Cars;
using Domain.Cars.Attributes;
using Domain.Clients;
using Domain.Leads;
using Domain.Quotes;
using Domain.Quotes.Attributes;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using WebApiTests.IntegrationTests;

namespace WebApiTests.IntegrationTests.Quotes;

public class QuotesJsonShapeIntegrationTests
{
    /// <summary>
    /// Generates a valid Argentine license plate: AAA + 3 digits (e.g. TST001).
    /// </summary>
    private static string UniquePlate(string prefix = "TST")
    {
        var suffix = new Random().Next(100, 999);
        return $"{prefix}{suffix}";
    }

    [Fact]
    public async Task GetQuotes_Returns_Status_As_String()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var token = await IntegrationTestHelpers.GetAdminTokenAsync(factory);
        var client = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(client, token);

        // Act
        var response = await client.GetAsync("/api/v1/quotes");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var jsonArray = await response.Content.ReadFromJsonAsync<JsonElement>(IntegrationTestHelpers.JsonOptions);
        jsonArray.ValueKind.Should().Be(JsonValueKind.Array);

        // This test only checks the list endpoint returns OK and correct type for seeded quotes.
        // Shape-specific tests use GetById below.
    }

    [Fact]
    public async Task GetQuoteById_LeadQuote_ClientIdIsNull_LeadIdIsPopulated()
    {
        // RED: current QuoteResponse has no LeadId property and ClientId is Guid (non-nullable → Guid.Empty for leads)
        // Arrange
        await using var factory = new CustomWebApplicationFactory();

        Guid quoteId;
        Guid leadId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Database.EnsureCreated();

            var dealerId = Guid.Parse(CustomWebApplicationFactory.AdminDealerId);

            var marca = new Marca("LeadBrand");
            var modelo = new Modelo("LeadModel", marca.Id);
            marca.Modelos.Add(modelo);
            db.Marca.Add(marca);
            db.Modelo.Add(modelo);
            await db.SaveChangesAsync();

            var car = new Car(
                dealerId, marca, modelo,
                Color.White, TypeCar.Sedan,
                StatusCar.New, StatusServiceCar.Disponible,
                4, 5, 2000, 0, 2024,
                UniquePlate("LDX"),
                "Lead car",
                15000m,
                DateTime.UtcNow);
            db.Cars.Add(car);

            var lead = Lead.Create(dealerId, "Test Lead", "lead.quote@test.com", "1112223344", LeadSource.Web, DateTime.UtcNow);
            db.Leads.Add(lead);
            await db.SaveChangesAsync();
            leadId = lead.Id;

            var quote = new Quote(
                dealerId, car, client: null, lead: lead,
                proposedPrice: 14000m,
                paymentMethod: PaymentMethod.Contado,
                validUntil: DateTime.UtcNow.AddDays(30),
                comments: "Lead quote test",
                date: DateTime.UtcNow);
            db.Quotes.Add(quote);
            await db.SaveChangesAsync();
            quoteId = quote.Id;
        }

        var token = await IntegrationTestHelpers.GetAdminTokenAsync(factory);
        var httpClient = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(httpClient, token);

        // Act
        var response = await httpClient.GetAsync($"/api/v1/quotes/{quoteId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Use case-SENSITIVE default options
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        json.TryGetProperty("status", out var statusProp).Should().BeTrue("'status' key must exist");
        statusProp.GetString().Should().Be("Pending");

        // leadId must be non-null and equal to the seeded lead's id
        json.TryGetProperty("leadId", out var leadIdProp).Should().BeTrue("'leadId' key must exist in response");
        leadIdProp.ValueKind.Should().NotBe(JsonValueKind.Null, "leadId must be non-null for a lead-quote");
        leadIdProp.GetGuid().Should().Be(leadId);

        // clientId must be null (not Guid.Empty)
        json.TryGetProperty("clientId", out var clientIdProp).Should().BeTrue("'clientId' key must exist in response");
        clientIdProp.ValueKind.Should().Be(JsonValueKind.Null, "clientId must be null for a lead-quote");

        // REQ-QT-XREF-001: originLeadId/convertedClientId must always be present
        // in the shape; both null here since the lead was never converted.
        json.TryGetProperty("originLeadId", out var originLeadIdProp).Should().BeTrue("'originLeadId' key must exist in response");
        originLeadIdProp.ValueKind.Should().Be(JsonValueKind.Null, "originLeadId must be null when there is no correlation");

        json.TryGetProperty("convertedClientId", out var convertedClientIdProp).Should().BeTrue("'convertedClientId' key must exist in response");
        convertedClientIdProp.ValueKind.Should().Be(JsonValueKind.Null, "convertedClientId must be null when the lead was never converted");
    }

    [Fact]
    public async Task GetQuoteById_ClientQuote_LeadIdIsNull_ClientIdIsPopulated()
    {
        // RED: current QuoteResponse has no LeadId; adding it and confirming ClientId is non-null for client-quotes
        // Arrange
        await using var factory = new CustomWebApplicationFactory();

        Guid quoteId;
        Guid clientId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Database.EnsureCreated();

            var dealerId = Guid.Parse(CustomWebApplicationFactory.AdminDealerId);

            var marca = new Marca("ClientBrand");
            var modelo = new Modelo("ClientModel", marca.Id);
            marca.Modelos.Add(modelo);
            db.Marca.Add(marca);
            db.Modelo.Add(modelo);
            await db.SaveChangesAsync();

            var car = new Car(
                dealerId, marca, modelo,
                Color.Black, TypeCar.Sedan,
                StatusCar.New, StatusServiceCar.Disponible,
                4, 5, 2000, 0, 2024,
                UniquePlate("CLX"),
                "Client car",
                18000m,
                DateTime.UtcNow);
            db.Cars.Add(car);

            var testClient = new Client(
                dealerId, "Client", "Shape",
                "40000001", "client.shape@test.com", "9998887766",
                "Av. Test 999", DateTime.UtcNow);
            db.Clients.Add(testClient);
            await db.SaveChangesAsync();
            clientId = testClient.Id;

            var quote = new Quote(
                dealerId, car, client: testClient, lead: null,
                proposedPrice: 17000m,
                paymentMethod: PaymentMethod.Financiado,
                validUntil: DateTime.UtcNow.AddDays(30),
                comments: "Client quote test",
                date: DateTime.UtcNow);
            db.Quotes.Add(quote);
            await db.SaveChangesAsync();
            quoteId = quote.Id;
        }

        var token = await IntegrationTestHelpers.GetAdminTokenAsync(factory);
        var httpClient = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(httpClient, token);

        // Act
        var response = await httpClient.GetAsync($"/api/v1/quotes/{quoteId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        // clientId must be non-null and equal to the seeded client's id
        json.TryGetProperty("clientId", out var clientIdProp).Should().BeTrue("'clientId' key must exist");
        clientIdProp.ValueKind.Should().NotBe(JsonValueKind.Null, "clientId must be non-null for a client-quote");
        clientIdProp.GetGuid().Should().Be(clientId);

        // leadId must be null
        json.TryGetProperty("leadId", out var leadIdProp).Should().BeTrue("'leadId' key must exist in response");
        leadIdProp.ValueKind.Should().Be(JsonValueKind.Null, "leadId must be null for a client-quote");

        // REQ-QT-XREF-001: both xrefs null since this client has no OriginLeadId.
        json.TryGetProperty("originLeadId", out var originLeadIdProp).Should().BeTrue("'originLeadId' key must exist in response");
        originLeadIdProp.ValueKind.Should().Be(JsonValueKind.Null, "originLeadId must be null when the client has no OriginLeadId");

        json.TryGetProperty("convertedClientId", out var convertedClientIdProp).Should().BeTrue("'convertedClientId' key must exist in response");
        convertedClientIdProp.ValueKind.Should().Be(JsonValueKind.Null, "convertedClientId must be null for a client-quote");
    }

    [Fact]
    public async Task GetQuoteById_ClientLinkedToLead_ProjectsOriginLeadId()
    {
        // REQ-QT-XREF-001: a Client-linked quote whose Client has an OriginLeadId
        // must project that id through the OriginLeadId xref field end-to-end.
        await using var factory = new CustomWebApplicationFactory();

        Guid quoteId;
        Guid originLeadId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Database.EnsureCreated();

            var dealerId = Guid.Parse(CustomWebApplicationFactory.AdminDealerId);

            var marca = new Marca("XrefBrand");
            var modelo = new Modelo("XrefModel", marca.Id);
            marca.Modelos.Add(modelo);
            db.Marca.Add(marca);
            db.Modelo.Add(modelo);
            await db.SaveChangesAsync();

            var car = new Car(
                dealerId, marca, modelo,
                Color.Blue, TypeCar.Sedan,
                StatusCar.New, StatusServiceCar.Disponible,
                4, 5, 2000, 0, 2024,
                UniquePlate("XRF"),
                "Xref car",
                16000m,
                DateTime.UtcNow);
            db.Cars.Add(car);

            var originLead = Lead.Create(dealerId, "Origin Lead", "origin.xref@test.com", "1112223300", LeadSource.Web, DateTime.UtcNow);
            db.Leads.Add(originLead);
            await db.SaveChangesAsync();
            originLeadId = originLead.Id;

            var testClient = new Client(
                dealerId, "Xref", "Client",
                "40000099", "xref.client@test.com", "9998887700",
                "Av. Xref 1", DateTime.UtcNow, originLeadId: originLead.Id);
            db.Clients.Add(testClient);
            await db.SaveChangesAsync();

            var quote = new Quote(
                dealerId, car, client: testClient, lead: null,
                proposedPrice: 16500m,
                paymentMethod: PaymentMethod.Contado,
                validUntil: DateTime.UtcNow.AddDays(30),
                comments: "Xref quote test",
                date: DateTime.UtcNow);
            db.Quotes.Add(quote);
            await db.SaveChangesAsync();
            quoteId = quote.Id;
        }

        var token = await IntegrationTestHelpers.GetAdminTokenAsync(factory);
        var httpClient = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(httpClient, token);

        var response = await httpClient.GetAsync($"/api/v1/quotes/{quoteId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        json.TryGetProperty("originLeadId", out var originLeadIdProp).Should().BeTrue("'originLeadId' key must exist in response");
        originLeadIdProp.ValueKind.Should().NotBe(JsonValueKind.Null, "originLeadId must be populated when the linked client has one");
        originLeadIdProp.GetGuid().Should().Be(originLeadId);
    }
}
