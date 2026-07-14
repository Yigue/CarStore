using System.Net;
using System.Security.Cryptography;
using System.Text;
using Application.UnitTests;
using Domain.Webhooks;
using Infrastructure.BackgroundJobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Quartz;

namespace Application.UnitTests.Webhooks;

/// <summary>
/// Covers the outgoing webhook dispatcher: HMAC-SHA256 signature correctness and the
/// success/failure/dead-letter retry state transitions driven by the HTTP response.
/// HTTP is faked via a stub HttpMessageHandler wired into IHttpClientFactory — no real
/// network call is ever made.
/// </summary>
public class DispatchWebhookDeliveriesJobTests
{
    private const string Secret = "0123456789abcdef0123456789abcdef";

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastRequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastRequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return responder(request);
        }
    }

    private TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new TestApplicationDbContext(options);
    }

    private static (DispatchWebhookDeliveriesJob Job, StubHttpMessageHandler Handler) CreateJob(
        TestApplicationDbContext context,
        FakeDateTimeProvider dateTimeProvider,
        Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        var handler = new StubHttpMessageHandler(responder);
        var httpClient = new HttpClient(handler);

        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(DispatchWebhookDeliveriesJob.HttpClientName)).Returns(httpClient);

        var job = new DispatchWebhookDeliveriesJob(
            context,
            factory.Object,
            dateTimeProvider,
            NullLogger<DispatchWebhookDeliveriesJob>.Instance);

        return (job, handler);
    }

    // ─── HMAC signature ────────────────────────────────────────────────────────

    [Fact]
    public void ComputeSignature_ShouldBeDeterministic_ForSamePayloadAndSecret()
    {
        var sig1 = DispatchWebhookDeliveriesJob.ComputeSignature("{\"a\":1}", Secret);
        var sig2 = DispatchWebhookDeliveriesJob.ComputeSignature("{\"a\":1}", Secret);

        sig1.Should().Be(sig2);
    }

    [Fact]
    public void ComputeSignature_ShouldChange_WhenPayloadChanges()
    {
        var sig1 = DispatchWebhookDeliveriesJob.ComputeSignature("{\"a\":1}", Secret);
        var sig2 = DispatchWebhookDeliveriesJob.ComputeSignature("{\"a\":2}", Secret);

        sig1.Should().NotBe(sig2);
    }

    [Fact]
    public void ComputeSignature_ShouldChange_WhenSecretChanges()
    {
        var sig1 = DispatchWebhookDeliveriesJob.ComputeSignature("{\"a\":1}", Secret);
        var sig2 = DispatchWebhookDeliveriesJob.ComputeSignature("{\"a\":1}", "fedcba9876543210fedcba9876543210");

        sig1.Should().NotBe(sig2);
    }

    [Fact]
    public void ComputeSignature_ShouldMatch_IndependentlyComputedHmacSha256Hex()
    {
        const string payload = "{\"event\":\"sale.created\"}";

        byte[] expectedHash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(Secret), Encoding.UTF8.GetBytes(payload));
        string expected = Convert.ToHexString(expectedHash).ToLowerInvariant();

        DispatchWebhookDeliveriesJob.ComputeSignature(payload, Secret).Should().Be(expected);
    }

    // ─── Delivery lifecycle ────────────────────────────────────────────────────

    private WebhookSubscription SeedSubscription(TestApplicationDbContext context, DateTime now)
    {
        var subscription = WebhookSubscription.Create(
            Guid.NewGuid(), "https://example.com/hook", Secret, [WebhookEventCatalog.SaleCreated], now);
        context.WebhookSubscriptions.Add(subscription);
        context.SaveChanges();
        return subscription;
    }

    private WebhookDelivery SeedDelivery(TestApplicationDbContext context, WebhookSubscription subscription, DateTime now)
    {
        var delivery = WebhookDelivery.Create(
            subscription.DealerId, subscription.Id, Guid.NewGuid(),
            WebhookEventCatalog.SaleCreated, "{\"event\":\"sale.created\"}", now);
        context.WebhookDeliveries.Add(delivery);
        context.SaveChanges();
        return delivery;
    }

    [Fact]
    public async Task Execute_ShouldMarkDelivered_AndSignRequest_OnSuccessResponse()
    {
        var now = DateTime.UtcNow;
        var context = CreateContext();
        var dateTimeProvider = new FakeDateTimeProvider { UtcNow = now };
        var subscription = SeedSubscription(context, now);
        var delivery = SeedDelivery(context, subscription, now);

        var (job, handler) = CreateJob(context, dateTimeProvider,
            _ => new HttpResponseMessage(HttpStatusCode.OK));

        await job.Execute(new Mock<IJobExecutionContext>().Object);

        var updated = await context.WebhookDeliveries.FirstAsync(d => d.Id == delivery.Id);
        updated.Status.Should().Be(WebhookDeliveryStatus.Delivered);
        updated.LastStatusCode.Should().Be(200);
        updated.DeliveredAtUtc.Should().Be(now);

        handler.LastRequest.Should().NotBeNull();
        handler.LastRequest!.Headers.GetValues("X-CarStore-Event").Should().ContainSingle().Which.Should().Be(WebhookEventCatalog.SaleCreated);
        handler.LastRequest.Headers.GetValues("X-CarStore-Signature").Should().ContainSingle()
            .Which.Should().Be(DispatchWebhookDeliveriesJob.ComputeSignature(delivery.Payload, subscription.Secret));
        handler.LastRequestBody.Should().Be(delivery.Payload);
    }

    [Fact]
    public async Task Execute_ShouldScheduleRetry_OnNonSuccessResponse()
    {
        var now = DateTime.UtcNow;
        var context = CreateContext();
        var dateTimeProvider = new FakeDateTimeProvider { UtcNow = now };
        var subscription = SeedSubscription(context, now);
        var delivery = SeedDelivery(context, subscription, now);

        var (job, _) = CreateJob(context, dateTimeProvider,
            _ => new HttpResponseMessage(HttpStatusCode.InternalServerError));

        await job.Execute(new Mock<IJobExecutionContext>().Object);

        var updated = await context.WebhookDeliveries.FirstAsync(d => d.Id == delivery.Id);
        updated.Status.Should().Be(WebhookDeliveryStatus.Pending);
        updated.AttemptCount.Should().Be(1);
        updated.LastStatusCode.Should().Be(500);
        updated.NextRetryAtUtc.Should().Be(now.AddMinutes(1));
    }

    [Fact]
    public async Task Execute_ShouldDeadLetter_AfterFifthFailedAttempt()
    {
        var now = DateTime.UtcNow;
        var context = CreateContext();
        var dateTimeProvider = new FakeDateTimeProvider { UtcNow = now };
        var subscription = SeedSubscription(context, now);
        var delivery = SeedDelivery(context, subscription, now);

        for (int i = 0; i < WebhookRetryPolicy.MaxAttempts; i++)
        {
            var (job, _) = CreateJob(context, dateTimeProvider,
                _ => new HttpResponseMessage(HttpStatusCode.InternalServerError));

            // Force it due again regardless of backoff — each iteration simulates the
            // scheduled retry actually becoming due.
            var current = await context.WebhookDeliveries.FirstAsync(d => d.Id == delivery.Id);
            dateTimeProvider.UtcNow = current.NextRetryAtUtc;

            await job.Execute(new Mock<IJobExecutionContext>().Object);
        }

        var final = await context.WebhookDeliveries.FirstAsync(d => d.Id == delivery.Id);
        final.Status.Should().Be(WebhookDeliveryStatus.DeadLettered);
        final.AttemptCount.Should().Be(WebhookRetryPolicy.MaxAttempts);
    }

    [Fact]
    public async Task Execute_ShouldSkip_DeliveriesNotYetDue()
    {
        var now = DateTime.UtcNow;
        var context = CreateContext();
        var dateTimeProvider = new FakeDateTimeProvider { UtcNow = now };
        var subscription = SeedSubscription(context, now);
        var delivery = SeedDelivery(context, subscription, now.AddMinutes(10)); // not due yet

        bool handlerInvoked = false;
        var (job, _) = CreateJob(context, dateTimeProvider, _ =>
        {
            handlerInvoked = true;
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        await job.Execute(new Mock<IJobExecutionContext>().Object);

        handlerInvoked.Should().BeFalse();
        var unchanged = await context.WebhookDeliveries.FirstAsync(d => d.Id == delivery.Id);
        unchanged.Status.Should().Be(WebhookDeliveryStatus.Pending);
        unchanged.AttemptCount.Should().Be(0);
    }
}
