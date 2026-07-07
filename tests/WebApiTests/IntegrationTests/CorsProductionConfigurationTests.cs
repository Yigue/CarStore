using Application.Abstractions.Data;
using FluentAssertions;
using Infrastructure.Database;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Web.Api;

namespace WebApiTests.IntegrationTests;

/// <summary>
/// PR1 (saas-custom-domains) task 1.4.4 regression guard.
/// spec: wildcard-subdomain-routing §CORS — a per-dealer subdomain (e.g.
/// xyz.carstore.com) must be able to fetch the public API from the browser
/// in Production, or wildcard-subdomain-routing is defeated for every
/// client-rendered request.
/// </summary>
public sealed class CorsProductionConfigurationTests
{
    /// <summary>
    /// Wraps a raw <see cref="WebApplicationFactory{Program}"/> (env=Production,
    /// unlike <c>CustomWebApplicationFactory</c> which forces "Testing") together
    /// with the SQLite connection backing its <see cref="IApplicationDbContext"/>.
    /// <para>
    /// A real SQLite-backed context must be wired here (mirroring
    /// <c>CustomWebApplicationFactory</c>) because <c>UseInMemoryDatabase=true</c>
    /// deliberately skips registering <c>IApplicationDbContext</c> in
    /// <c>Infrastructure.DependencyInjection.AddDatabase</c>, expecting the test
    /// host to supply its own. Without it, the FIRST real HTTP request eagerly
    /// builds the full minimal-API route table and throws on an unrelated
    /// endpoint (<c>GetImageWithSas</c>) whose <c>IApplicationDbContext</c>
    /// parameter can no longer be inferred as a service.
    /// </para>
    /// </summary>
    private sealed class ProductionFactory : IDisposable
    {
        private readonly SqliteConnection _connection;
        public WebApplicationFactory<Program> Factory { get; }

        public ProductionFactory()
        {
            _connection = new SqliteConnection("Filename=:memory:");
            _connection.Open();

            // Mirrors CustomWebApplicationFactory: AddDatabase (Infrastructure/DependencyInjection.cs)
            // reads UseInMemoryDatabase via Environment.GetEnvironmentVariable as a fallback,
            // because that top-level Program.cs statement runs before WebApplicationFactory's
            // ConfigureAppConfiguration additions are guaranteed to be merged into builder.Configuration.
            // Setting only the in-memory IConfiguration key here is not reliable enough — it must
            // be a real process env var, same as every other factory in this test project.
            Environment.SetEnvironmentVariable("UseInMemoryDatabase", "true");

            Factory = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.UseEnvironment("Production");

                    // Mirrors StartupAssertionTests: minimum config so DI/startup
                    // succeeds up to the point under test, without a real Postgres
                    // connection or a Tenant:DevFallbackDealerId override.
                    builder.ConfigureAppConfiguration((_, config) =>
                    {
                        config.AddInMemoryCollection(new Dictionary<string, string?>
                        {
                            ["Jwt:Secret"] = "SecretKeyForCorsProductionStartupTest000",
                            ["Jwt:Issuer"] = "CarStore",
                            ["Jwt:Audience"] = "CarStore",
                            ["UseInMemoryDatabase"] = "true",
                            ["Storage:Minio:InternalEndpoint"] = "http://minio:9000",
                            ["Storage:Minio:PublicEndpoint"] = "http://localhost:9000",
                            ["Storage:Minio:AccessKey"] = "minioadmin",
                            ["Storage:Minio:SecretKey"] = "minioadmin123",
                            ["Storage:Minio:BucketName"] = "cars",
                            ["ConnectionStrings:Redis"] = ""
                        });
                    });

                    builder.ConfigureTestServices(services =>
                    {
                        services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                        services.RemoveAll<ApplicationDbContext>();
                        services.RemoveAll<IApplicationDbContext>();

                        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(_connection));
                        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());
                    });
                });

            using var scope = Factory.Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.EnsureCreated();
        }

        public void Dispose()
        {
            Factory.Dispose();
            _connection.Dispose();
        }
    }

    /// <summary>
    /// Configuration-level guard: outside Development, `Cors:AllowedHostSuffixes`
    /// must resolve to a non-empty list containing the production wildcard
    /// domain. Catches the regression where the key silently defaulted to `[]`
    /// because no appsettings file (or env var) ever set it.
    /// </summary>
    [Fact]
    public void ProductionEnvironment_CorsAllowedHostSuffixes_IsNotEmpty()
    {
        using var production = new ProductionFactory();
        using var scope = production.Factory.Services.CreateScope();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        var suffixes = configuration.GetSection("Cors:AllowedHostSuffixes").Get<string[]>();

        suffixes.Should().NotBeNullOrEmpty(
            "outside Development, Cors:AllowedHostSuffixes must be populated or every " +
            "per-dealer subdomain browser request is CORS-rejected in production");
        suffixes.Should().Contain(".carstore.com");
    }

    /// <summary>
    /// End-to-end guard: a cross-origin browser request from a dealer subdomain
    /// (e.g. https://xyz.carstore.com) must receive Access-Control-Allow-Origin
    /// for that exact origin in Production.
    /// </summary>
    [Fact]
    public async Task ProductionEnvironment_RequestFromDealerSubdomain_ReceivesCorsAllowHeader()
    {
        using var production = new ProductionFactory();
        using var client = production.Factory.CreateClient();

        const string dealerOrigin = "https://xyz.carstore.com";
        client.DefaultRequestHeaders.Add("Origin", dealerOrigin);

        var response = await client.GetAsync("/api/v1/cars/search");

        response.Headers.TryGetValues("Access-Control-Allow-Origin", out var values)
            .Should().BeTrue("the CORS policy must reflect the dealer subdomain origin in production");
        values!.Should().Contain(dealerOrigin);
    }
}
