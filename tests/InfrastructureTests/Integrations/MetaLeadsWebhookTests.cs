using Application.Abstractions.Integrations;
using Application.Abstractions.Tenancy;
using Domain.Integrations;
using Domain.Leads;
using FluentAssertions;
using Infrastructure.Database;
using Infrastructure.Integrations;
using Infrastructure.Integrations.Meta;
using MediatR;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SharedKernel;
using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace InfrastructureTests.Integrations;

/// <summary>
/// sfc C1.4 / B13: Meta Leads Ads webhook. Everything here is mocked/direct-payload — no live call to a real Meta
/// Business/Ads account. Live E2E against a real Meta app remains blocked pending real app credentials.
/// </summary>
public sealed class MetaLeadsWebhookTests : IDisposable
{
    private const string AppSecret = "meta-app-secret-for-tests";
    private const string PageId = "page-1234567890";
    private const string PageAccessToken = "EAAG-fake-page-access-token";

    private readonly SqliteConnection _connection;
    private readonly ApplicationDbContext _db;
    private readonly ICredentialProtector _protector;
    private readonly Guid _dealerId = Guid.NewGuid();
    private readonly FakeClock _clock = new(new DateTime(2026, 10, 31, 12, 0, 0, DateTimeKind.Utc));

    public MetaLeadsWebhookTests()
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

    private async Task<DealerIntegration> SeedConnectedMetaIntegrationAsync()
    {
        DealerIntegration integration = DealerIntegration.Connect(
            _dealerId,
            MetaLeadsWebhookHandler.Provider,
            _protector.Protect(PageAccessToken),
            scopes: PageId,
            tokenExpiresAtUtc: null,
            nowUtc: _clock.UtcNow);

        _db.DealerIntegrations.Add(integration);
        await _db.SaveChangesAsync();
        return integration;
    }

    private MetaLeadsWebhookHandler CreateHandler(IMetaGraphClient graphClient) =>
        new(_db, _protector, graphClient, _clock, NullLogger.Instance);

    [Fact]
    public async Task HandleLeadgenChange_FacebookLead_CreatesExactlyOneLead_WithSourceAndAttribution()
    {
        await SeedConnectedMetaIntegrationAsync();
        var graphClient = new FakeMetaGraphClient(new MetaLeadFieldData(
            Platform: "fb",
            AdId: "ad-1",
            AdsetId: "adset-1",
            CampaignId: "camp-1",
            CampaignName: "Primavera 2026",
            FormId: "form-1",
            FullName: "Juana Perez",
            Email: "juana.perez@example.com",
            Phone: "+54 11 5555-0001"));

        MetaLeadOutcome outcome = await CreateHandler(graphClient)
            .HandleLeadgenChangeAsync(PageId, "leadgen-abc-1", CancellationToken.None);

        outcome.Should().Be(MetaLeadOutcome.Created);

        Lead[] leads = await _db.Leads.ToArrayAsync();
        leads.Should().HaveCount(1);

        Lead lead = leads[0];
        lead.DealerId.Should().Be(_dealerId);
        lead.Email.Value.Should().Be("juana.perez@example.com");
        lead.Source.Should().Be(LeadSource.Facebook);
        lead.Attribution.Should().NotBeNull();
        lead.Attribution!.UtmSource.Should().Be("facebook");
        lead.Attribution.UtmMedium.Should().Be("lead-ad");
        lead.Attribution.UtmCampaign.Should().Be("Primavera 2026");
        lead.Attribution.UtmContent.Should().Be("ad-1");
        lead.Attribution.UtmTerm.Should().Be("adset-1");
        lead.Attribution.Referrer.Should().Be("meta-form:form-1");
    }

    [Fact]
    public async Task HandleLeadgenChange_InstagramPlatform_SetsInstagramSource()
    {
        await SeedConnectedMetaIntegrationAsync();
        var graphClient = new FakeMetaGraphClient(new MetaLeadFieldData(
            Platform: "ig",
            AdId: "ad-2",
            AdsetId: "adset-2",
            CampaignId: "camp-2",
            CampaignName: null,
            FormId: "form-2",
            FullName: "Carlos Diaz",
            Email: "carlos.diaz@example.com",
            Phone: null));

        MetaLeadOutcome outcome = await CreateHandler(graphClient)
            .HandleLeadgenChangeAsync(PageId, "leadgen-abc-2", CancellationToken.None);

        outcome.Should().Be(MetaLeadOutcome.Created);

        Lead lead = await _db.Leads.SingleAsync();
        lead.Source.Should().Be(LeadSource.Instagram);
        lead.Attribution!.UtmSource.Should().Be("instagram");
        // CampaignName was null — falls back to CampaignId, same as CreateInquiryCommandHandler-style attribution mapping.
        lead.Attribution.UtmCampaign.Should().Be("camp-2");
    }

