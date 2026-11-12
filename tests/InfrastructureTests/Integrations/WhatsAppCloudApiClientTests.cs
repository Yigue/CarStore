using System.Net;
using System.Text.Json;
using Application.Abstractions.Integrations;
using FluentAssertions;
using Infrastructure.Integrations.WhatsApp;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SharedKernel;
using Xunit;

namespace InfrastructureTests.Integrations;

/// <summary>
/// sfc C0.4 / RN-046: outbound WhatsApp sends must respect an opted-out contact and Meta's 24h
/// customer-service window. HTTP is faked via a stub <see cref="HttpMessageHandler"/> wired into a
/// mocked <see cref="IHttpClientFactory"/> — no real network call is made, and this connector has
/// never been exercised against a real WhatsApp Business Account (see the apply report).
/// </summary>
public class WhatsAppCloudApiClientTests
{
    private static readonly Guid DealerId = Guid.NewGuid();
    private const string PhoneNumberId = "1234567890";
    private const string AccessToken = "EAAB-test-token";
    private const string ToPhone = "5491122334455";

    private sealed class FakeClock : IDateTimeProvider
    {
        public DateTime UtcNow { get; set; }
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastRequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastRequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return responder(request);
        }
    }

    private sealed class FakeContactStateStore : IWhatsAppContactStateStore
    {
        public DateTime? LastInboundAtUtc;
        public bool OptedOut;

        public Task<DateTime?> GetLastInboundAtUtcAsync(Guid dealerId, string phoneE164, CancellationToken cancellationToken) =>
            Task.FromResult(LastInboundAtUtc);

        public Task SetLastInboundAtUtcAsync(Guid dealerId, string phoneE164, DateTime atUtc, CancellationToken cancellationToken)
        {
            LastInboundAtUtc = atUtc;
            return Task.CompletedTask;
        }

        public Task<bool> IsOptedOutAsync(Guid dealerId, string phoneE164, CancellationToken cancellationToken) =>
            Task.FromResult(OptedOut);

        public Task SetOptedOutAsync(Guid dealerId, string phoneE164, bool optedOut, CancellationToken cancellationToken)
        {
            OptedOut = optedOut;
            return Task.CompletedTask;
        }
    }

    private static (WhatsAppCloudApiClient Client, StubHttpMessageHandler Handler, FakeContactStateStore Store) CreateClient(
        FakeClock clock, bool connected = true, HttpStatusCode responseStatus = HttpStatusCode.OK)
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(responseStatus)
        {
            Content = new StringContent("{\"messages\":[{\"id\":\"wamid.test\"}]}"),
        });
        var httpClient = new HttpClient(handler);

        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(WhatsAppCloudApiClient.HttpClientName)).Returns(httpClient);

        var credentialResolver = new Mock<IWhatsAppCredentialResolver>();
        credentialResolver
            .Setup(r => r.ResolveAsync(DealerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(connected ? new WhatsAppCredential(AccessToken, PhoneNumberId, "waba-1") : null);

        var store = new FakeContactStateStore();

        var client = new WhatsAppCloudApiClient(
            credentialResolver.Object, store, factory.Object, clock, NullLogger<WhatsAppCloudApiClient>.Instance);

        return (client, handler, store);
    }

    private static IntegrationRequest BuildRequest(WhatsAppOutboundPayload payload) =>
        new(Guid.NewGuid(), DealerId, "whatsapp", "send_message", JsonSerializer.Serialize(payload));

    [Fact]
    public async Task ExecuteAsync_Should_NeverCallHttp_WhenContactOptedOut()
    {
        var clock = new FakeClock { UtcNow = DateTime.UtcNow };
        (WhatsAppCloudApiClient client, StubHttpMessageHandler handler, FakeContactStateStore store) = CreateClient(clock);
        store.OptedOut = true;

        IntegrationResult result = await client.ExecuteAsync(
            BuildRequest(new WhatsAppOutboundPayload(ToPhone, "Hola!", null, null, null)),
            CancellationToken.None);

        result.Succeeded.Should().BeTrue("an opted-out contact is a deliberate no-op, not a provider failure");
        handler.LastRequest.Should().BeNull("the mocked HTTP client must never be called for an opted-out contact");
    }

    [Fact]
    public async Task ExecuteAsync_Should_SendFreeFormText_WithinThe24HourWindow()
    {
        DateTime now = DateTime.UtcNow;
        var clock = new FakeClock { UtcNow = now };
        (WhatsAppCloudApiClient client, StubHttpMessageHandler handler, FakeContactStateStore store) = CreateClient(clock);
        store.LastInboundAtUtc = now.AddHours(-2);

        IntegrationResult result = await client.ExecuteAsync(
            BuildRequest(new WhatsAppOutboundPayload(ToPhone, "Hola! ¿Cómo estás?", null, null, null)),
            CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        handler.LastRequest.Should().NotBeNull();
        handler.LastRequestBody.Should().Contain("\"type\":\"text\"");
    }

    [Fact]
    public async Task ExecuteAsync_Should_SendTemplate_OutsideThe24HourWindow()
    {
        DateTime now = DateTime.UtcNow;
        var clock = new FakeClock { UtcNow = now };
        (WhatsAppCloudApiClient client, StubHttpMessageHandler handler, FakeContactStateStore store) = CreateClient(clock);
        store.LastInboundAtUtc = now.AddHours(-30); // outside the 24h window

        IntegrationResult result = await client.ExecuteAsync(
            BuildRequest(new WhatsAppOutboundPayload(ToPhone, "Hola! ¿Cómo estás?", "seguimiento_lead", "es_AR", new[] { "Juan" })),
            CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        handler.LastRequestBody.Should().Contain("\"type\":\"template\"");
        handler.LastRequestBody.Should().Contain("seguimiento_lead");
    }

    [Fact]
    public async Task ExecuteAsync_Should_UseTemplate_WhenThereIsNoPriorInboundMessageAtAll()
    {
        var clock = new FakeClock { UtcNow = DateTime.UtcNow };
        (WhatsAppCloudApiClient client, StubHttpMessageHandler handler, FakeContactStateStore _) = CreateClient(clock);

        IntegrationResult result = await client.ExecuteAsync(
            BuildRequest(new WhatsAppOutboundPayload(ToPhone, null, "seguimiento_lead", "es_AR", null)),
            CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        handler.LastRequestBody.Should().Contain("\"type\":\"template\"");
    }

    [Fact]
    public async Task ExecuteAsync_Should_Fail_OutsideWindow_WithNoTemplateProvided()
    {
        var clock = new FakeClock { UtcNow = DateTime.UtcNow };
        (WhatsAppCloudApiClient client, StubHttpMessageHandler handler, FakeContactStateStore _) = CreateClient(clock);

        IntegrationResult result = await client.ExecuteAsync(
            BuildRequest(new WhatsAppOutboundPayload(ToPhone, "Hola!", null, null, null)),
            CancellationToken.None);

        result.Succeeded.Should().BeFalse("outside the window Meta only allows templates");
        handler.LastRequest.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_Should_Fail_WhenDealerIsNotConnected()
    {
        var clock = new FakeClock { UtcNow = DateTime.UtcNow };
        (WhatsAppCloudApiClient client, StubHttpMessageHandler handler, FakeContactStateStore _) = CreateClient(clock, connected: false);

        IntegrationResult result = await client.ExecuteAsync(
            BuildRequest(new WhatsAppOutboundPayload(ToPhone, null, "seguimiento_lead", "es_AR", null)),
            CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        handler.LastRequest.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_Should_Fail_WhenMetaReturnsAnErrorStatus()
    {
        var clock = new FakeClock { UtcNow = DateTime.UtcNow };
        (WhatsAppCloudApiClient client, StubHttpMessageHandler handler, FakeContactStateStore store) = CreateClient(
            clock, responseStatus: HttpStatusCode.BadRequest);
        store.LastInboundAtUtc = clock.UtcNow.AddHours(-1);

        IntegrationResult result = await client.ExecuteAsync(
            BuildRequest(new WhatsAppOutboundPayload(ToPhone, "Hola!", null, null, null)),
            CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        handler.LastRequest.Should().NotBeNull();
    }
}
