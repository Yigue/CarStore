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
}
