using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Infrastructure.Database;
using Infrastructure.Database.SeedData;
using Microsoft.EntityFrameworkCore;
using Serilog;

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

            Log.Information("Applying database migrations...");
            dbContext.Database.Migrate();
            Log.Information("Database migrations completed successfully");
            
            // Seed datos iniciales (solo en desarrollo)
            var env = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();
            if (env.IsDevelopment())
            {
                Log.Information("Seeding development data...");
                var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
                var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
                
                DatabaseSeeder.SeedAsync(context, passwordHasher).GetAwaiter().GetResult();
                Log.Information("Development data seeded successfully");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "An error occurred while applying migrations");
            throw;
        }
    }
}
