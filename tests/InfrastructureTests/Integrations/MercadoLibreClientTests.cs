using System.Net;
using System.Text.Json;
using Application.Abstractions.Integrations;
using Application.Abstractions.Tenancy;
using Domain.Cars.Events;
using Domain.Integrations;
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
/// sfc C1.2 / ADR C25: push connector. Everything here is mocked HTTP against MercadoLibre's publicly
/// documented item-update and OAuth2 refresh-token endpoints — no live call is ever made. Live E2E
/// against a real MercadoLibre seller account remains blocked pending real developer-app credentials
/// (see the apply report).
/// </summary>
public sealed class MercadoLibreClientTests : IDisposable
{
    private const string RefreshToken = "TG-refresh-token-original";
    private const string RotatedRefreshToken = "TG-refresh-token-rotated";
    private const string ExternalItemId = "MLA123456789";

    private readonly SqliteConnection _connection;
    private readonly ApplicationDbContext _db;
    private readonly ICredentialProtector _protector;
    private readonly Guid _dealerId = Guid.NewGuid();
    private readonly Guid _carId = Guid.NewGuid();
    private readonly FakeClock _clock = new(new DateTime(2026, 10, 31, 12, 0, 0, DateTimeKind.Utc));

    public MercadoLibreClientTests()
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

    private async Task<ExternalListing> SeedConnectedDealerWithListingAsync()
    {
        DealerIntegration integration = DealerIntegration.Connect(
            _dealerId, MercadoLibreClient.ProviderKey, _protector.Protect(RefreshToken), scopes: "read write", tokenExpiresAtUtc: null, nowUtc: _clock.UtcNow);
        _db.DealerIntegrations.Add(integration);

        ExternalListing listing = ExternalListing.Create(_dealerId, _carId, MercadoLibreClient.ProviderKey, ExternalItemId, _clock.UtcNow);
        _db.ExternalListings.Add(listing);

        await _db.SaveChangesAsync();
        return listing;
    }

