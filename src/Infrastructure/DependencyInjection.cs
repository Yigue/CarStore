using System.Text;
using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Storage;
using Application.Services;
using Infrastructure.Authentication;
using Infrastructure.Authorization;
using Infrastructure.Caching;
using Infrastructure.Database;
using Infrastructure.Storage;
using Infrastructure.Time;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using SharedKernel;

namespace Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration) =>
        services
            .AddServices()
            .AddDatabase(configuration)
            .AddCaching(configuration)
            .AddHealthChecks(configuration)
            .AddAuthenticationInternal(configuration)
            .AddAuthorizationInternal();

    private static IServiceCollection AddServices(this IServiceCollection services)
    {
        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
        
        // Registrar servicios de almacenamiento según la configuración
        services.AddSingleton<IBlobStorageService>(provider =>
        {
            var configuration = provider.GetRequiredService<IConfiguration>();
            var azureConnectionString = configuration["AzureBlobStorage:ConnectionString"];
            
            // Si la cadena de conexión de Azure existe y no está vacía, usar Azure Blob Storage
            if (!string.IsNullOrEmpty(azureConnectionString))
            {
                try
                {
                    return new AzureBlobStorageService(configuration);
                }
                catch (Exception)
                {
                    // Si hay algún error inicializando Azure, usar almacenamiento local
                    return new LocalFileStorageService(configuration);
                }
            }
            else
            {
                // Si no hay cadena de conexión configurada, usar almacenamiento local
                return new LocalFileStorageService(configuration);
            }
        });
        
        return services;
    }

    private static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        string? connectionString = configuration.GetConnectionString("Database") ?? throw new ArgumentNullException(nameof(configuration));

        services.AddDbContext<ApplicationDbContext>(
            options => options
                .UseNpgsql(connectionString, npgsqlOptions =>
                {
                    npgsqlOptions.MigrationsHistoryTable(HistoryRepository.DefaultTableName, Schemas.Default);
                    npgsqlOptions.MigrationsAssembly("Infrastructure");
                    npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "public");
                })
                .UseSnakeCaseNamingConvention());

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

        return services;
    }

    private static IServiceCollection AddCaching(this IServiceCollection services, IConfiguration configuration)
    {
        var redisConnectionString = configuration.GetConnectionString("Redis");
        
        if (!string.IsNullOrEmpty(redisConnectionString))
        {
            // Configurar Redis como caché distribuido
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConnectionString;
                options.InstanceName = "CarStore:";
            });

            // Registrar servicio de caché
            services.AddSingleton<ICacheService, RedisCacheService>();
        }
        else
        {
            // Si no hay Redis configurado, usar caché en memoria como fallback
            services.AddDistributedMemoryCache();
            services.AddSingleton<ICacheService, RedisCacheService>();
        }

        // Registrar servicios de caché para datos frecuentes
        services.AddScoped<CachedBrandService>();
        services.AddScoped<CachedModelService>();
        services.AddScoped<CachedCategoryService>();

        return services;
    }

    private static IServiceCollection AddHealthChecks(this IServiceCollection services, IConfiguration configuration)
    {
        var healthChecksBuilder = services
            .AddHealthChecks()
            .AddNpgSql(configuration.GetConnectionString("Database")!);

        // Agregar health check de Redis si está configurado
        var redisConnectionString = configuration.GetConnectionString("Redis");
        if (!string.IsNullOrEmpty(redisConnectionString))
        {
            healthChecksBuilder.AddRedis(redisConnectionString, name: "redis");
        }

        return services;
    }

    private static IServiceCollection AddAuthenticationInternal(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(o =>
            {
                o.RequireHttpsMetadata = false;
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:Secret"]!)),
                    ValidIssuer = configuration["Jwt:Issuer"],
                    ValidAudience = configuration["Jwt:Audience"],
                    ClockSkew = TimeSpan.Zero
                };
            });

        services.AddHttpContextAccessor();
        services.AddScoped<IUserContext, UserContext>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<ITokenProvider, TokenProvider>();

        return services;
    }

    private static IServiceCollection AddAuthorizationInternal(this IServiceCollection services)
    {
        services.AddAuthorization();

        services.AddScoped<PermissionProvider>();

        services.AddTransient<IAuthorizationHandler, PermissionAuthorizationHandler>();

        services.AddTransient<IAuthorizationPolicyProvider, PermissionAuthorizationPolicyProvider>();

        return services;
    }
}
