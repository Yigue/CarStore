using System.Text;
using Application.Abstractions;
using Application.Abstractions.Authentication;
using Application.Abstractions.Caching;
using Application.Abstractions.Data;
using Application.Abstractions.Storage;
using Application.Abstractions.Tenancy;
using Application.Users.Register;
using Infrastructure.Authentication;
using Infrastructure.Authorization;
using Infrastructure.Caching;
using Infrastructure.Database;
using Infrastructure.Storage;
using Domain.Services;
using Infrastructure.Services;
using Infrastructure.Tenancy;
using Infrastructure.Time;
using Infrastructure.Users;
using Application.Users.Register;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using SharedKernel;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Quartz;
using Infrastructure.BackgroundJobs;

namespace Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration) =>
        services
            .AddTenancy()
            .AddServices()
            .AddDatabase(configuration)
            .AddCaching(configuration)
            .AddHealthChecks(configuration)
            .AddAuthenticationInternal(configuration)
            .AddAuthorizationInternal()
            .ConfigureOpenTelemetry()
            .AddBackgroundJobs();

    private static IServiceCollection AddTenancy(this IServiceCollection services)
    {
        // Multi-tenancy service: reads DealerId from JWT "dealer_id" claim via IHttpContextAccessor
        // IHttpContextAccessor is registered in AddAuthenticationInternal()
        services.AddScoped<ICurrentTenantService, CurrentTenantService>();

        return services;
    }

    private static IServiceCollection AddServices(this IServiceCollection services)
    {
        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();

        // PHASE-2 (Inventario-Overhaul): MinIO-backed storage for the Cars aggregate.
        // ADR-3: single production implementation (no factory, no multi-binding).
        // Coexists with IBlobStorageService (Documents) per ADR-2.
        // Fails startup with a clear OptionsValidationException if required keys are missing.
        services.AddOptions<MinioOptions>()
            .BindConfiguration(MinioOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddScoped<IStorageService, MinioStorageService>();

        // PHASE-3: Blob storage. Defaults to NoOpBlobStorageService (logs + synthetic
        // blob names/SAS URLs). Falls back to LocalFileStorageService if explicitly
        // configured. AzureBlobStorageService stays available for prod wire-up but is
        // NOT registered by default to avoid an Azure runtime dependency.
        services.AddScoped<IBlobStorageService>(provider =>
        {
            var configuration = provider.GetRequiredService<IConfiguration>();
            var azureConnectionString = configuration["AzureBlob:ConnectionString"];

            if (!string.IsNullOrWhiteSpace(azureConnectionString))
            {
                try
                {
                    return new AzureBlobStorageService(configuration);
                }
                catch (Exception)
                {
                    // Misconfigured Azure → fall back to NoOp (don't crash dev).
                    var logger = provider.GetRequiredService<ILogger<NoOpBlobStorageService>>();
                    return new NoOpBlobStorageService(logger);
                }
            }

            // Default for Phase 3: stubbed NoOp blob storage.
            var noOpLogger = provider.GetRequiredService<ILogger<NoOpBlobStorageService>>();
            return new NoOpBlobStorageService(noOpLogger);
        });

        services.AddScoped<IUserNotificationService, UserNotificationService>();
        services.AddScoped<IRoundRobinLeadAllocator, RoundRobinLeadAllocator>();

        // REQ-FIN-LEDGER-001: real EF-backed ledger. Replaces NoOpFinancialLedgerService
        // (deleted with this change). Idempotency is enforced via the partial
        // unique index `IX_transactions_ReconditioningTaskId_SourceId` (B.6).
        services.AddScoped<IFinancialLedgerService, EfFinancialLedgerService>();

        // PHASE-3: OCR. Defaults to MockOcrService (logs + canned ParsedDocumentDto).
        // AzureDocumentIntelligenceOcrService is wired only when Endpoint + ApiKey are
        // present in configuration — avoids any Azure runtime dependency in dev.
        services.AddScoped<IOcrService>(provider =>
        {
            var configuration = provider.GetRequiredService<IConfiguration>();
            var endpoint = configuration["AzureDocumentIntelligence:Endpoint"];
            var apiKey = configuration["AzureDocumentIntelligence:ApiKey"];

            if (!string.IsNullOrWhiteSpace(endpoint) && !string.IsNullOrWhiteSpace(apiKey))
            {
                return new AzureDocumentIntelligenceOcrService(configuration);
            }

            var logger = provider.GetRequiredService<ILogger<MockOcrService>>();
            return new MockOcrService(logger);
        });

        services.AddSingleton<FinancingCalculationService>();

        // Email: optional-service pattern (mirrors IBlobStorageService/IOcrService).
        // When Email:Smtp:Host is present → SmtpEmailService (real SMTP via MailKit).
        // When absent → NoOpEmailService (logs only, never throws).
        // ValidateOnStart is NOT used because an absent section must not crash boot (R6).
        services.AddOptions<EmailOptions>()
            .BindConfiguration(EmailOptions.SectionName)
            .ValidateDataAnnotations();

        services.AddScoped<Application.Abstractions.Messaging.IEmailService>(provider =>
        {
            var cfg = provider.GetRequiredService<IConfiguration>();
            var host = cfg["Email:Smtp:Host"];

            if (!string.IsNullOrWhiteSpace(host))
            {
                return new SmtpEmailService(
                    provider.GetRequiredService<IOptions<EmailOptions>>(),
                    provider.GetRequiredService<ILogger<SmtpEmailService>>());
            }

            return new NoOpEmailService(
                provider.GetRequiredService<ILogger<NoOpEmailService>>());
        });

        return services;
    }

    private static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        bool useInMemory = configuration.GetValue<bool>("UseInMemoryDatabase") || 
                          Environment.GetEnvironmentVariable("UseInMemoryDatabase") == "true";

        if (useInMemory)
        {
            // No registramos nada aqui para permitir que el proyecto de tests inyecte su propio provider
            return services;
        }

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
        services.AddScoped<ICachedBrandService, CachedBrandService>();
        services.AddScoped<ICachedModelService, CachedModelService>();
        services.AddScoped<ICachedCategoryService, CachedCategoryService>();

        return services;
    }

    private static IServiceCollection AddHealthChecks(this IServiceCollection services, IConfiguration configuration)
    {
        var healthChecksBuilder = services.AddHealthChecks();

        bool useInMemory = configuration.GetValue<bool>("UseInMemoryDatabase") || 
                          Environment.GetEnvironmentVariable("UseInMemoryDatabase") == "true";

        if (!useInMemory)
        {
            healthChecksBuilder.AddNpgSql(configuration.GetConnectionString("Database")!);

            // Agregar health check de Redis si estÃ¡ configurado
            var redisConnectionString = configuration.GetConnectionString("Redis");
            if (!string.IsNullOrEmpty(redisConnectionString))
            {
                healthChecksBuilder.AddRedis(redisConnectionString, name: "redis");
            }
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
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:Secret"]!)),
                    ValidIssuer = configuration["Jwt:Issuer"] ?? "carstore",
                    ValidAudience = configuration["Jwt:Audience"] ?? "carstore-api",
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

    private static IServiceCollection ConfigureOpenTelemetry(this IServiceCollection services)
    {
        services.AddOpenTelemetry()
            .WithLogging(logging => logging.AddOtlpExporter())
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddOtlpExporter())
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddEntityFrameworkCoreInstrumentation()
                .AddOtlpExporter());

        return services;
    }
    private static IServiceCollection AddBackgroundJobs(this IServiceCollection services)
    {
        // Skip background jobs when using in-memory database (integration tests)
        bool useInMemory = Environment.GetEnvironmentVariable("UseInMemoryDatabase") == "true";
        if (useInMemory)
        {
            return services;
        }

        services.AddQuartz(configure =>
        {
            // Job 1: Process Outbox Messages
            var outboxJobKey = new JobKey(nameof(ProcessOutboxMessagesJob));
            configure
                .AddJob<ProcessOutboxMessagesJob>(opts => opts.WithIdentity(outboxJobKey))
                .AddTrigger(trigger =>
                    trigger.ForJob(outboxJobKey)
                        .WithSimpleSchedule(schedule =>
                            schedule.WithIntervalInSeconds(10)
                                .RepeatForever()));

            // Job 2: Mark Expired Quotes
            var expiredQuotesJobKey = new JobKey(nameof(MarkExpiredQuotesJob));
            configure
                .AddJob<MarkExpiredQuotesJob>(opts => opts.WithIdentity(expiredQuotesJobKey))
                .AddTrigger(trigger =>
                    trigger.ForJob(expiredQuotesJobKey)
                        .WithSimpleSchedule(schedule =>
                            schedule.WithIntervalInMinutes(5) // Check every 5 minutes
                                .RepeatForever()));
        });

        services.AddQuartzHostedService();

        return services;
    }
}
