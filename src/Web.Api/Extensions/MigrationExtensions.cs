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
    private static void PreemptMigrationsCollision(DbContext dbContext)
    {
        try
        {
            var connection = dbContext.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
            {
                connection.Open();
            }

            using var cmd = connection.CreateCommand();
            
            // Verificamos si la tabla public.clients existe y si tiene la columna city
            cmd.CommandText = @"
                SELECT EXISTS (
                    SELECT 1 
                    FROM information_schema.columns 
                    WHERE table_schema = 'public' 
                      AND table_name = 'clients' 
                      AND column_name = 'city'
                );";
            
            var columnExists = (bool)(cmd.ExecuteScalar() ?? false);

            if (columnExists)
            {
                // Si la columna ya existe, las columnas de la migración ya están en la DB.
                // Insertamos la migración en __EFMigrationsHistory para que EF Core no intente aplicarla y falle.
                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS public.""__EFMigrationsHistory"" (
                        migration_id character varying(150) NOT NULL,
                        product_version character varying(32) NOT NULL,
                        CONSTRAINT pk___ef_migrations_history PRIMARY KEY (migration_id)
                    );
                    
                    INSERT INTO public.""__EFMigrationsHistory"" (migration_id, product_version)
                    VALUES ('20260601000735_AddMissingClientAndCarColumns', '8.0.4')
                    ON CONFLICT (migration_id) DO NOTHING;
                ";
                cmd.ExecuteNonQuery();
                Log.Information("Se previno la colisión de migración EF para 'AddMissingClientAndCarColumns' registrándola como aplicada.");
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "No se pudo prevenir la colisión de migración. Continuando normalmente.");
        }
    }

    public static void ApplyMigrations(this IApplicationBuilder app)
    {
        try
        {
            using IServiceScope scope = app.ApplicationServices.CreateScope();

            using ApplicationDbContext dbContext =
                scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            if (dbContext.Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                PreemptMigrationsCollision(dbContext);

                Log.Information("Applying database migrations...");
                dbContext.Database.Migrate();
                Log.Information("Database migrations completed successfully");

                // Ajustes idempotentes para columnas agregadas a entidades sin una migración EF dedicada.
                // Nota: se recomienda mover estos ajustes a migraciones reales de EF Core.
                dbContext.Database.ExecuteSqlRaw(@"
                    ALTER TABLE public.users
                        ADD COLUMN IF NOT EXISTS role character varying(20) NOT NULL DEFAULT 'Cliente';
                ");

                // Columnas faltantes en clients (ref: AddMissingClientAndCarColumns migration)
                dbContext.Database.ExecuteSqlRaw(@"
                    ALTER TABLE public.clients
                        ADD COLUMN IF NOT EXISTS type character varying(20) NOT NULL DEFAULT 'Individual';
                    ALTER TABLE public.clients
                        ADD COLUMN IF NOT EXISTS city character varying(100);
                    ALTER TABLE public.clients
                        ADD COLUMN IF NOT EXISTS notes character varying(2000);
                    ALTER TABLE public.clients
                        ADD COLUMN IF NOT EXISTS zip_code character varying(20);
                ");

                // Columnas faltantes en cars (ref: AddMissingClientAndCarColumns migration)
                dbContext.Database.ExecuteSqlRaw(@"
                    ALTER TABLE public.cars
                        ADD COLUMN IF NOT EXISTS is_highlighted boolean NOT NULL DEFAULT false;
                ");
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
