using Application.Quotes.Get;
using Application.Quotes.GetById;
using Domain.Cars;
using Domain.Cars.Attributes;
using Domain.Clients;
using Domain.Quotes;
using Domain.Quotes.Attributes;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;

namespace WebApiTests.IntegrationTests;

/// <summary>
/// Tests de integración para endpoints de Quotes usando datos seedeados
/// </summary>
public class QuotesIntegrationTests
{
    [Fact]
    public async Task CreateQuote_WithSeededData_ShouldSucceed()
    {
        await using var factory = new CustomWebApplicationFactory();
        var token = await IntegrationTestHelpers.GetAdminTokenAsync(factory);
        var client = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(client, token);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var toyota = await context.Marca.IgnoreQueryFilters().FirstAsync(m => m.Nombre == "Toyota");
        var rav4 = await context.Modelo.IgnoreQueryFilters().FirstAsync(m => m.Nombre == "RAV4" && m.MarcaId == toyota.Id);   

        var dealerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var car = new Car(
            dealerId,
            toyota,
            rav4,
            Color.Green,
            TypeCar.SUV,
            StatusCar.New,
            StatusServiceCar.Disponible,
            5,
            5,
            2500,
            0,
            2024,
            "ABC123",
            "Toyota RAV4 nuevo",
            35000m,
            DateTime.UtcNow);        
        var testClient = new Client(
            dealerId,
            "Sofía",
            "Martínez",
            "55667788",
            "sofia.martinez@example.com",
            "+54 11 7777-6666",
            "Av. del Libertador 3456",
            DateTime.UtcNow);
        
        context.Cars.Add(car);
        context.Clients.Add(testClient);
        await context.SaveChangesAsync();

        var validUntil = DateTime.UtcNow.AddDays(30);
        var request = new
        {
            CarId = car.Id.ToString(),
            ClientId = testClient.Id.ToString(),
            ProposedPrice = 34000m,
            ValidUntil = validUntil,
            Comments = "Cotización para Toyota RAV4"
        };

        var response = await client.PostAsJsonAsync("/api/v1/quotes", request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var result = await response.Content.ReadFromJsonAsync<CreateResponse>(IntegrationTestHelpers.JsonOptions);
        var quoteId = result!.id;
        quoteId.Should().NotBe(Guid.Empty);

        var createdQuote = await context.Quotes
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(q => q.Car)
            .Include(q => q.Client)
            .FirstAsync(q => q.Id == quoteId);        
        createdQuote.CarId.Should().Be(car.Id);
        createdQuote.ClientId.Should().Be(testClient.Id);
        createdQuote.ProposedPrice.Amount.Should().Be(34000m);
        createdQuote.Status.Should().Be(QuoteStatus.Pending);
        createdQuote.ValidUntil.Should().BeCloseTo(validUntil, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task GetQuotes_ShouldReturnQuotes()
    {
        await using var factory = new CustomWebApplicationFactory();
        var token = await IntegrationTestHelpers.GetAdminTokenAsync(factory);
        var client = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(client, token);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var ford = await context.Marca.IgnoreQueryFilters().FirstAsync(m => m.Nombre == "Ford");
        var mustang = await context.Modelo.IgnoreQueryFilters().FirstAsync(m => m.Nombre == "Mustang" && m.MarcaId == ford.Id);

        var dealerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var car = new Car(
            dealerId,
            ford,
            mustang,
            Color.Red,
            TypeCar.Coupe,
            StatusCar.New,
            StatusServiceCar.Disponible,
            2,
            4,
            5000,
            0,
            2024,
            "ABC123",
            "Ford Mustang nuevo",
            45000m,
            DateTime.UtcNow);        
        var testClient = new Client(
            dealerId,
            "Diego",
            "Ramírez",
            "22334455",
            "diego.ramirez@example.com",
            "+54 11 5555-4444",
            "Av. Las Heras 4567",
            DateTime.UtcNow);
        
        context.Cars.Add(car);
        context.Clients.Add(testClient);
        await context.SaveChangesAsync();

        var quote = new Domain.Quotes.Quote(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            car,
            testClient,
            null,
            44000m,
            Domain.Quotes.Attributes.PaymentMethod.Contado,
            DateTime.UtcNow.AddDays(15),
            "Cotización para Ford Mustang",
            DateTime.UtcNow);
        
        context.Quotes.Add(quote);
        await context.SaveChangesAsync();

        var response = await client.GetAsync("/api/v1/quotes");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var quotes = await response.Content.ReadFromJsonAsync<List<Application.Quotes.Get.QuoteResponse>>(IntegrationTestHelpers.JsonOptions);
        quotes.Should().NotBeNull();
        quotes!.Count.Should().BeGreaterThan(0);
        quotes.Should().Contain(q => q.Id == quote.Id);
    }

    [Fact]
    public async Task GetQuoteById_ShouldReturnQuote()
    {
        await using var factory = new CustomWebApplicationFactory();
        var token = await IntegrationTestHelpers.GetAdminTokenAsync(factory);
        var client = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(client, token);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        var volkswagen = await context.Marca.IgnoreQueryFilters().FirstAsync(m => m.Nombre == "Volkswagen");
        var tiguan = await context.Modelo.IgnoreQueryFilters().FirstAsync(m => m.Nombre == "Tiguan" && m.MarcaId == volkswagen.Id);

        var dealerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var car = new Car(
            dealerId,
            volkswagen,
            tiguan,
            Color.Black,
            TypeCar.SUV,
            StatusCar.Used,
            StatusServiceCar.Disponible,
            5,
            7,
            2000,
            25000,
            2022,
            "ABC123",
            "Volkswagen Tiguan usado",
            30000m,
            DateTime.UtcNow);
        
        var testClient = new Client(
            dealerId,
            "Laura",
            "Torres",
            "66778899",
            "laura.torres@example.com",
            "+54 11 3333-4444",
            "Av. Callao 6789",
            DateTime.UtcNow);
        
        context.Cars.Add(car);
        context.Clients.Add(testClient);
        await context.SaveChangesAsync();

        var quote = new Domain.Quotes.Quote(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            car,
            testClient,
            null,
            29000m,
            Domain.Quotes.Attributes.PaymentMethod.Financiado,
            DateTime.UtcNow.AddDays(20),
            "Cotización para Volkswagen Tiguan",
            DateTime.UtcNow);
        
        context.Quotes.Add(quote);
        await context.SaveChangesAsync();

        var response = await client.GetAsync($"/api/v1/quotes/{quote.Id}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
                var result = await response.Content.ReadFromJsonAsync<Application.Quotes.Get.QuoteResponse>(IntegrationTestHelpers.JsonOptions);
        result.Should().NotBeNull();
        result!.Id.Should().Be(quote.Id);
        result.ProposedPrice.Should().Be(29000m);
        result.Status.Should().Be(QuoteStatus.Pending.ToString());
        result.CarBrand.Should().Be("Volkswagen");
        result.CarModel.Should().Be("Tiguan");
    }

    [Fact]
    public async Task AcceptQuote_ShouldChangeStatus()
    {
        await using var factory = new CustomWebApplicationFactory();
        var token = await IntegrationTestHelpers.GetAdminTokenAsync(factory);
        var client = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(client, token);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        var chevrolet = await context.Marca.IgnoreQueryFilters().FirstAsync(m => m.Nombre == "Chevrolet");
        var cruze = await context.Modelo.IgnoreQueryFilters().FirstAsync(m => m.Nombre == "Cruze" && m.MarcaId == chevrolet.Id);
        
        var dealerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var car = new Car(
            dealerId,
            chevrolet,
            cruze,
            Color.White,
            TypeCar.SUV,
            StatusCar.New,
            StatusServiceCar.Disponible,
            5,
            5,
            2400,
            0,
            2024,
            "ABC123",
            "Chevrolet Cruze nuevo",
            32000m,
            DateTime.UtcNow);
        
        var testClient = new Client(
            dealerId,
            "Miguel",
            "Sánchez",
            "44556677",
            "miguel.sanchez@example.com",
            "+54 11 8888-7777",
            "Av. Pueyrredón 8901",
            DateTime.UtcNow);
        
        context.Cars.Add(car);
        context.Clients.Add(testClient);
        await context.SaveChangesAsync();

        var quote = new Domain.Quotes.Quote(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            car,
            testClient,
            null,
            31000m,
            Domain.Quotes.Attributes.PaymentMethod.Permuta,
            DateTime.UtcNow.AddDays(25),
            "Cotización para Chevrolet Cruze",
            DateTime.UtcNow);
        
        context.Quotes.Add(quote);
        await context.SaveChangesAsync();

        var response = await client.PostAsync($"/api/v1/quotes/{quote.Id}/accept", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var updatedQuote = await context.Quotes.IgnoreQueryFilters().AsNoTracking().FirstAsync(q => q.Id == quote.Id);
        updatedQuote!.Status.Should().Be(QuoteStatus.Accepted);
    }

    private sealed record CreateResponse(Guid id);
}