    private (MercadoLibreClient Client, StubHttpMessageHandler Handler) CreateClient(HttpStatusCode itemResponseStatus = HttpStatusCode.OK)
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/oauth/token", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        JsonSerializer.Serialize(new { access_token = "APP_USR-access-token", refresh_token = RotatedRefreshToken, expires_in = 21600 }))
                };
            }

            return new HttpResponseMessage(itemResponseStatus)
            {
                Content = new StringContent(itemResponseStatus == HttpStatusCode.OK
                    ? JsonSerializer.Serialize(new { id = ExternalItemId, status = "active" })
                    : "{\"message\":\"boom\"}")
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

        return (client, handler);
    }

    // ─── Push: PauseListing / CloseListing HTTP shape ──────────────────────────

    [Fact]
    public async Task ExecuteAsync_PauseListingOperation_PutsPausedStatus_AfterOAuthRefresh()
    {
        await SeedConnectedDealerWithListingAsync();
        (MercadoLibreClient client, StubHttpMessageHandler handler) = CreateClient();

        IntegrationResult result = await client.ExecuteAsync(
            new IntegrationRequest(Guid.NewGuid(), _dealerId, MercadoLibreClient.ProviderKey, MercadoLibreClient.PauseListingOperation,
                JsonSerializer.Serialize(new MercadoLibreClient.ListingCommandPayload(_carId, ExternalItemId))),
            CancellationToken.None);

        result.Succeeded.Should().BeTrue();

        handler.Requests.Should().HaveCount(2);
        handler.Requests[0].Method.Should().Be(HttpMethod.Post);
        handler.Requests[0].RequestUri!.AbsolutePath.Should().Be("/oauth/token");

        handler.Requests[1].Method.Should().Be(HttpMethod.Put);
        handler.Requests[1].RequestUri!.AbsolutePath.Should().Be($"/items/{ExternalItemId}");
        handler.RequestBodies[1].Should().Be("{\"status\":\"paused\"}");
        handler.Requests[1].Headers.Authorization!.Scheme.Should().Be("Bearer");
    }

    [Fact]
    public async Task ExecuteAsync_CloseListingOperation_PutsClosedStatus()
    {
        await SeedConnectedDealerWithListingAsync();
        (MercadoLibreClient client, StubHttpMessageHandler handler) = CreateClient();

        IntegrationResult result = await client.ExecuteAsync(
            new IntegrationRequest(Guid.NewGuid(), _dealerId, MercadoLibreClient.ProviderKey, MercadoLibreClient.CloseListingOperation,
                JsonSerializer.Serialize(new MercadoLibreClient.ListingCommandPayload(_carId, ExternalItemId))),
            CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        handler.RequestBodies[1].Should().Be("{\"status\":\"closed\"}");
    }

    [Fact]
    public async Task ExecuteAsync_SuccessfulPush_UpdatesExternalListingStatusAndLastSyncedAt()
    {
        ExternalListing listing = await SeedConnectedDealerWithListingAsync();
        (MercadoLibreClient client, _) = CreateClient();

        await client.ExecuteAsync(
            new IntegrationRequest(Guid.NewGuid(), _dealerId, MercadoLibreClient.ProviderKey, MercadoLibreClient.CloseListingOperation,
                JsonSerializer.Serialize(new MercadoLibreClient.ListingCommandPayload(_carId, ExternalItemId))),
            CancellationToken.None);
        await _db.SaveChangesAsync();

        ExternalListing reloaded = await _db.ExternalListings.IgnoreQueryFilters().SingleAsync(l => l.Id == listing.Id);
        reloaded.Status.Should().Be(ExternalListingStatus.Closed);
        reloaded.LastSyncedAtUtc.Should().Be(_clock.UtcNow);
    }

    [Fact]
    public async Task ExecuteAsync_OAuthTokenRotates_PersistsNewEncryptedRefreshToken()
    {
        await SeedConnectedDealerWithListingAsync();
        (MercadoLibreClient client, _) = CreateClient();

        await client.ExecuteAsync(
            new IntegrationRequest(Guid.NewGuid(), _dealerId, MercadoLibreClient.ProviderKey, MercadoLibreClient.PauseListingOperation,
                JsonSerializer.Serialize(new MercadoLibreClient.ListingCommandPayload(_carId, ExternalItemId))),
            CancellationToken.None);
        await _db.SaveChangesAsync();

        DealerIntegration integration = await _db.DealerIntegrations.IgnoreQueryFilters()
            .SingleAsync(i => i.DealerId == _dealerId && i.Provider == MercadoLibreClient.ProviderKey);
        _protector.Unprotect(integration.EncryptedCredentials!).Should().Be(RotatedRefreshToken);
    }

    [Fact]
    public async Task ExecuteAsync_MercadoLibreApiFails_ReturnsFailure_AndRecordsListingError()
    {
        ExternalListing listing = await SeedConnectedDealerWithListingAsync();
        (MercadoLibreClient client, _) = CreateClient(HttpStatusCode.InternalServerError);

        IntegrationResult result = await client.ExecuteAsync(
            new IntegrationRequest(Guid.NewGuid(), _dealerId, MercadoLibreClient.ProviderKey, MercadoLibreClient.PauseListingOperation,
                JsonSerializer.Serialize(new MercadoLibreClient.ListingCommandPayload(_carId, ExternalItemId))),
            CancellationToken.None);

        result.Succeeded.Should().BeFalse();

        ExternalListing reloaded = await _db.ExternalListings.IgnoreQueryFilters().SingleAsync(l => l.Id == listing.Id);
        reloaded.LastError.Should().Contain("500");
    }

    [Fact]
    public async Task ExecuteAsync_NoConnectedIntegration_ReturnsFailure_WithoutCallingMercadoLibre()
    {
        (MercadoLibreClient client, StubHttpMessageHandler handler) = CreateClient();

        IntegrationResult result = await client.ExecuteAsync(
            new IntegrationRequest(Guid.NewGuid(), _dealerId, MercadoLibreClient.ProviderKey, MercadoLibreClient.PauseListingOperation,
                JsonSerializer.Serialize(new MercadoLibreClient.ListingCommandPayload(_carId, ExternalItemId))),
            CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        handler.Requests.Should().BeEmpty();
    }

    // ─── Trigger → enqueue mapping (CarPriceChanged/CarSold/CarDeleted) ────────

    [Theory]
    [InlineData(nameof(CarPriceChangedDomainEvent), MercadoLibreClient.PauseListingOperation)]
    [InlineData(nameof(CarSoldDomainEvent), MercadoLibreClient.CloseListingOperation)]
    [InlineData(nameof(CarDeleteDomainEvent), MercadoLibreClient.CloseListingOperation)]
    public async Task ListingSyncHandlers_EachTrigger_EnqueuesExpectedOperation(string eventName, string expectedOperation)
    {
        await SeedConnectedDealerWithListingAsync();
        var handlers = new MercadoLibreListingSyncHandlers(_db, _clock);

        switch (eventName)
        {
            case nameof(CarPriceChangedDomainEvent):
                await handlers.Handle(new CarPriceChangedDomainEvent(_carId), CancellationToken.None);
                break;
            case nameof(CarSoldDomainEvent):
                await handlers.Handle(new CarSoldDomainEvent(_carId), CancellationToken.None);
                break;
            case nameof(CarDeleteDomainEvent):
                await handlers.Handle(new CarDeleteDomainEvent(_carId), CancellationToken.None);
                break;
        }

        IntegrationCommand command = await _db.IntegrationCommands.IgnoreQueryFilters()
            .SingleAsync(c => c.DealerId == _dealerId && c.Provider == MercadoLibreClient.ProviderKey);
        command.Operation.Should().Be(expectedOperation);

        var payload = JsonSerializer.Deserialize<MercadoLibreClient.ListingCommandPayload>(command.Payload);
        payload!.ExternalId.Should().Be(ExternalItemId);
        payload.CarId.Should().Be(_carId);
    }

    [Fact]
    public async Task ListingSyncHandlers_CarNeverPublished_DoesNotEnqueueAnything()
    {
        // No ExternalListing seeded for this car — never published to MercadoLibre.
        var handlers = new MercadoLibreListingSyncHandlers(_db, _clock);

        await handlers.Handle(new CarSoldDomainEvent(_carId), CancellationToken.None);

        (await _db.IntegrationCommands.IgnoreQueryFilters().AnyAsync()).Should().BeFalse();
    }

    // ─── Test doubles ───────────────────────────────────────────────────────────

    private sealed class FakeClock(DateTime utcNow) : IDateTimeProvider
    {
        public DateTime UtcNow { get; } = utcNow;
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];
        public List<string?> RequestBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            RequestBodies.Add(request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken));
            return responder(request);
        }
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
