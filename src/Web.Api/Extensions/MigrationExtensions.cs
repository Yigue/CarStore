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

                // Ajustes idempotentes para columnas agregadas a entidades sin una migración EF dedicada.
                // El dominio User tiene una propiedad Role mapeada como string, pero ninguna migración la creó.
                // Hasta que se genere `dotnet ef migrations add AddUserRole`, garantizamos la columna acá.
                dbContext.Database.ExecuteSqlRaw(@"
                    ALTER TABLE public.users
                        ADD COLUMN IF NOT EXISTS role character varying(20) NOT NULL DEFAULT 'Cliente';

                    CREATE TABLE IF NOT EXISTS public.dealer_settings (
                        id uuid NOT NULL CONSTRAINT PK_dealer_settings PRIMARY KEY,
                        dealer_id uuid NOT NULL,
                        dealer_name character varying(200) NOT NULL,
                        contact_email character varying(200) NOT NULL,
                        notifications_enabled boolean NOT NULL DEFAULT TRUE,
                        updated_at timestamp with time zone NOT NULL,
                        host_name character varying(200) NULL,
                        custom_domain character varying(200) NULL,
                        address character varying(500) NULL,
                        phone_number character varying(50) NULL,
                        facebook_url character varying(500) NULL,
                        instagram_url character varying(500) NULL,
                        twitter_url character varying(500) NULL,
                        interest_rate_tna numeric(5,2) NULL,
                        logo_url character varying(500) NULL,
                        primary_color character varying(7) NULL,
                        secondary_color character varying(7) NULL,
                        footer_text character varying(200) NULL
                    );
                    CREATE UNIQUE INDEX IF NOT EXISTS IX_dealer_settings_dealer_id ON public.dealer_settings (dealer_id);

                    -- Agregar columnas visuales que pudieran faltar (idempotente)
                    ALTER TABLE public.dealer_settings
                        ADD COLUMN IF NOT EXISTS logo_url character varying(500) NULL;
                    ALTER TABLE public.dealer_settings
                        ADD COLUMN IF NOT EXISTS primary_color character varying(7) NULL;
                    ALTER TABLE public.dealer_settings
                        ADD COLUMN IF NOT EXISTS secondary_color character varying(7) NULL;
                    ALTER TABLE public.dealer_settings
                        ADD COLUMN IF NOT EXISTS footer_text character varying(200) NULL;

                    INSERT INTO public.dealer_settings (id, dealer_id, dealer_name, contact_email, notifications_enabled, updated_at, host_name, custom_domain, address, phone_number, facebook_url, instagram_url, twitter_url, interest_rate_tna, logo_url, primary_color, secondary_color, footer_text)
                    VALUES ('22222222-2222-2222-2222-222222222222', '11111111-1111-1111-1111-111111111111', 'Lux Dealership', 'info@luxdealership.com', TRUE, NOW(), 'localhost', 'localhost', 'Av. del Libertador 4500, Palermo, CABA', '+54 11 9999-8888', 'https://facebook.com/luxdealership', 'https://instagram.com/luxdealership', 'https://twitter.com/luxdealership', 65.50, NULL, NULL, NULL, '© 2024 Lux Dealership. Todos los derechos reservados.')
                    ON CONFLICT (dealer_id) DO NOTHING;
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
