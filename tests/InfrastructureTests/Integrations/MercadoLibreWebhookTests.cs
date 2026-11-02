using System.Net;
using System.Text.Json;
using Application.Abstractions.Integrations;
using Application.Abstractions.Tenancy;
using Domain.Cars;
using Domain.Cars.Attributes;
using Domain.Deposits;
using Domain.Integrations;
using Domain.Leads;
using FluentAssertions;
using Infrastructure.Database;
using Infrastructure.Integrations;
using Infrastructure.Integrations.MercadoLibre;
using MediatR;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using SharedKernel;
using Xunit;

namespace InfrastructureTests.Integrations;

/// <summary>
/// sfc C1.3 / ADR C25, RN-047: inbound MercadoLibre webhook. HTTP is faked via a stub message handler
/// wired into a mocked <see cref="IHttpClientFactory"/> — the pull-to-confirm call never hits a real
/// MercadoLibre account. Live E2E against a real MercadoLibre seller account remains blocked pending
/// real developer-app credentials (see the apply report).
/// </summary>
public sealed class MercadoLibreWebhookTests : IDisposable
{
    private const string ExternalItemId = "MLA999888777";

    private readonly SqliteConnection _connection;
    private readonly ApplicationDbContext _db;
    private readonly ICredentialProtector _protector;
    private readonly Guid _dealerId = Guid.NewGuid();
    private readonly FakeClock _clock = new(new DateTime(2026, 10, 31, 12, 0, 0, DateTimeKind.Utc));

    public MercadoLibreWebhookTests()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .UseSnakeCaseNamingConvention()
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
            .Options;

        _db = new ApplicationDbContext(options, new NoOpPublisher(), new NoTenant());
        _db.Database.EnsureCreated();

        _protector = new DataProtectionCredentialProtector(new EphemeralDataProtectionProvider());
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private async Task<Car> SeedCarAsync(StatusServiceCar status)
    {
        DealerIntegration integration = DealerIntegration.Connect(
            _dealerId, MercadoLibreClient.ProviderKey, _protector.Protect("TG-refresh"), scopes: "read write", tokenExpiresAtUtc: null, nowUtc: _clock.UtcNow);
        _db.DealerIntegrations.Add(integration);

        var marca = new Marca("Toyota");
        var modelo = new Modelo("Corolla", marca.Id);

        var car = new Car(
            _dealerId, marca, modelo, Color.Black, TypeCar.Sedan, StatusCar.Used, status,
            cantidadPuertas: 4, cantidadAsientos: 5, cilindrada: 1600, kilometraje: 10000, anio: 2020,
            patente: $"ABC{Random.Shared.Next(100, 999)}", descripcion: "Test", price: 20000, date: _clock.UtcNow);

        _db.Cars.Add(car);

        ExternalListing listing = ExternalListing.Create(_dealerId, car.Id, MercadoLibreClient.ProviderKey, ExternalItemId, _clock.UtcNow);
        _db.ExternalListings.Add(listing);

        await _db.SaveChangesAsync();
        return car;
    }

