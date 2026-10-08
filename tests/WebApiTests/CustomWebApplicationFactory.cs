using Application.Abstractions.Authentication;
using Application.Abstractions.Billing;
using Application.Abstractions.Data;
using Infrastructure.Database;
using Infrastructure.Database.SeedData;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Web.Api;
using System.Collections.Generic;
using System;
using Microsoft.Data.Sqlite;
using Application.Abstractions.Storage;
using WebApiTests.Fakes;

namespace WebApiTests;

public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    public string Id { get; } = Guid.NewGuid().ToString();
    private readonly SqliteConnection _connection;

    /// <summary>In-memory storage double; tests can assert uploads/deletes against it.</summary>
    public FakeStorageService Storage { get; } = new();

    public const string AdminDealerId = "11111111-1111-1111-1111-111111111111";

    /// <summary>
    /// ASPNETCORE_ENVIRONMENT the host boots under. Defaults to "Testing" (existing behavior for
    /// every pre-existing caller). qa-p1-integridad PR1 (D1) needs "Development" and "Production"
    /// hosts side by side to prove <c>RouteHandlerOptions.ThrowOnBadRequest</c> convergence — see
    /// <c>IntegrationTests/ErrorContract/GlobalHandlerConvergenceTests.cs</c>.
    /// </summary>
    private readonly string _environment;

    public CustomWebApplicationFactory() : this("Testing")
    {
    }

    internal CustomWebApplicationFactory(string environment)
    {
        _environment = environment;

        // Crear conexión compartida para Sqlite en memoria
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Use host configuration via UseSetting instead of process-wide environment variables
        builder.UseSetting("UseInMemoryDatabase", "true");
        builder.UseSetting("Jwt:Secret", "SecretKeyForTestingPurposesOnly1234567890");
        builder.UseSetting("Jwt:Issuer", "CarStore");
        builder.UseSetting("Jwt:Audience", "CarStore");
        builder.UseSetting("ConnectionStrings:Redis", "");

        // Storage:Minio config so the IOptions<MinioOptions> ValidateOnStart passes in tests.
        // The real MinioStorageService is replaced below by FakeStorageService, so these
        // values are never used to reach a real MinIO.
        builder.UseSetting("Storage:Minio:InternalEndpoint", "http://minio:9000");
        builder.UseSetting("Storage:Minio:PublicEndpoint", "http://localhost:9000");
        builder.UseSetting("Storage:Minio:AccessKey", "minioadmin");
        builder.UseSetting("Storage:Minio:SecretKey", "minioadmin123");
        builder.UseSetting("Storage:Minio:BucketName", "cars");
        
        // Mock Stripe secrets to bypass SubscriptionEnforcement checks
        builder.UseSetting("Stripe:SecretKey", "sk_test_mock");
        builder.UseSetting("Stripe:WebhookSecret", "whsec_mock");
        builder.UseSetting("Stripe:PriceId", "price_mock");

        builder.UseEnvironment(_environment);

        builder.ConfigureTestServices(services =>
        {
            // Remover TODO rastro de EF Core previo
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<Microsoft.EntityFrameworkCore.Infrastructure.IDbContextOptionsConfiguration<ApplicationDbContext>>();
            services.RemoveAll<ApplicationDbContext>();
            services.RemoveAll<IApplicationDbContext>();

            // Registrar el DbContext con Sqlite real.
            // UseSnakeCaseNamingConvention must match production's Npgsql registration
            // (DependencyInjection.cs) — raw-SQL filtered indexes reference physical
            // snake_case column names and silently produce an empty schema on EnsureCreated
            // if the convention is missing here.
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlite(_connection).UseSnakeCaseNamingConvention());

            services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

            // ProvisionDealerCommandHandler needs the base DbContext for BeginTransactionAsync.
            // Resolved to the same scoped ApplicationDbContext instance as IApplicationDbContext.
            services.AddScoped<DbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

            // Swap the real MinIO-backed storage for the in-memory fake.
            services.RemoveAll<IStorageService>();
            services.AddSingleton<IStorageService>(Storage);

            // Mock Stripe to avoid real API calls and Exceptions
            services.RemoveAll<ISubscriptionGateway>();
            services.AddSingleton<ISubscriptionGateway, FakeSubscriptionGateway>();
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _connection.Dispose();
        }
        base.Dispose(disposing);
    }

    public void SeedDatabase()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Database.EnsureCreated();

        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseSeeder");

        DatabaseSeeder.SeedAsync(db, passwordHasher, configuration, logger).GetAwaiter().GetResult();
    }
}