    [Fact]
    public async Task HandleLeadgenChange_RetriedWebhook_SameLeadgenId_DoesNotCreateDuplicateLead()
    {
        await SeedConnectedMetaIntegrationAsync();
        var graphClient = new FakeMetaGraphClient(new MetaLeadFieldData(
            "fb", "ad-3", "adset-3", "camp-3", "Otono 2026", "form-3",
            "Marta Lopez", "marta.lopez@example.com", "+54 11 5555-0003"));
        var handler = CreateHandler(graphClient);

        MetaLeadOutcome first = await handler.HandleLeadgenChangeAsync(PageId, "leadgen-retry-1", CancellationToken.None);
        MetaLeadOutcome second = await handler.HandleLeadgenChangeAsync(PageId, "leadgen-retry-1", CancellationToken.None);

        first.Should().Be(MetaLeadOutcome.Created);
        second.Should().Be(MetaLeadOutcome.Duplicate);

        (await _db.Leads.CountAsync()).Should().Be(1);
        graphClient.CallCount.Should().Be(1, "a duplicate leadgen_id must short-circuit before the Graph API call");
    }

    [Fact]
    public async Task HandleLeadgenChange_NoConnectedIntegrationForPage_CreatesNoLead()
    {
        // No DealerIntegration seeded for this page at all.
        var graphClient = new FakeMetaGraphClient(new MetaLeadFieldData(
            "fb", "ad-4", "adset-4", "camp-4", "X", "form-4", "Nadie", "nadie@example.com", null));

        MetaLeadOutcome outcome = await CreateHandler(graphClient)
            .HandleLeadgenChangeAsync("unknown-page", "leadgen-unknown-1", CancellationToken.None);

        outcome.Should().Be(MetaLeadOutcome.DealerNotConnected);
        (await _db.Leads.CountAsync()).Should().Be(0);
        graphClient.CallCount.Should().Be(0);
    }

    [Fact]
    public void Webhook_InvalidSignature_IsRejected()
    {
        // The endpoint rejects (401) before ever constructing MetaLeadsWebhookHandler or touching the database —
        // MetaSignatureVerifier is the gate that guarantees that. No live HTTP pipeline is stood up here; this is
        // the same boundary StripeWebhookEndpoint relies on for its own signature check.
        byte[] payload = Encoding.UTF8.GetBytes("""{"object":"page","entry":[]}""");

        bool validWithTamperedSecret = MetaSignatureVerifier.IsValid(payload, "sha256=deadbeef", AppSecret);
        bool validWithMissingHeader = MetaSignatureVerifier.IsValid(payload, null, AppSecret);
        bool validWithWrongPrefix = MetaSignatureVerifier.IsValid(payload, "sha1=deadbeef", AppSecret);

        validWithTamperedSecret.Should().BeFalse();
        validWithMissingHeader.Should().BeFalse();
        validWithWrongPrefix.Should().BeFalse();
    }

    [Fact]
    public void Webhook_ValidSignature_ComputedOverExactBody_IsAccepted()
    {
        byte[] payload = Encoding.UTF8.GetBytes("""{"object":"page","entry":[]}""");
        byte[] key = Encoding.UTF8.GetBytes(AppSecret);
        byte[] hmac = System.Security.Cryptography.HMACSHA256.HashData(key, payload);
        string signatureHeader = "sha256=" + Convert.ToHexString(hmac).ToLowerInvariant();

        MetaSignatureVerifier.IsValid(payload, signatureHeader, AppSecret).Should().BeTrue();

        // Any tamper of the body invalidates the same signature.
        byte[] tampered = Encoding.UTF8.GetBytes("""{"object":"page","entry":[{}]}""");
        MetaSignatureVerifier.IsValid(tampered, signatureHeader, AppSecret).Should().BeFalse();
    }

    private sealed class FakeMetaGraphClient : IMetaGraphClient
    {
        private readonly MetaLeadFieldData _result;

        public FakeMetaGraphClient(MetaLeadFieldData result) => _result = result;

        public int CallCount { get; private set; }

        public Task<MetaLeadFieldData?> GetLeadAsync(string leadgenId, string pageAccessToken, CancellationToken cancellationToken)
        {
            CallCount++;
            pageAccessToken.Should().Be(PageAccessToken, "the handler must decrypt the stored credential before calling Graph API");
            return Task.FromResult<MetaLeadFieldData?>(_result);
        }
    }

    private sealed class FakeClock : IDateTimeProvider
    {
        public FakeClock(DateTime utcNow) => UtcNow = utcNow;
        public DateTime UtcNow { get; }
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
