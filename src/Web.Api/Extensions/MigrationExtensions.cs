using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Infrastructure.Database;
using Infrastructure.Database.SeedData;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Web.Api.Extensions;

public static class MigrationExtensions
{
    public static void ApplyMigrations(this IApplicationBuilder app)
    {
        try
        {
            using IServiceScope scope = app.ApplicationServices.CreateScope();

            using ApplicationDbContext dbContext =
                scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            if (dbContext.Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                Log.Information("Applying database migrations...");
                dbContext.Database.Migrate();
                Log.Information("Database migrations completed successfully");
            }
            else
            {
                Log.Information("Skipping migrations for non-Npgsql database provider ({ProviderName})", dbContext.Database.ProviderName);
                dbContext.Database.EnsureCreated();
            }

            // Seed datos iniciales (en desarrollo y testing)
            var env = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();
            if (env.IsDevelopment() || env.IsEnvironment("Testing"))
            {
                Log.Information("Seeding data for environment {Environment}...", env.EnvironmentName);
                var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
                var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
                var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
                ILogger logger = loggerFactory.CreateLogger("Infrastructure.Database.SeedData");

                // Usar dbContext directamente para evitar problemas de interfaz en tests
                DatabaseSeeder.SeedAsync(dbContext, passwordHasher, configuration, logger).GetAwaiter().GetResult();
                Log.Information("Data seeded successfully");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "An error occurred while applying migrations");
            // No relanzar en testing para permitir que otros tests sigan si falla el seeder global
            if (app.ApplicationServices.GetRequiredService<IWebHostEnvironment>().IsEnvironment("Testing"))
            {
                return;
            }
            throw;
        }
    }
}