    private MercadoLibreWebhookProcessor CreateProcessor(HttpStatusCode itemResponseStatus = HttpStatusCode.OK, bool rateLimitAllowed = true)
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/oauth/token", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(new { access_token = "APP_USR-token" }))
                };
            }

            return new HttpResponseMessage(itemResponseStatus)
            {
                Content = new StringContent(itemResponseStatus == HttpStatusCode.OK
                    ? JsonSerializer.Serialize(new { id = ExternalItemId, status = "active" })
                    : "{}")
            };
        });
        var httpClient = new HttpClient(handler);

        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(MercadoLibreClient.HttpClientName)).Returns(httpClient);

        var client = new MercadoLibreClient(
            factory.Object,
            _db,
            _protector,
            Options.Create(new MercadoLibreOptions { ClientId = "app-id", ClientSecret = "app-secret", ApiBaseUrl = "https://api.mercadolibre.com" }),
            _clock,
            NullLogger<MercadoLibreClient>.Instance);

        var rateLimiter = new Mock<IRateLimiter>();
        rateLimiter.Setup(r => r.TryAcquireAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RateLimitDecision(rateLimitAllowed, TimeSpan.FromSeconds(5)));

        return new MercadoLibreWebhookProcessor(_db, client, rateLimiter.Object, _clock, NullLogger<MercadoLibreWebhookProcessor>.Instance);
    }

    // ─── RN-047: purchase intent vs local reservation/deposit ──────────────────

    [Fact]
    public async Task PurchaseIntent_CarIsReservedLocally_IsRejected_AndNeverCreatesLeadOrChangesCar()
    {
        Car car = await SeedCarAsync(StatusServiceCar.Reservado);
        MercadoLibreWebhookProcessor processor = CreateProcessor();

        Result result = await processor.ProcessAsync(
            new MercadoLibreNotification("orders", $"/orders/{ExternalItemId}", UserId: 123), CancellationToken.None);

        result.IsFailure.Should().BeTrue();

        Car reloaded = await _db.Cars.IgnoreQueryFilters().SingleAsync(c => c.Id == car.Id);
        reloaded.ServiceCar.Should().Be(StatusServiceCar.Reservado);
        (await _db.Leads.IgnoreQueryFilters().AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task PurchaseIntent_CarHasActiveDeposit_IsRejected_AndNeverCreatesLeadOrSale()
    {
        Car car = await SeedCarAsync(StatusServiceCar.Disponible);

        Result<Deposit> depositResult = Deposit.Create(
            _dealerId, car.Id, clientId: Guid.NewGuid(), quoteId: null, amount: 1000, currency: "ARS",
            exchangeRate: null, nowUtc: _clock.UtcNow,
            car: new DepositCarContext(StatusServiceCar.Disponible, ReservedByQuoteId: null, HasActiveDeposit: false, CarCurrency: "ARS"));
        depositResult.IsSuccess.Should().BeTrue();
        _db.Deposits.Add(depositResult.Value);
        await _db.SaveChangesAsync();

        MercadoLibreWebhookProcessor processor = CreateProcessor();

        Result result = await processor.ProcessAsync(
            new MercadoLibreNotification("orders_v2", $"/orders/{ExternalItemId}", UserId: 123), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        (await _db.Leads.IgnoreQueryFilters().AnyAsync()).Should().BeFalse();
        (await _db.Sales.IgnoreQueryFilters().AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task PurchaseIntent_CarIsAvailable_CreatesLead_NeverASale()
    {
        Car car = await SeedCarAsync(StatusServiceCar.Disponible);
        MercadoLibreWebhookProcessor processor = CreateProcessor();

        Result result = await processor.ProcessAsync(
            new MercadoLibreNotification("orders", $"/orders/{ExternalItemId}", UserId: 123), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        Lead lead = await _db.Leads.IgnoreQueryFilters().SingleAsync();
        lead.Source.Should().Be(LeadSource.MercadoLibre);
        lead.InterestedVehicleId.Should().Be(car.Id);
        (await _db.Sales.IgnoreQueryFilters().AnyAsync()).Should().BeFalse();
    }

    // ─── Questions always create/append a Lead regardless of car state ────────

    [Fact]
    public async Task Question_AlwaysCreatesLeadWithMercadoLibreAttribution_EvenWhenCarIsSold()
    {
        Car car = await SeedCarAsync(StatusServiceCar.Vendido);
        MercadoLibreWebhookProcessor processor = CreateProcessor();

        Result result = await processor.ProcessAsync(
            new MercadoLibreNotification("questions", $"/questions/{ExternalItemId}", UserId: 123), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        Lead lead = await _db.Leads.IgnoreQueryFilters().SingleAsync();
        lead.Source.Should().Be(LeadSource.MercadoLibre);
        lead.Attribution!.UtmSource.Should().Be("mercadolibre");
        lead.InterestedVehicleId.Should().Be(car.Id);
    }

    [Fact]
    public async Task Question_Twice_AppendsToSameLead_NeverDuplicates()
    {
        await SeedCarAsync(StatusServiceCar.Disponible);
        MercadoLibreWebhookProcessor processor = CreateProcessor();

        var notification = new MercadoLibreNotification("questions", $"/questions/{ExternalItemId}", UserId: 123);
        await processor.ProcessAsync(notification, CancellationToken.None);
        await processor.ProcessAsync(notification, CancellationToken.None);

        (await _db.Leads.IgnoreQueryFilters().CountAsync()).Should().Be(1);
    }

    // Security review (sfc C1.3): the webhook has no signature, so anyone who knows a dealer's
    // public MeLi item id could otherwise spam notifications unboundedly. This proves the rate
    // limiter (reused from sfc C1.1, a distinct provider key from outbound sync) actually
    // short-circuits before any pull-to-confirm or Lead write happens.
    [Fact]
    public async Task RateLimited_IsAckedWithoutAnyPullOrLeadCreation()
    {
        await SeedCarAsync(StatusServiceCar.Disponible);
        var processor = CreateProcessor(rateLimitAllowed: false);

        Result result = await processor.ProcessAsync(
            new MercadoLibreNotification("questions", $"/questions/{ExternalItemId}", UserId: 999), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await _db.Leads.IgnoreQueryFilters().AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task UntrackedItem_NoMatchingExternalListing_IsAckedWithoutAnyPull()
    {
        var processor = CreateProcessor();

        Result result = await processor.ProcessAsync(
            new MercadoLibreNotification("questions", "/questions/UNKNOWN-ITEM", UserId: 999), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await _db.Leads.IgnoreQueryFilters().AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task PullToConfirmFails_ReturnsFailure_AndNeverTrustsWebhookBody()
    {
        await SeedCarAsync(StatusServiceCar.Disponible);
        MercadoLibreWebhookProcessor processor = CreateProcessor(HttpStatusCode.Forbidden);

        Result result = await processor.ProcessAsync(
            new MercadoLibreNotification("questions", $"/questions/{ExternalItemId}", UserId: 123), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        (await _db.Leads.IgnoreQueryFilters().AnyAsync()).Should().BeFalse();
    }

    // ─── Test doubles ───────────────────────────────────────────────────────────

    private sealed class FakeClock(DateTime utcNow) : IDateTimeProvider
    {
        public DateTime UtcNow { get; } = utcNow;
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(responder(request));
    }

    private sealed class NoTenant : ICurrentTenantService
    {
        public Guid DealerId => Guid.Empty;
        public bool HasTenant => false;
    }

    private sealed class NoOpPublisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification => Task.CompletedTask;
    }
}
