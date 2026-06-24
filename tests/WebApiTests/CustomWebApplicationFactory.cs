using Application.Abstractions.Authentication;
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

    public CustomWebApplicationFactory()
    {
        // Crear conexión compartida para Sqlite en memoria
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Forzar variables de entorno ANTES de cualquier configuración
        Environment.SetEnvironmentVariable("UseInMemoryDatabase", "true");
        Environment.SetEnvironmentVariable("Jwt__Secret", "SecretKeyForTestingPurposesOnly1234567890");
        Environment.SetEnvironmentVariable("Jwt__Issuer", "CarStore");
        Environment.SetEnvironmentVariable("Jwt__Audience", "CarStore");
        Environment.SetEnvironmentVariable("ConnectionStrings__Redis", "");

        // Storage:Minio config so the IOptions<MinioOptions> ValidateOnStart passes in tests.
        // The real MinioStorageService is replaced below by FakeStorageService, so these
        // values are never used to reach a real MinIO.
        Environment.SetEnvironmentVariable("Storage__Minio__InternalEndpoint", "http://minio:9000");
        Environment.SetEnvironmentVariable("Storage__Minio__PublicEndpoint", "http://localhost:9000");
        Environment.SetEnvironmentVariable("Storage__Minio__AccessKey", "minioadmin");
        Environment.SetEnvironmentVariable("Storage__Minio__SecretKey", "minioadmin123");
        Environment.SetEnvironmentVariable("Storage__Minio__BucketName", "cars");

        builder.UseEnvironment("Testing");

        builder.ConfigureTestServices(services =>
        {
            // Remover TODO rastro de EF Core previo
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<ApplicationDbContext>();
            services.RemoveAll<IApplicationDbContext>();

            // Registrar el DbContext con Sqlite real
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlite(_connection));

            services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

            // Swap the real MinIO-backed storage for the in-memory fake.
            services.RemoveAll<IStorageService>();
            services.AddSingleton<IStorageService>(Storage);
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
