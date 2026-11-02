using System.Net;
using System.Net.Sockets;
using System.Text;
using Application.Abstractions;
using Application.Abstractions.Authentication;
using Application.Abstractions.Caching;
using Application.Abstractions.Data;
using Application.Abstractions.Integrations;
using Domain.Cars.Events;
using Infrastructure.Integrations;
using Infrastructure.Integrations.MercadoLibre;
using MediatR;
using Application.Abstractions.Storage;
using Application.Abstractions.Tenancy;
using Application.Abstractions.Webhooks;
using Application.Users.Register;
using Infrastructure.Authentication;
using Infrastructure.Authorization;
using Infrastructure.Caching;
using Infrastructure.Database;
using Infrastructure.Storage;
using Domain.Services;
using Application.Abstractions.Messaging;
using Infrastructure.Dealers;
using Infrastructure.Services;
using Infrastructure.Tenancy;
using Infrastructure.Time;
using Infrastructure.Users;
using Infrastructure.Webhooks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Minio;
using Stripe;
using Application.Abstractions.Billing;
using Infrastructure.Billing;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
        IConfiguration configuration,
        IWebHostEnvironment? environment = null) =>
        services
            .AddTenancy(environment)
            .AddServices()
            .AddDatabase(configuration)
            .AddCaching(configuration)
            .AddHealthChecks(configuration)
            .AddAuthenticationInternal(configuration)
            .AddAuthorizationInternal()
            .ConfigureOpenTelemetry()
            .AddBackgroundJobs(configuration)
            .AddBilling(configuration)
            .AddIntegrations(configuration, environment);

    /// <summary>sfc C1.1 / ADR C22: encrypted credentials, command queue dispatcher and rate limiter.
    /// sfc C1.2 / C1.3 (ADR C25): adds the MercadoLibre push client/webhook processor and its domain-event
    /// handlers — registered here (not auto-scanned) because MediatR only scans the Application assembly.</summary>
    private static IServiceCollection AddIntegrations(
        this IServiceCollection services, IConfiguration configuration, IWebHostEnvironment? environment)
    {
        services.AddIntegrationDataProtection(configuration);
        if (environment?.IsProduction() == true)
        {
            services.AddTransient<IStartupFilter, DataProtectionProductionGuard>();
        }

        services.AddSingleton<ICredentialProtector, DataProtectionCredentialProtector>();
        services.AddSingleton<IIntegrationProviderSettings, IntegrationProviderSettings>();

        var rateLimit = (configuration.GetSection(RateLimiterOptions.SectionName).Get<RateLimiterOptions>() ?? new RateLimiterOptions()).Validate();
        services.AddSingleton(rateLimit);

        string? redisConnectionString = configuration.GetConnectionString("Redis");
        if (!string.IsNullOrEmpty(redisConnectionString))
        {
            services.AddSingleton<IConnectionMultiplexer>(_ =>
            {
                ConfigurationOptions options = ConfigurationOptions.Parse(redisConnectionString);
                options.AbortOnConnectFail = false;
                return ConnectionMultiplexer.Connect(options);
            });
            services.AddSingleton<IRateLimiter, RedisRateLimiter>();
        }
        else
        {
            services.AddSingleton<IRateLimiter, InMemoryRateLimiter>();
        }

        services.AddScoped<IntegrationCommandDispatcher>();

        services.AddOptions<MercadoLibreOptions>()
            .BindConfiguration(MercadoLibreOptions.SectionName);
        services.AddHttpClient(MercadoLibreClient.HttpClientName, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(15);
        });
        services.AddScoped<MercadoLibreClient>();
        services.AddScoped<IIntegrationClient, MercadoLibreClient>(sp => sp.GetRequiredService<MercadoLibreClient>());
        services.AddScoped<MercadoLibreWebhookProcessor>();
        services.AddScoped<INotificationHandler<CarPriceChangedDomainEvent>, MercadoLibreListingSyncHandlers>();
        services.AddScoped<INotificationHandler<Domain.Cars.Events.CarSoldDomainEvent>, MercadoLibreListingSyncHandlers>();
        services.AddScoped<INotificationHandler<Domain.Cars.Events.CarDeleteDomainEvent>, MercadoLibreListingSyncHandlers>();

        return services;
    }

    private static IServiceCollection AddBilling(this IServiceCollection services, IConfiguration configuration)
    {
        var stripe = services.AddOptions<StripeOptions>()
            .BindConfiguration(StripeOptions.SectionName);

        // spr PR 1.1 (ADR B1, D1): "plan único" is validated whenever Stripe is actually wired up.
        services.AddSingleton<IValidateOptions<StripeOptions>, StripeOptionsValidator>();

        var billingEnabled = configuration.GetValue<bool?>("Stripe:Enabled") ?? !string.IsNullOrWhiteSpace(configuration["Stripe:SecretKey"]);
        if (billingEnabled)
        {
            stripe.ValidateDataAnnotations();
            stripe.ValidateOnStart();
            services.AddSingleton<IStripeClient>(new StripeClient(configuration["Stripe:SecretKey"]));
            services.AddScoped<ISubscriptionGateway, StripeSubscriptionGateway>();
            services.AddScoped<IPlanCatalog, StripePlanCatalog>();
        }
        else
        {
            services.AddScoped<ISubscriptionGateway, NoOpSubscriptionGateway>();
            services.AddScoped<IPlanCatalog, NoOpPlanCatalog>();
        }

        // Pure translation (no I/O): needed even when billing is disabled so queued events can still be applied.
        services.AddSingleton<IBillingEventTranslator, StripeBillingEventTranslator>();

        services.AddScoped<ISubscriptionStatusCache, RedisSubscriptionStatusCache>();
        // Short-TTL (30s) cache over the lifecycle read: the guard hits it on every authenticated request.
        // IMemoryCache is a singleton, so invalidation from the retention handler reaches every scoped reader.
        services.AddMemoryCache();
        services.AddScoped<Infrastructure.Tenancy.EfTenantLifecycleReader>();
        services.AddScoped<Infrastructure.Tenancy.CachedTenantLifecycleReader>(sp => new Infrastructure.Tenancy.CachedTenantLifecycleReader(
            sp.GetRequiredService<Infrastructure.Tenancy.EfTenantLifecycleReader>(),
            sp.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>()));
        services.AddScoped<Application.Abstractions.Tenancy.ITenantLifecycleReader>(sp => sp.GetRequiredService<Infrastructure.Tenancy.CachedTenantLifecycleReader>());
        services.AddScoped<Application.Abstractions.Tenancy.ITenantLifecycleCache>(sp => sp.GetRequiredService<Infrastructure.Tenancy.CachedTenantLifecycleReader>());
        services.AddScoped<IDealerSubscriptionRepository, DealerSubscriptionRepository>();
        services.AddScoped<ProcessedStripeEventRepository>();

        return services;
    }

    private static IServiceCollection AddTenancy(
        this IServiceCollection services,
        IWebHostEnvironment? environment = null)
    {
        // Multi-tenancy service: reads DealerId from JWT "dealer_id" claim via IHttpContextAccessor
        // IHttpContextAccessor is registered in AddAuthenticationInternal()
        //
        // PR1 (saas-custom-domains) ADR-1: bind Tenant:DevFallbackDealerId so the
        // non-test safety assertion in Web.Api/Program.cs can read it and the
        // CurrentTenantService can honor the Development-only convenience fallback.
        services.AddOptions<TenantFallbackOptions>()
            .BindConfiguration(TenantFallbackOptions.SectionName);
        services.AddScoped<ICurrentTenantService, CurrentTenantService>();

        // ADR-1 production guard: NoTenantService MUST never be the active ICurrentTenantService
        // in production. Register a startup filter that validates the registration at app startup.
        if (environment?.IsProduction() == true)
        {
            services.AddTransient<IStartupFilter, NoTenantServiceProductionGuard>();
        }

        return services;
    }

    private static IServiceCollection AddServices(this IServiceCollection services)
    {
        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
        services.AddSingleton<Application.Legal.ILegalTermsProvider, Infrastructure.Legal.ConfigurationLegalTermsProvider>();

        // sfc B3.1 / B8: QuestPDF-backed document renderer. License is set once via a
        // static constructor on QuestPdfRenderer (see that class), not here.
        services.AddSingleton<Application.Abstractions.Documents.IDocumentRenderer, Infrastructure.Documents.QuestPdfRenderer>();

        // PHASE-2 (Inventario-Overhaul): MinIO-backed storage for the Cars aggregate.
        // ADR-3: single production implementation (no factory, no multi-binding).
        // Coexists with IBlobStorageService (Documents) per ADR-2.
        // Fails startup with a clear OptionsValidationException if required keys are missing.
        services.AddOptions<MinioOptions>()
            .BindConfiguration(MinioOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddSingleton<Minio.IMinioClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<MinioOptions>>().Value;
            Uri internalEndpoint = Services.Internal.PresignedUrlRewriter.NormalizeEndpoint(options.InternalEndpoint);
            string endpoint = internalEndpoint.IsDefaultPort
                ? internalEndpoint.Host
                : $"{internalEndpoint.Host}:{internalEndpoint.Port}";

            return new Minio.MinioClient()
                .WithEndpoint(endpoint)
                .WithCredentials(options.AccessKey, options.SecretKey)
                .WithRegion(options.Region)
                .WithSSL(options.UseSsl)
                .Build();
        });
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
        services.AddScoped<IDealerNotificationService, DealerNotificationService>();
        services.AddScoped<IRoundRobinLeadAllocator, RoundRobinLeadAllocator>();
        // sfc B4.3 / RN-018: per-dealer quote folios.
        services.AddScoped<Application.Abstractions.Data.IDealerSequenceAllocator, Common.DealerSequenceAllocator>();

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

        services.AddOptions<ResendOptions>()
            .BindConfiguration(ResendOptions.SectionName)
            .ValidateDataAnnotations();

        services.AddHttpClient<ResendEmailService>();

        services.AddScoped<Application.Abstractions.Messaging.IEmailService>(provider =>
        {
            var cfg = provider.GetRequiredService<IConfiguration>();
            var providerName = cfg["Email:Provider"];
            var resendApiKey = cfg["Email:Resend:ApiKey"] ?? cfg["Resend:ApiKey"];
            var smtpHost = cfg["Email:Smtp:Host"];

            if (string.Equals(providerName, "Resend", StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrWhiteSpace(resendApiKey) && string.IsNullOrEmpty(smtpHost)))
            {
                return provider.GetRequiredService<ResendEmailService>();
            }

            if (!string.IsNullOrWhiteSpace(smtpHost))
            {
                return new SmtpEmailService(
                    provider.GetRequiredService<IOptions<EmailOptions>>(),
                    provider.GetRequiredService<ILogger<SmtpEmailService>>());
            }

            return new NoOpEmailService(
                provider.GetRequiredService<ILogger<NoOpEmailService>>());
        });

        // sfc A2.3 / A18: template catalog on top of the IEmailService transport.
        services.AddScoped<Application.Abstractions.Email.ITransactionalEmailSender, TransactionalEmail.TransactionalEmailSender>();
        // sfc B5.1: anonymous endpoints queue their emails so the response time does not reveal whether one was sent.
        services.AddSingleton<TransactionalEmail.BackgroundEmailQueue>();
        services.AddSingleton<Application.Abstractions.Email.IBackgroundEmailQueue>(sp => sp.GetRequiredService<TransactionalEmail.BackgroundEmailQueue>());
        services.AddHostedService<TransactionalEmail.BackgroundEmailWorker>();

        return services;
    }

    private static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        bool useInMemory = configuration.GetValue<bool>("UseInMemoryDatabase");

        if (useInMemory)
        {
            // No registramos nada aqui para permitir que el proyecto de tests inyecte su propio provider
            return services;
        }

        string? connectionString = configuration.GetConnectionString("Database") ?? throw new ArgumentNullException(nameof(configuration));

            // sfc B2.2: the interceptor writes the DealerAuditLogEntry rows inside the same SaveChanges.
            services.AddScoped<Database.Interceptors.TenantAuditInterceptor>();

            services.AddDbContext<ApplicationDbContext>(
                (serviceProvider, options) => options
                    .AddInterceptors(serviceProvider.GetRequiredService<Database.Interceptors.TenantAuditInterceptor>())
                    .UseNpgsql(connectionString, npgsqlOptions =>
                    {
                        npgsqlOptions.MigrationsHistoryTable(HistoryRepository.DefaultTableName, Schemas.Default);
                        npgsqlOptions.MigrationsAssembly("Infrastructure");
                        npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "public");
                    })
                    .UseSnakeCaseNamingConvention());

            services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

            // The ProvisionDealerCommandHandler needs both IApplicationDbContext (for DbSet
            // access) and the base DbContext (for Database.BeginTransactionAsync). Both resolve
            // to the same scoped ApplicationDbContext instance.
            services.AddScoped<DbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

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

        bool useInMemory = configuration.GetValue<bool>("UseInMemoryDatabase");

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
        // sfc B2.2: carries the business reason of the current scope (also registered when the DB is replaced in tests).
        services.AddScoped<Application.Abstractions.Auditing.IAuditContext, Auditing.AuditContext>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<ITokenProvider, TokenProvider>();

        return services;
    }

    private static IServiceCollection AddAuthorizationInternal(this IServiceCollection services)
    {
        services.AddAuthorization();

        services.AddScoped<PermissionProvider>();
        // sfc C2.2 / ADR C26: branch data scope for listings.
        services.AddScoped<Application.Abstractions.Branches.IBranchScope, BranchScope>();

        // Without this, a permission change waits out PermissionProvider's 30-minute cache: a
        // granted permission looks ignored, and a REVOKED one keeps working.
        services.AddScoped<Application.Abstractions.Authorization.IPermissionCacheInvalidator,
            Infrastructure.Authorization.PermissionCacheInvalidator>();

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
    private static IServiceCollection AddBackgroundJobs(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IWebhookDestinationGuard, WebhookDestinationGuard>();

        // Named client for outgoing webhook deliveries — anti-SSRF SocketsHttpHandler validates
        // resolved IPs in ConnectCallback, connects directly to the validated IP to defeat DNS rebinding,
        // and refuses auto-redirects. Short 10s timeout prevents slow endpoints from stalling dispatch.
        services.AddHttpClient(DispatchWebhookDeliveriesJob.HttpClientName, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
        })
        .ConfigurePrimaryHttpMessageHandler(sp =>
        {
            var guard = sp.GetRequiredService<IWebhookDestinationGuard>();
            return new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                ConnectCallback = async (context, cancellationToken) =>
                {
                    IPAddress[] entry = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, cancellationToken);
                    if (entry is null || entry.Length == 0)
                    {
                        throw new HttpRequestException("Destination blocked: unable to resolve host.");
                    }

                    IPAddress? targetIp = null;
                    foreach (var ip in entry)
                    {
                        if (guard.IsBlocked(ip))
                        {
                            throw new HttpRequestException($"Destination blocked: resolved IP '{ip}' is in a restricted range.");
                        }

                        targetIp ??= ip;
                    }

                    var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
                    try
                    {
                        await socket.ConnectAsync(new IPEndPoint(targetIp!, context.DnsEndPoint.Port), cancellationToken);
                        return new NetworkStream(socket, ownsSocket: true);
                    }
                    catch
                    {
                        socket.Dispose();
                        throw;
                    }
                }
            };
        });

        // Crm:Reengagement:Enabled defaults to false — see CrmReengagementOptions.
        services.AddOptions<CrmReengagementOptions>()
            .BindConfiguration(CrmReengagementOptions.SectionName);

        services.AddOptions<BillingReconciliationOptions>()
            .BindConfiguration(BillingReconciliationOptions.SectionName);

        // Skip background jobs when using in-memory database (integration tests)
        bool useInMemory = configuration.GetValue<bool>("UseInMemoryDatabase");
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

            // Job 3: Process Stripe Webhooks
            var stripeJobKey = new JobKey(nameof(ProcessStripeWebhooksJob));
            configure
                .AddJob<ProcessStripeWebhooksJob>(opts => opts.WithIdentity(stripeJobKey))
                .AddTrigger(trigger =>
                    trigger.ForJob(stripeJobKey)
                        .WithSimpleSchedule(schedule =>
                            schedule.WithIntervalInSeconds(5)
                                .RepeatForever()));

            // Job 4: Dispatch outgoing webhook deliveries (retry/backoff state lives on
            // WebhookDelivery rows — see Domain.Webhooks.WebhookRetryPolicy).
            var webhookDispatchJobKey = new JobKey(nameof(DispatchWebhookDeliveriesJob));
            configure
                .AddJob<DispatchWebhookDeliveriesJob>(opts => opts.WithIdentity(webhookDispatchJobKey))
                .AddTrigger(trigger =>
                    trigger.ForJob(webhookDispatchJobKey)
                        .WithSimpleSchedule(schedule =>
                            schedule.WithIntervalInSeconds(15)
                                .RepeatForever()));

            // sfc C1.1 / ADR C22: integration command queue (providers opt in via Integrations:{Provider}:Enabled).
            var integrationJobKey = new JobKey(nameof(DispatchIntegrationCommandsJob));
            configure
                .AddJob<DispatchIntegrationCommandsJob>(opts => opts.WithIdentity(integrationJobKey))
                .AddTrigger(trigger =>
                    trigger.ForJob(integrationJobKey)
                        .WithSimpleSchedule(schedule =>
                            schedule.WithIntervalInSeconds(10)
                                .RepeatForever()));

            // Job 5: Lost-lead re-engagement (disabled by default — Crm:Reengagement:Enabled).
            var reengagementJobKey = new JobKey(nameof(LeadReengagementJob));
            configure
                .AddJob<LeadReengagementJob>(opts => opts.WithIdentity(reengagementJobKey))
                .AddTrigger(trigger =>
                    trigger.ForJob(reengagementJobKey)
                        .WithSimpleSchedule(schedule =>
                            schedule.WithIntervalInHours(24)
                                .RepeatForever()));

            // Job 7: Tenant data exports (RN-014): builds pending ZIPs and expires old ones.
            var exportJobKey = new JobKey(nameof(BuildTenantExportJob));
            configure
                .AddJob<BuildTenantExportJob>(opts => opts.WithIdentity(exportJobKey))
                .AddTrigger(trigger =>
                    trigger.ForJob(exportJobKey)
                        .WithSimpleSchedule(schedule =>
                            schedule.WithIntervalInSeconds(30)
                                .RepeatForever()));

            // Job 8: Tenant purge (RN-013, A1.4): daily at 03:00 UTC. Dry run unless Retention:PurgeEnabled=true.
            var purgeJobKey = new JobKey(nameof(PurgeTenantDataJob));
            configure
                .AddJob<PurgeTenantDataJob>(opts => opts.WithIdentity(purgeJobKey))
                .AddTrigger(trigger =>
                    trigger.ForJob(purgeJobKey)
                        .WithCronSchedule("0 0 3 * * ?"));

            // sfc C2.4: after-sales reminders (RN-049), daily at 10:30 UTC.
            var serviceReminderJobKey = new JobKey(nameof(ServiceReminderJob));
            configure
                .AddJob<ServiceReminderJob>(opts => opts.WithIdentity(serviceReminderJobKey))
                .AddTrigger(trigger =>
                    trigger.ForJob(serviceReminderJobKey)
                        .WithCronSchedule("0 30 10 * * ?"));

            // sfc B6.2: follow-up reminders (RN-041), daily at 11:00 UTC (08:00 in Argentina).
            var followUpJobKey = new JobKey(nameof(FollowUpCadenceJob));
            configure
                .AddJob<FollowUpCadenceJob>(opts => opts.WithIdentity(followUpJobKey))
                .AddTrigger(trigger =>
                    trigger.ForJob(followUpJobKey)
                        .WithCronSchedule("0 0 11 * * ?"));

            // sfc B1.3.3: deposit sweep (RN-002/RN-003), hourly at minute 5.
            var depositJobKey = new JobKey(nameof(ExpireDepositsJob));
            configure
                .AddJob<ExpireDepositsJob>(opts => opts.WithIdentity(depositJobKey))
                .AddTrigger(trigger =>
                    trigger.ForJob(depositJobKey)
                        .WithCronSchedule("0 5 * * * ?"));

            // Job 9: retries storage deletes parked by the tenant purge (every 15 minutes). No feature flag: it only
            // deletes objects of tenants already purged.
            var storageRetryJobKey = new JobKey(nameof(StorageDeletionRetryJob));
            configure
                .AddJob<StorageDeletionRetryJob>(opts => opts.WithIdentity(storageRetryJobKey))
                .AddTrigger(trigger =>
                    trigger.ForJob(storageRetryJobKey)
                        .WithSimpleSchedule(schedule =>
                            schedule.WithIntervalInMinutes(15)
                                .RepeatForever()));

            // Job 6: Billing reconciliation (spr ADR B9) — flags dealers without a confirmed
            // subscription past the grace window; never deletes. Billing:Reconciliation:Enabled
            // turns it off entirely; the interval is read here because Quartz fixes it at startup.
            var reconciliation = configuration.GetSection(BillingReconciliationOptions.SectionName)
                .Get<BillingReconciliationOptions>() ?? new BillingReconciliationOptions();
            if (reconciliation.Enabled)
            {
                var reconcileJobKey = new JobKey(nameof(ReconcileSubscriptionsJob));
                configure
                    .AddJob<ReconcileSubscriptionsJob>(opts => opts.WithIdentity(reconcileJobKey))
                    .AddTrigger(trigger =>
                        trigger.ForJob(reconcileJobKey)
                            .WithSimpleSchedule(schedule =>
                                schedule.WithIntervalInMinutes(Math.Max(1, reconciliation.IntervalMinutes))
                                    .RepeatForever()));
            }
        });

        services.AddQuartzHostedService();

        return services;
    }
}
