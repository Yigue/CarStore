using Application.Billing.Entitlements;
using Infrastructure.Billing;
using Infrastructure.Caching;
using Microsoft.Extensions.Options;
using Moq;
using Stripe;

namespace InfrastructureTests.Billing;

/// <summary>
/// sfc A3.1.3 (A1, A21, RN-011): <see cref="StripePlanCatalog.GetEntitlementsAsync"/> is a config-only
/// lookup (<c>Billing:Plans:{code}:Limits:*</c>) — it never calls Stripe, unlike <c>GetAsync</c>.
/// </summary>
public class StripePlanCatalogEntitlementsTests
{
    private static StripePlanCatalog CreateCatalog(StripeOptions options) =>
        new(Mock.Of<IStripeClient>(), Mock.Of<ICacheService>(), Microsoft.Extensions.Options.Options.Create(options));

    [Fact]
    public async Task GetEntitlementsAsync_ForAConfiguredPlanWithLimits_ResolvesThem()
    {
        var options = new StripeOptions
        {
            Plans = new Dictionary<string, StripePlanOptions>
            {
                ["standard"] = new StripePlanOptions
                {
                    PriceId = "price_123",
                    Limits = new StripePlanLimitsOptions { MaxVehicles = 50, MaxUsers = 5, MaxWebhooks = 3 }
                }
            }
        };

        PlanLimits? limits = await CreateCatalog(options).GetEntitlementsAsync("standard", CancellationToken.None);

        limits.Should().NotBeNull();
        limits!.MaxVehicles.Should().Be(50);
        limits.MaxUsers.Should().Be(5);
        limits.MaxWebhooks.Should().Be(3);
        limits.MaxApiKeys.Should().BeNull();
    }

    [Fact]
    public async Task GetEntitlementsAsync_ForAnUnconfiguredCode_ReturnsNull()
    {
        var options = new StripeOptions
        {
            Plans = new Dictionary<string, StripePlanOptions>
            {
                ["standard"] = new StripePlanOptions { PriceId = "price_123" }
            }
        };

        PlanLimits? limits = await CreateCatalog(options).GetEntitlementsAsync("unknown", CancellationToken.None);

        limits.Should().BeNull();
    }

    [Fact]
    public async Task GetEntitlementsAsync_ForAPlanWithNoLimitsSection_ReturnsNull_FailOpen()
    {
        var options = new StripeOptions
        {
            Plans = new Dictionary<string, StripePlanOptions>
            {
                ["standard"] = new StripePlanOptions { PriceId = "price_123" } // no Limits configured
            }
        };

        PlanLimits? limits = await CreateCatalog(options).GetEntitlementsAsync("standard", CancellationToken.None);

        limits.Should().BeNull("a plan with no declared limits is unlimited, RN-011 fail-open");
    }
}
