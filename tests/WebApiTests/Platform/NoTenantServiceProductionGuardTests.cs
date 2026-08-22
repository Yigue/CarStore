using Application.Abstractions.Data;
using Application.Abstractions.Storage;
using Application.Abstractions.Tenancy;
using FluentAssertions;
using Infrastructure.Database;
using Infrastructure.Tenancy;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WebApiTests.Fakes;

namespace WebApiTests.Platform;

/// <summary>
/// Integration tests for <see cref="Infrastructure.Tenancy.NoTenantServiceProductionGuard"/>.
/// Verifies that booting the host in Production with NoTenantService throws InvalidOperationException.
/// Task 1.5.8.
/// </summary>
public class NoTenantServiceProductionGuardTests
{
    [Fact]
    public void ProductionHost_WithNoTenantServiceRegistered_ThrowsInvalidOperationExceptionOnStartup()
    {
        // Arrange: production factory that overrides ICurrentTenantService → NoTenantService
        using var factory = new NoTenantServiceProductionFactory();

        // Act & Assert: the host fails to start because the guard detects NoTenantService
        var act = () => factory.CreateClient();
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*NoTenantService*");
    }

    /// <summary>
    /// WebApplicationFactory that boots the app in "Production" environment
    /// with NoTenantService registered as ICurrentTenantService.
    /// This intentionally triggers the NoTenantServiceProductionGuard.
    /// </summary>
    private sealed class NoTenantServiceProductionFactory : WebApplicationFactory<Web.Api.Program>
    {
        private readonly SqliteConnection _connection;

        public NoTenantServiceProductionFactory()
        {
            _connection = new SqliteConnection("Filename=:memory:");
            _connection.Open();
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            // Set environment variables required for Production startup validations.
            Environment.SetEnvironmentVariable("Jwt__Secret", "SecretKeyForProductionGuardTest1234567890");
            Environment.SetEnvironmentVariable("Jwt__Issuer", "CarStore");
            Environment.SetEnvironmentVariable("Jwt__Audience", "CarStore");
            Environment.SetEnvironmentVariable("ConnectionStrings__Redis", "");
            Environment.SetEnvironmentVariable("Storage__Minio__InternalEndpoint", "http://minio:9000");
            Environment.SetEnvironmentVariable("Storage__Minio__PublicEndpoint", "http://localhost:9000");
            Environment.SetEnvironmentVariable("Storage__Minio__AccessKey", "minioadmin");
            Environment.SetEnvironmentVariable("Storage__Minio__SecretKey", "minioadmin123");
            Environment.SetEnvironmentVariable("Storage__Minio__BucketName", "cars");
            Environment.SetEnvironmentVariable("Stripe__SecretKey", "sk_test_dummy");
            Environment.SetEnvironmentVariable("Stripe__PriceId", "price_dummy");
            Environment.SetEnvironmentVariable("Stripe__WebhookSecret", "whsec_dummy");

            // Production environment triggers NoTenantServiceProductionGuard registration.
            builder.UseEnvironment("Production");

            builder.ConfigureTestServices(services =>
            {
                // Use SQLite in-memory instead of real Postgres.
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.RemoveAll<ApplicationDbContext>();
                services.RemoveAll<IApplicationDbContext>();
                services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(_connection).UseSnakeCaseNamingConvention());
                services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

                // Replace storage with no-op fake to avoid Minio dependency.
                services.RemoveAll<IStorageService>();
                services.AddSingleton<IStorageService>(new FakeStorageService());

                // Override ICurrentTenantService with NoTenantService.
                // The production guard checks for this exact type and must throw.
                services.RemoveAll<ICurrentTenantService>();
                services.AddScoped<ICurrentTenantService, NoTenantService>();
            });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _connection.Dispose();
            base.Dispose(disposing);
        }
    }
}
