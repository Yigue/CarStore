using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Web.Api;

namespace WebApiTests.IntegrationTests;

/// <summary>
/// Startup guard tests for saas-custom-domains PR1 ADR-1.
/// task 1.6.4: env=Production + Tenant:DevFallbackDealerId → host throws InvalidOperationException.
/// </summary>
[Collection("StartupAssertionTests")]
public sealed class StartupAssertionTests
{
    /// <summary>
    /// spec: tenant-safety-default-deny §startup assertion
    /// When the application starts in Production with Tenant:DevFallbackDealerId configured,
    /// Program.cs must throw InvalidOperationException before the host is ready.
    /// This mirrors the Jwt:Secret fail-fast guard (Program.cs lines 128–144).
    /// </summary>
    [Fact]
    public void Host_WithProductionEnvironmentAndDevFallbackDealerId_ThrowsInvalidOperationException()
    {
        // Arrange — configure env=Production with a valid JWT secret and a DevFallbackDealerId.
        // UseInMemoryDatabase=true avoids needing a real PostgreSQL connection string.
        var fallbackGuid = Guid.NewGuid().ToString();

        var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Production");

                // spr PR 3.3 (B10): AddCaptcha's NoOp-vs-Turnstile decision (like AddBilling's
                // Stripe:SecretKey check below) runs synchronously during AddInfrastructure, which
                // executes on `builder.Configuration` BEFORE `Build()` — only `UseSetting` is visible
                // to it at that point; `ConfigureAppConfiguration`'s additions are only merged into
                // `app.Configuration` once `Build()` runs, too late for this particular read.
                builder.UseSetting("Captcha:SecretKey", "test-secret-key");

                // Provide env-vars that CustomWebApplicationFactory normally sets,
                // so the infrastructure DI registration succeeds up to the assertion point.
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        // JWT: valid secret ≥ 32 chars, not the placeholder.
                        ["Jwt:Secret"] = "SecretKeyForProductionStartupTestOnly00000",
                        ["Jwt:Issuer"] = "CarStore",
                        ["Jwt:Audience"] = "CarStore",

                        // The trigger: DevFallbackDealerId in Production must be rejected.
                        ["Tenant:DevFallbackDealerId"] = fallbackGuid,

                        // Avoid real DB connection string requirement.
                        ["UseInMemoryDatabase"] = "true",

                        // Minio: minimal values to pass options binding (ValidateOnStart fires
                        // during IHost.StartAsync which never runs — thrown before app.Run).
                        ["Storage:Minio:InternalEndpoint"] = "http://minio:9000",
                        ["Storage:Minio:PublicEndpoint"] = "http://localhost:9000",
                        ["Storage:Minio:AccessKey"] = "minioadmin",
                        ["Storage:Minio:SecretKey"] = "minioadmin123",
                        ["Storage:Minio:BucketName"] = "cars",

                        // spr PR 5.1 (OPS-06): Production now requires enforcement on; this test is
                        // about DevFallbackDealerId, not billing, so give it a webhook secret too.
                        ["FeatureFlags:SubscriptionEnforcement"] = "true",
                        ["Stripe:WebhookSecret"] = "whsec_startup_test_only",

                        // Redis: empty → uses in-memory cache fallback.
                        ["ConnectionStrings:Redis"] = ""
                    });
                });
            });

        // Act — CreateClient triggers EnsureServer which runs Program.cs startup code.
        var act = () => factory.CreateClient();

        // Assert — the Program.cs assertion must fire before the host starts.
        act.Should().Throw<Exception>()
            .And.Message.Should().Contain("DevFallbackDealerId",
                because: "Program.cs must reject DevFallbackDealerId in non-Development environments");
    }

    [Fact]
    public void Host_WithStripeSecretKeyButNoPriceId_ThrowsValidationException()
    {
        var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("Stripe:SecretKey", "sk_test_123");
                builder.UseSetting("Stripe:PriceId", ""); // Explicitly missing
                // spr PR 3.3 (B10): must be UseSetting, not ConfigureAppConfiguration — see the
                // comment on the first test above for why.
                builder.UseSetting("Captcha:SecretKey", "test-secret-key");
                builder.UseEnvironment("Production");
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        // spr PR 5.1 (OPS-06): Production now requires enforcement on; this test is
                        // about Stripe PriceId validation, which fires at Build() before this guard
                        // would even run — kept consistent anyway.
                        ["FeatureFlags:SubscriptionEnforcement"] = "true",
                        ["Stripe:WebhookSecret"] = "whsec_startup_test_only",
                        ["Jwt:Secret"] = "SecretKeyForProductionStartupTestOnly00000",
                        ["UseInMemoryDatabase"] = "true",
                        ["Storage:Minio:InternalEndpoint"] = "http://minio:9000",
                        ["Storage:Minio:PublicEndpoint"] = "http://localhost:9000",
                        ["Storage:Minio:AccessKey"] = "minioadmin",
                        ["Storage:Minio:SecretKey"] = "minioadmin123",
                        ["Storage:Minio:BucketName"] = "cars"
                    });
                });
            });

        var act = () => factory.CreateClient();

        act.Should().Throw<Microsoft.Extensions.Options.OptionsValidationException>()
            .And.Message.Should().Contain("PriceId");
    }

    [Fact]
    public void Host_WithNoStripeSection_BootsClean()
    {
        var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Production");
                // spr PR 3.3 (B10): must be UseSetting, not ConfigureAppConfiguration — without
                // this, Production + no visible Captcha:SecretKey would throw from the anti-NoOp
                // captcha guard, contradicting "boots clean". See the comment on the first test
                // above for why ConfigureAppConfiguration doesn't reach this particular check.
                builder.UseSetting("Captcha:SecretKey", "test-secret-key");
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        // No Stripe:SecretKey/PriceId section — only the webhook secret spr PR 5.1
                        // (OPS-06) now requires in Production alongside enforcement being on.
                        ["FeatureFlags:SubscriptionEnforcement"] = "true",
                        ["Stripe:WebhookSecret"] = "whsec_startup_test_only",
                        ["Jwt:Secret"] = "SecretKeyForProductionStartupTestOnly00000",
                        ["UseInMemoryDatabase"] = "true",
                        ["Storage:Minio:InternalEndpoint"] = "http://minio:9000",
                        ["Storage:Minio:PublicEndpoint"] = "http://localhost:9000",
                        ["Storage:Minio:AccessKey"] = "minioadmin",
                        ["Storage:Minio:SecretKey"] = "minioadmin123",
                        ["Storage:Minio:BucketName"] = "cars"
                    });
                });
            });

        var act = () => factory.CreateClient();

        act.Should().NotThrow();
    }
}

[CollectionDefinition("StartupAssertionTests", DisableParallelization = true)]
public class StartupAssertionTestsCollection
{
}
