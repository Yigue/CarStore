using Application.Abstractions.Billing;
using Domain.Billing;
using FluentAssertions;
using Infrastructure.Billing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Moq;
using Stripe;
using Stripe.Checkout;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ApplicationTests.Billing;

/// <summary>
/// spr PR 1.1 (SPR-02/BE-BILLING-07, ADR B1, D1): the Stripe checkout session must carry
/// <c>metadata.planCode</c> (both top-level and on the created subscription) so the webhook can persist it.
/// </summary>
public class CreateCheckoutSessionTests
{
    [Fact]
    public async Task CreateCheckoutSessionAsync_IncludesPlanCodeInSessionAndSubscriptionMetadata()
    {
        var dealerId = Guid.NewGuid();

        // Short-circuits CreateCustomerAsync before it ever reaches Stripe — this test is only about the
        // checkout session's own metadata, not customer search/creation.
        var repository = new Mock<IDealerSubscriptionRepository>();
        repository.Setup(r => r.GetByDealerIdAsync(dealerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(DealerSubscription.CreatePending(dealerId, "cus_existing", string.Empty));

        var planCatalog = new Mock<IPlanCatalog>();
        planCatalog.Setup(c => c.GetAsync("standard", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlanDetails("standard", "price_test", 9900, "usd", "month", 14));

        var options = Microsoft.Extensions.Options.Options.Create(new StripeOptions
        {
            SecretKey = "sk_test_fake",
            WebhookSecret = "whsec_fake",
            PriceId = "price_test",
            TrialDays = 14
        });

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Frontend:BaseUrl"] = "https://app.example.com" })
            .Build();

        var fakeClient = new CapturingStripeClient();
        var gateway = new StripeSubscriptionGateway(fakeClient, repository.Object, configuration, options, planCatalog.Object);

        var url = await gateway.CreateCheckoutSessionAsync(dealerId, "admin@dealer.com", "standard", CheckoutReturn.None, CancellationToken.None);

        url.Should().Be("https://checkout.stripe.com/fake");
        fakeClient.CapturedSessionOptions.Should().NotBeNull();
        var captured = (SessionCreateOptions)fakeClient.CapturedSessionOptions!;
        captured.Metadata.Should().ContainKey("planCode").WhoseValue.Should().Be("standard");
        captured.SubscriptionData.Metadata.Should().ContainKey("planCode").WhoseValue.Should().Be("standard");
        captured.Metadata.Should().ContainKey("dealer_id").WhoseValue.Should().Be(dealerId.ToString());
        captured.LineItems[0].Price.Should().Be("price_test");
    }

    /// <summary>Captures the <see cref="SessionCreateOptions"/> Stripe.net builds for Checkout Session create calls.</summary>
    private sealed class CapturingStripeClient : IStripeClient
    {
        public object? CapturedSessionOptions { get; private set; }

        public string ApiBase => "https://api.stripe.com";
        public string ApiKey => "sk_test_fake";
        public string ClientId => string.Empty;
        public string ConnectBase => "https://connect.stripe.com";
        public string FilesBase => "https://files.stripe.com";

        public Task<T> RequestAsync<T>(HttpMethod method, string path, BaseOptions options, RequestOptions requestOptions, CancellationToken cancellationToken = default)
            where T : IStripeEntity
        {
            if (options is SessionCreateOptions sessionOptions)
            {
                CapturedSessionOptions = sessionOptions;
                return Task.FromResult((T)(object)new Session { Url = "https://checkout.stripe.com/fake" });
            }

            throw new InvalidOperationException($"Unexpected Stripe request for test: {path} with {options.GetType().Name}");
        }

        public Task<Stream> RequestStreamingAsync(HttpMethod method, string path, BaseOptions options, RequestOptions requestOptions, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
