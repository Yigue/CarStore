using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Web.Api;

namespace WebApiTests.Startup;

/// <summary>
/// spr PR 5.1 (OPS-06): a Production boot must never run with billing enforcement off — that would
/// let every dealer use the product without a confirmed subscription, the same free-ride risk the
/// Incomplete fail-closed (PR 3.1) and the CAPTCHA anti-NoOp guard (PR 3.3) already exist to prevent.
/// Mirrors the Jwt:Secret/DevFallbackDealerId/Captcha fail-fast guards in <c>StartupAssertionTests</c>.
/// </summary>
[Collection("StartupAssertionTests")]
public sealed class ProductionAssertionTests
{
    [Fact]
    public void Host_WithProductionEnvironmentAndSubscriptionEnforcementDisabled_ThrowsInvalidOperationException()
    {
        var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Production");
                // Production refuses to boot without an email provider (Program.cs); any SMTP host satisfies the guard.
                builder.UseSetting("Email:Smtp:Host", "localhost");
                // spr PR 3.3 (B10): must be UseSetting, not ConfigureAppConfiguration — AddCaptcha's
                // NoOp-vs-Turnstile decision runs synchronously during AddInfrastructure, on
                // builder.Configuration, before Build() merges ConfigureAppConfiguration's additions.
                builder.UseSetting("Captcha:SecretKey", "test-secret-key");
                // spr PR 5.1.8: same reason — AddBackgroundJobs now reads UseInMemoryDatabase
                // synchronously (to skip Quartz's persistent Postgres store under tests), so it must
                // also be UseSetting, or the host tries a real Postgres connection and fails auth.
                builder.UseSetting("UseInMemoryDatabase", "true");
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["FeatureFlags:SubscriptionEnforcement"] = "false",
                        ["Jwt:Secret"] = "SecretKeyForProductionStartupTestOnly00000",
                        ["ConnectionStrings:Redis"] = "",
                        ["Storage:Minio:InternalEndpoint"] = "http://minio:9000",
                        ["Storage:Minio:PublicEndpoint"] = "http://localhost:9000",
                        ["Storage:Minio:AccessKey"] = "minioadmin",
                        ["Storage:Minio:SecretKey"] = "minioadmin123",
                        ["Storage:Minio:BucketName"] = "cars"
                    });
                });
            });

        var act = () => factory.CreateClient();

        act.Should().Throw<InvalidOperationException>()
            .And.Message.Should().Contain("FeatureFlags:SubscriptionEnforcement",
                because: "Program.cs must reject a Production boot with billing enforcement disabled");
    }

    [Fact]
    public void Host_WithProductionEnvironmentAndSubscriptionEnforcementEnabled_BootsClean()
    {
        var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Production");
                // Production refuses to boot without an email provider (Program.cs); any SMTP host satisfies the guard.
                builder.UseSetting("Email:Smtp:Host", "localhost");
                builder.UseSetting("Captcha:SecretKey", "test-secret-key");
                builder.UseSetting("UseInMemoryDatabase", "true");
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["FeatureFlags:SubscriptionEnforcement"] = "true",
                        ["Stripe:WebhookSecret"] = "whsec_startup_test_only",
                        ["Jwt:Secret"] = "SecretKeyForProductionStartupTestOnly00000",
                        ["ConnectionStrings:Redis"] = "",
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

    [Fact]
    public void Host_WithProductionEnvironmentAndNoEmailProvider_ThrowsInvalidOperationException()
    {
        var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Production");
                builder.UseSetting("Captcha:SecretKey", "test-secret-key");
                builder.UseSetting("UseInMemoryDatabase", "true");
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["FeatureFlags:SubscriptionEnforcement"] = "true",
                        ["Stripe:WebhookSecret"] = "whsec_startup_test_only",
                        ["Jwt:Secret"] = "SecretKeyForProductionStartupTestOnly00000",
                        ["ConnectionStrings:Redis"] = "",
                        ["Storage:Minio:InternalEndpoint"] = "http://minio:9000",
                        ["Storage:Minio:PublicEndpoint"] = "http://localhost:9000",
                        ["Storage:Minio:AccessKey"] = "minioadmin",
                        ["Storage:Minio:SecretKey"] = "minioadmin123",
                        ["Storage:Minio:BucketName"] = "cars"
                    });
                });
            });

        var act = () => factory.CreateClient();

        act.Should().Throw<InvalidOperationException>()
            .And.Message.Should().Contain("email provider",
                because: "a Production host that only logs its emails silently drops password resets");
    }
}
