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
using System.Data.Common;

namespace WebApiTests;

public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    public string Id { get; } = Guid.NewGuid().ToString();
    private readonly DbConnection _connection;

    public const string AdminDealerId = "00000000-0000-0000-0000-000000000001";

    public CustomWebApplicationFactory()
    {
        // Crear conexiÃ³n compartida para Sqlite in-memory
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Forzar variables de entorno ANTES de cualquier configuraciÃ³n
        Environment.SetEnvironmentVariable("UseInMemoryDatabase", "true");
        Environment.SetEnvironmentVariable("Jwt__Secret", "SecretKeyForTestingPurposesOnly1234567890");
        Environment.SetEnvironmentVariable("Jwt__Issuer", "CarStore");
        Environment.SetEnvironmentVariable("Jwt__Audience", "CarStore");

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
