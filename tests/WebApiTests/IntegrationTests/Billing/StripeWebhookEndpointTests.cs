using Application.Abstractions.Data;
using FluentAssertions;
using Infrastructure.Database;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Stripe;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using WebApiTests;
using Xunit;

namespace WebApiTests.IntegrationTests.Billing;

public class StripeWebhookEndpointTests
{
    private const string TestWebhookSecret = "whsec_test_secret_key_for_webhook_signature_verification_123";

    private HttpClient CreateTestClient(CustomWebApplicationFactory factory)
    {
        return factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "Stripe:SecretKey", "sk_test_mock" },
                    { "Stripe:WebhookSecret", TestWebhookSecret },
                    { "Stripe:PriceId", "price_mock" }
                });
            });
        }).CreateClient();
    }

    /// <summary>
    /// Builds a realistic Stripe event JSON that the SDK can parse.
    /// The SDK requires api_version, created, livemode, pending_webhooks, request fields.
    /// </summary>
    private static string BuildStripeEventJson(string eventId, string eventType, string dataObjectJson)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return $$"""
        {
            "id": "{{eventId}}",
            "object": "event",
            "api_version": "2024-06-20",
            "created": {{now}},
            "livemode": false,
            "pending_webhooks": 1,
            "request": {
                "id": "req_test",
                "idempotency_key": null
            },
            "type": "{{eventType}}",
            "data": {
                "object": {{dataObjectJson}}
            }
        }
        """;
    }

    [Fact]
    public async Task Webhook_WithValidSignature_Returns200AndSavesToOutbox()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await dbContext.Database.EnsureCreatedAsync();
        }

        var client = CreateTestClient(factory);

        var subscriptionObject = """
        {
            "id": "sub_123",
            "object": "subscription",
            "customer": "cus_123",
            "status": "active",
            "current_period_start": 1700000000,
            "current_period_end": 1701209600,
            "items": {
                "object": "list",
                "data": [
                    {
                        "id": "si_123",
                        "object": "subscription_item",
                        "price": {
                            "id": "price_123",
                            "object": "price"
                        }
                    }
                ]
            }
        }
        """;

        var payload = BuildStripeEventJson("evt_test_123", "customer.subscription.created", subscriptionObject);

        var signature = CreateSignature(payload, TestWebhookSecret);
        var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/stripe");
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
        request.Headers.Add("Stripe-Signature", signature);

        // Act
        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"Failed with status {response.StatusCode} and body {body}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var assertScope = factory.Services.CreateScope();
        var assertDbContext = assertScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var outboxMsg = await assertDbContext.OutboxMessages
            .Where(m => m.Type == "StripeRaw")
            .FirstOrDefaultAsync();

        outboxMsg.Should().NotBeNull();
        outboxMsg!.Content.Should().Contain("evt_test_123");
    }

    [Fact]
    public async Task Webhook_WithInvalidSignature_Returns400AndDoesNotSaveToOutbox()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await dbContext.Database.EnsureCreatedAsync();
        }

        var client = CreateTestClient(factory);

        var payload = @"{""id"": ""evt_test_invalid""}";
        var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/stripe");
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
        request.Headers.Add("Stripe-Signature", "t=123,v1=invalid_signature");

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var assertScope = factory.Services.CreateScope();
        var assertDbContext = assertScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var outboxMsg = await assertDbContext.OutboxMessages
            .Where(m => m.Type == "StripeRaw" && m.Content.Contains("evt_test_invalid"))
            .FirstOrDefaultAsync();

        outboxMsg.Should().BeNull();
    }

    [Fact]
    public async Task Webhook_WithValidSignatureButMalformedJsonBody_Returns400WithoutStackTrace()
    {
        // qa-p1-integridad PR8, Slice 16 (D8, REQ: api-error-contract "Forged Stripe Webhook
        // Signature Is Rejected Deliberately" — the sibling defect this PR fixes). A signature
        // computed over a genuinely malformed JSON body passes signature verification, so
        // EventUtility.ConstructEvent throws while parsing — today that lands in the generic
        // catch (Exception ex) => Results.Problem(ex.ToString()), dumping a full stack trace to
        // an AllowAnonymous, unauthenticated caller. Fails today: 500 with the exception's
        // ToString() (type name, message, stack frames) in the response body.
        await using var factory = new CustomWebApplicationFactory();

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await dbContext.Database.EnsureCreatedAsync();
        }

        var client = CreateTestClient(factory);

        const string malformedPayload = "{ this is not valid json at all";
        var signature = CreateSignature(malformedPayload, TestWebhookSecret);
        var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/stripe");
        request.Content = new StringContent(malformedPayload, Encoding.UTF8, "application/json");
        request.Headers.Add("Stripe-Signature", signature);

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            $"a malformed JSON body must be rejected with 400 via a local JsonException arm, not surfaced as a " +
            $"generic 500 (actual status {response.StatusCode}, body: {body})");
        body.Should().NotContain("StackTrace",
            "the response body must never carry an exception dump to an unauthenticated caller");
        body.Should().NotContain("   at ",
            "the response body must not contain stack frame lines (e.g. '   at Namespace.Type.Method(...)')");
    }

    private static string CreateSignature(string payload, string secret)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var signedPayload = $"{timestamp}.{payload}";

        using var hmac = new System.Security.Cryptography.HMACSHA256(System.Text.Encoding.UTF8.GetBytes(secret));
        var hashBytes = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(signedPayload));
        var hash = Convert.ToHexString(hashBytes).ToLowerInvariant();

        return $"t={timestamp},v1={hash}";
    }
}
