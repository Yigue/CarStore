using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Domain.Cars;
using Domain.Cars.Attributes;
using Domain.Clients;
using Domain.Quotes;
using FluentAssertions;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using PaymentMethod = Domain.Quotes.Attributes.PaymentMethod;

namespace WebApiTests.Postgres;

/// <summary>
/// AcceptQuoteCommandHandler guards exclusivity with a check-then-act AnyAsync before saving.
/// Two requests close enough together both pass that check, and one of them hits
/// `ux_quotes_one_accepted_per_car` at SaveChangesAsync — an unhandled DbUpdateException, which
/// falls through to a raw 500. The loser of a SEQUENTIAL acceptance already got the intended 409
/// (QuoteErrors.CarAlreadyCommitted) from the app-level check; the loser of a RACE deserves the
/// exact same answer, not a different one depending on timing. An InMemory-provider test cannot
/// reproduce this: the unique index only exists in real Postgres.
/// </summary>
[Collection("PostgresCollection")]
[Trait("Category", "Postgres")]
public class AcceptQuoteRacePostgresTests : IAsyncLifetime
{
    private readonly PostgresWebApplicationFactory _factory;

    public AcceptQuoteRacePostgresTests(PostgresFixture fixture)
    {
        _factory = new PostgresWebApplicationFactory(fixture.GetConnectionString());
    }

    public async Task InitializeAsync() => await _factory.InitializeDatabaseAsync();
    public async Task DisposeAsync() => await _factory.DisposeAsync();

    private sealed record LoginResponse(string Token);

    private async Task<HttpClient> AdminClientAsync()
    {
        var client = _factory.CreateClient();
        var loginResponse = await client.PostAsJsonAsync(
            "/api/v1/users/login",
            new { Email = "admin@carstore.com", Password = "Admin123!" },
            IntegrationTestHelpers.JsonOptions);
        loginResponse.EnsureSuccessStatusCode();
        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>(IntegrationTestHelpers.JsonOptions);
        IntegrationTestHelpers.SetAuthToken(client, login!.Token);
        return client;
    }

    [Fact]
    public async Task AcceptQuote_Concurrently_LoserGetsConflict_NeverAServerFailure()
    {
        var dealerId = Guid.Parse(CustomWebApplicationFactory.AdminDealerId);
        Guid firstQuoteId, secondQuoteId;

        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var marca = new Marca("RaceMarca");
            var modelo = new Modelo("RaceModelo", marca.Id);
            var car = new Car(dealerId, marca, modelo, Color.Red, TypeCar.Sedan, StatusCar.New,
                StatusServiceCar.Disponible, 4, 5, 1600, 1000, 2024, "RAC001", "race car",
                15000m, DateTime.UtcNow);

            var clientA = new Client(dealerId, "Race", "ClientA", "10000001", "race.a@test.com", "111", "Addr", DateTime.UtcNow);
            var clientB = new Client(dealerId, "Race", "ClientB", "10000002", "race.b@test.com", "222", "Addr", DateTime.UtcNow);

            var quoteA = new Quote(dealerId, car, clientA, null, 15000m, PaymentMethod.Contado,
                DateTime.UtcNow.AddDays(7), "race quote A", DateTime.UtcNow);
            var quoteB = new Quote(dealerId, car, clientB, null, 15000m, PaymentMethod.Contado,
                DateTime.UtcNow.AddDays(7), "race quote B", DateTime.UtcNow);

            context.Marca.Add(marca);
            context.Modelo.Add(modelo);
            context.Cars.Add(car);
            context.Clients.AddRange(clientA, clientB);
            context.Quotes.AddRange(quoteA, quoteB);
            await context.SaveChangesAsync();

            firstQuoteId = quoteA.Id;
            secondQuoteId = quoteB.Id;
        }

        var clientOne = await AdminClientAsync();
        var clientTwo = await AdminClientAsync();

        var taskOne = clientOne.PostAsync($"/api/v1/quotes/{firstQuoteId}/accept", null);
        var taskTwo = clientTwo.PostAsync($"/api/v1/quotes/{secondQuoteId}/accept", null);
        var responses = await Task.WhenAll(taskOne, taskTwo);

        var statusCodes = responses.Select(r => r.StatusCode).ToList();

        statusCodes.Should().NotContain(HttpStatusCode.InternalServerError,
            "the loser of a race must get the same Conflict a sequential loser already gets, never a raw 500");
        statusCodes.Should().ContainSingle(c => c == HttpStatusCode.OK, "exactly one acceptance must win");
        statusCodes.Should().Contain(HttpStatusCode.Conflict, "the other must be refused, not silently dropped");
    }
}
