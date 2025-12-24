using Application.Quotes.Get;
using Application.Quotes.GetById;
using Domain.Cars;
using Domain.Cars.Atribbutes;
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
        
        var toyota = await context.Marca.FirstAsync(m => m.Nombre == "Toyota");
        var rav4 = await context.Modelo.FirstAsync(m => m.Nombre == "RAV4" && m.MarcaId == toyota.Id);
        
        var car = new Car(
            toyota,
            rav4,
            Color.Green,
            TypeCar.SUV,
            StatusCar.New,
            statusServiceCar.Disponible,
            5,
            5,
            2500,
            0,
            2024,
            "TO5678",
            "Toyota RAV4 nuevo",
            35000m,
            DateTime.UtcNow);
        
        var testClient = new Client(
            "Sofía",
            "Martínez",
            "55667788",
            "sofia.martinez@example.com",
            "+54 11 7777-6666",
            "Av. del Libertador 3456");
        
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

        var response = await client.PostAsJsonAsync("/quotes", request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var quoteId = await response.Content.ReadFromJsonAsync<Guid>();
        quoteId.Should().NotBe(Guid.Empty);

        var createdQuote = await context.Quotes
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
        
        var ford = await context.Marca.FirstAsync(m => m.Nombre == "Ford");
        var mustang = await context.Modelo.FirstAsync(m => m.Nombre == "Mustang" && m.MarcaId == ford.Id);
        
        var car = new Car(
            ford,
            mustang,
            Color.Red,
            TypeCar.Sports,
            StatusCar.New,
            statusServiceCar.Disponible,
            2,
            4,
            5000,
            0,
            2024,
            "MU9012",
            "Ford Mustang nuevo",
            45000m,
            DateTime.UtcNow);
        
        var testClient = new Client(
            "Diego",
            "Ramírez",
            "22334455",
            "diego.ramirez@example.com",
            "+54 11 5555-4444",
            "Av. Las Heras 4567");
        
        context.Cars.Add(car);
        context.Clients.Add(testClient);
        await context.SaveChangesAsync();

        var quote = new Domain.Quotes.Quote(
            car,
            testClient,
            44000m,
            DateTime.UtcNow.AddDays(15),
            "Cotización para Ford Mustang");
        
        context.Quotes.Add(quote);
        await context.SaveChangesAsync();

        var response = await client.GetAsync("/quotes");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var quotes = await response.Content.ReadFromJsonAsync<List<Application.Quotes.Get.QuoteResponse>>();
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
        
        var volkswagen = await context.Marca.FirstAsync(m => m.Nombre == "Volkswagen");
        var tiguan = await context.Modelo.FirstAsync(m => m.Nombre == "Tiguan" && m.MarcaId == volkswagen.Id);
        
        var car = new Car(
            volkswagen,
            tiguan,
            Color.Black,
            TypeCar.SUV,
            StatusCar.Used,
            statusServiceCar.Disponible,
            5,
            7,
            2000,
            25000,
            2022,
            "TI3456",
            "Volkswagen Tiguan usado",
            30000m,
            DateTime.UtcNow);
        
        var testClient = new Client(
            "Laura",
            "Torres",
            "66778899",
            "laura.torres@example.com",
            "+54 11 3333-4444",
            "Av. Callao 6789");
        
        context.Cars.Add(car);
        context.Clients.Add(testClient);
        await context.SaveChangesAsync();

        var quote = new Domain.Quotes.Quote(
            car,
            testClient,
            29000m,
            DateTime.UtcNow.AddDays(20),
            "Cotización para Volkswagen Tiguan");
        
        context.Quotes.Add(quote);
        await context.SaveChangesAsync();

        var response = await client.GetAsync($"/quotes/{quote.Id}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<Application.Quotes.GetById.QuoteResponse>();
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
        
        var chevrolet = await context.Marca.FirstAsync(m => m.Nombre == "Chevrolet");
        var equinox = await context.Modelo.FirstAsync(m => m.Nombre == "Equinox" && m.MarcaId == chevrolet.Id);
        
        var car = new Car(
            chevrolet,
            equinox,
            Color.White,
            TypeCar.SUV,
            StatusCar.New,
            statusServiceCar.Disponible,
            5,
            5,
            2400,
            0,
            2024,
            "EQ7890",
            "Chevrolet Equinox nuevo",
            32000m,
            DateTime.UtcNow);
        
        var testClient = new Client(
            "Miguel",
            "Sánchez",
            "44556677",
            "miguel.sanchez@example.com",
            "+54 11 8888-7777",
            "Av. Pueyrredón 8901");
        
        context.Cars.Add(car);
        context.Clients.Add(testClient);
        await context.SaveChangesAsync();

        var quote = new Domain.Quotes.Quote(
            car,
            testClient,
            31000m,
            DateTime.UtcNow.AddDays(25),
            "Cotización para Chevrolet Equinox");
        
        context.Quotes.Add(quote);
        await context.SaveChangesAsync();

        var response = await client.PostAsync($"/quotes/{quote.Id}/accept", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var updatedQuote = await context.Quotes.FindAsync(quote.Id);
        updatedQuote!.Status.Should().Be(QuoteStatus.Accepted);
    }
}

