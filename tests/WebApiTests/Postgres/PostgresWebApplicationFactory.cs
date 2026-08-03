using Application.Abstractions.Authentication;
using Application.Abstractions.Billing;
using Application.Abstractions.Data;
using Application.Abstractions.Storage;
using Infrastructure.Database;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;
using System.Threading.Tasks;
using Infrastructure.Database.SeedData;
using Microsoft.Extensions.Configuration;
using Web.Api;
using WebApiTests.Fakes;

namespace WebApiTests.Postgres;

public sealed class PostgresWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;

    public FakeStorageService Storage { get; } = new();

    public SqlCommandCapturingInterceptor CommandInterceptor { get; } = new();

    public PostgresWebApplicationFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Environment.SetEnvironmentVariable("UseInMemoryDatabase", "false");
        Environment.SetEnvironmentVariable("Jwt__Secret", "SecretKeyForTestingPurposesOnly1234567890");
        Environment.SetEnvironmentVariable("Jwt__Issuer", "CarStore");
        Environment.SetEnvironmentVariable("Jwt__Audience", "CarStore");
        Environment.SetEnvironmentVariable("ConnectionStrings__Redis", "");

        Environment.SetEnvironmentVariable("Storage__Minio__InternalEndpoint", "http://minio:9000");
        Environment.SetEnvironmentVariable("Storage__Minio__PublicEndpoint", "http://localhost:9000");
        Environment.SetEnvironmentVariable("Storage__Minio__AccessKey", "minioadmin");
        Environment.SetEnvironmentVariable("Storage__Minio__SecretKey", "minioadmin123");
        Environment.SetEnvironmentVariable("Storage__Minio__BucketName", "cars");

        Environment.SetEnvironmentVariable("Stripe__SecretKey", "sk_test_mock");
        Environment.SetEnvironmentVariable("Stripe__WebhookSecret", "whsec_mock");
        Environment.SetEnvironmentVariable("Stripe__PriceId", "price_mock");

        builder.UseEnvironment("Testing");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<ApplicationDbContext>();
            services.RemoveAll<IApplicationDbContext>();

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseNpgsql(_connectionString)
                    .UseSnakeCaseNamingConvention()
                    .AddInterceptors(CommandInterceptor));

            services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());
            services.AddScoped<DbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

            services.RemoveAll<IStorageService>();
            services.AddSingleton<IStorageService>(Storage);

            services.RemoveAll<ISubscriptionGateway>();
            services.AddSingleton<ISubscriptionGateway, FakeSubscriptionGateway>();
        });
    }

    public async Task InitializeDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();

        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var logger = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>().CreateLogger("DatabaseSeeder");

        await DatabaseSeeder.SeedAsync(db, passwordHasher, configuration, logger);

        var dealerId = Guid.Parse(CustomWebApplicationFactory.AdminDealerId);
        if (!await db.Clients.IgnoreQueryFilters().AnyAsync(c => c.FirstName == "José"))
        {
            var joseClient = new Domain.Clients.Client(dealerId, "José", "Gomez", "99887766", "jose.gomez@email.com", "+54 11 9988-7766", "Av. Mayo 123", DateTime.UtcNow);
            db.Clients.Add(joseClient);
            await db.SaveChangesAsync();
        }
    }
}
