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
        builder.UseSetting("UseInMemoryDatabase", "false");
        builder.UseSetting("Jwt:Secret", "SecretKeyForTestingPurposesOnly1234567890");
        builder.UseSetting("Jwt:Issuer", "CarStore");
        builder.UseSetting("Jwt:Audience", "CarStore");
        builder.UseSetting("ConnectionStrings:Redis", "");

        builder.UseSetting("Storage:Minio:InternalEndpoint", "http://minio:9000");
        builder.UseSetting("Storage:Minio:PublicEndpoint", "http://localhost:9000");
        builder.UseSetting("Storage:Minio:AccessKey", "minioadmin");
        builder.UseSetting("Storage:Minio:SecretKey", "minioadmin123");
        builder.UseSetting("Storage:Minio:BucketName", "cars");

        builder.UseSetting("Stripe:SecretKey", "sk_test_mock");
        builder.UseSetting("Stripe:WebhookSecret", "whsec_mock");
        builder.UseSetting("Stripe:PriceId", "price_mock");

        builder.UseEnvironment("Testing");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<Microsoft.EntityFrameworkCore.Infrastructure.IDbContextOptionsConfiguration<ApplicationDbContext>>();
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
