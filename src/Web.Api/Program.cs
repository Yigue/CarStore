using System.Reflection;
using System.Threading.RateLimiting;
using System.Text.Json.Serialization;
using Application;
using HealthChecks.UI.Client;
using Infrastructure;
using Infrastructure.Middleware;
using Infrastructure.Billing;
using Infrastructure.Tenancy;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Serilog;
using Web.Api;
using Web.Api.Extensions;
using Microsoft.Extensions.FileProviders;
using Asp.Versioning;
using Asp.Versioning.Builder;
using Asp.Versioning.ApiExplorer;
using System.IO;
using Microsoft.EntityFrameworkCore;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, loggerConfig) => loggerConfig.ReadFrom.Configuration(context.Configuration));

builder.Services.AddSwaggerGenWithAuth();

// Configuración de autenticación
if (builder.Environment.IsEnvironment("Testing"))
{
    builder.Configuration["Jwt:Secret"] = "SecretKeyForTestingPurposesOnly1234567890";
    builder.Configuration["Jwt:Issuer"] = "CarStore";
    builder.Configuration["Jwt:Audience"] = "CarStore";
}

builder.Services
    .AddApplication()
    .AddPresentation()
    .AddFeatureFlags(builder.Configuration)
    .AddInfrastructure(builder.Configuration, builder.Environment);

builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    o.SerializerOptions.Converters.Add(new Web.Api.Infrastructure.UtcDateTimeJsonConverter());
    o.SerializerOptions.Converters.Add(new Web.Api.Infrastructure.NullableGuidJsonConverter());
});
builder.Services.Configure<Microsoft.AspNetCore.Mvc.JsonOptions>(o =>
{
    o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    o.JsonSerializerOptions.Converters.Add(new Web.Api.Infrastructure.UtcDateTimeJsonConverter());
    o.JsonSerializerOptions.Converters.Add(new Web.Api.Infrastructure.NullableGuidJsonConverter());
});

// Rate limiting for login endpoint
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddFixedWindowLimiter("login", limiterOptions =>
    {
        var rateLimitConfig = builder.Configuration.GetSection("RateLimiting:Login");
        limiterOptions.PermitLimit = rateLimitConfig.GetValue("PermitLimit", 10);
        limiterOptions.Window = TimeSpan.FromSeconds(rateLimitConfig.GetValue("WindowSeconds", 60));
        limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiterOptions.QueueLimit = 0;
    });

    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            type = "https://datatracker.ietf.org/doc/html/rfc6585#section-4",
            title = "Too Many Requests",
            status = 429,
            detail = "Too many login attempts. Please try again later."
        }, cancellationToken);
    };
});

builder.Services.AddEndpoints(Assembly.GetExecutingAssembly());

// Configurar CORS para permitir solicitudes desde la aplicación React
builder.Services.AddCors(options =>
{
    var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
        ?? ["http://localhost:3000", "http://localhost:3001", "http://localhost:5173"];

    // Multi-tenant: each dealer is served from its own subdomain (e.g. lux.localhost
    // in dev, lux.carstore.com in prod). An exact-match origin list cannot cover
    // arbitrary tenant subdomains, so we also allow configured host suffixes.
    // ".localhost" is always allowed in Development for local tenant testing.
    var allowedHostSuffixes = builder.Configuration.GetSection("Cors:AllowedHostSuffixes").Get<string[]>()
        ?? [];
    if (builder.Environment.IsDevelopment() && !allowedHostSuffixes.Contains(".localhost"))
    {
        allowedHostSuffixes = [.. allowedHostSuffixes, ".localhost"];
    }

    bool IsOriginAllowed(string origin)
    {
        if (allowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        // Note: SetIsOriginAllowed reflects the exact origin back, so this stays
        // compatible with AllowCredentials (which forbids a wildcard "*").
        return Uri.TryCreate(origin, UriKind.Absolute, out var uri)
            && allowedHostSuffixes.Any(suffix =>
                uri.Host.Equals(suffix.TrimStart('.'), StringComparison.OrdinalIgnoreCase)
                || uri.Host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
    }

    options.AddPolicy("CorsPolicy", policy =>
    {
        policy.SetIsOriginAllowed(IsOriginAllowed)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

WebApplication app = builder.Build();

// Fail fast if the JWT signing key is missing or still set to the committed placeholder.
// The secret MUST be provided at runtime via the Jwt__Secret environment variable,
// a secrets manager, or dotnet user-secrets — never committed to source control.
string? jwtSecret = app.Configuration["Jwt:Secret"];
if (string.IsNullOrWhiteSpace(jwtSecret) ||
    jwtSecret.Contains("super-duper-secret", StringComparison.OrdinalIgnoreCase))
{
    throw new InvalidOperationException(
        "Jwt:Secret is not configured. Provide a strong secret via the Jwt__Secret " +
        "environment variable, a secrets manager, or dotnet user-secrets. " +
        "It must not be empty or the committed placeholder value.");
}

bool isDevOrTesting = app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing");
if (!isDevOrTesting && jwtSecret.Length < 32)
{
    throw new InvalidOperationException(
        "Jwt:Secret must be at least 32 characters in non-development environments " +
        "to provide adequate signing strength for HMAC-SHA256.");
}

bool subscriptionEnforcement = false;
bool.TryParse(app.Configuration["FeatureFlags:SubscriptionEnforcement"], out subscriptionEnforcement);
string? stripeWebhookSecret = app.Configuration["Stripe:WebhookSecret"];
if (string.IsNullOrWhiteSpace(stripeWebhookSecret))
{
    if (subscriptionEnforcement)
    {
        throw new InvalidOperationException("Stripe:WebhookSecret is required when FeatureFlags:SubscriptionEnforcement is true.");
    }
    else
    {
        app.Logger.LogWarning("Stripe:WebhookSecret is not configured. Webhook signature verification will fail for real events.");
    }
}

// PR1 (saas-custom-domains) ADR-1: refuse to start the host if a dev-fallback
// is configured outside Development. Mirrors the Jwt:Secret fail-fast above.
// Spec: openspec/changes/saas-custom-domains/specs/tenant-safety-default-deny
TenantFallbackOptions fallback = app.Services.GetRequiredService<IOptions<TenantFallbackOptions>>().Value;
if (fallback.DevFallbackDealerId is not null && !app.Environment.IsDevelopment())
{
    throw new InvalidOperationException(
        "Tenant:DevFallbackDealerId is not allowed in " +
        app.Environment.EnvironmentName + ". " +
        "Remove the key for non-Development environments to prevent cross-tenant data leaks. " +
        "PR1 saas-custom-domains ADR-1 — see openspec/changes/saas-custom-domains/specs/tenant-safety-default-deny.");
}

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
{
    app.ApplyMigrations();
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        IReadOnlyList<ApiVersionDescription> descriptions = app.DescribeApiVersions();

        foreach (ApiVersionDescription description in descriptions)
        {
            options.SwaggerEndpoint(
                $"/swagger/{description.GroupName}/swagger.json",
                description.GroupName.ToUpperInvariant());
        }
    });

    app.SeedTestData();
}

// Configurar middleware en el orden correcto
app.UseRouting();

// CORS debe ir después de UseRouting pero antes de UseAuthentication
app.UseCors("CorsPolicy");

// Configurar archivos estáticos
// 1. Servir archivos estáticos desde wwwroot (predeterminado)
app.UseStaticFiles();

// 2. Si estás usando almacenamiento local, configura un proveedor de archivos adicional
string uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "uploads");
if (!Directory.Exists(uploadPath))
{
    Directory.CreateDirectory(uploadPath);
}

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploadPath),
    RequestPath = "/uploads"
});

app.UseRequestContextLogging();

app.UseSerilogRequestLogging();

app.UseExceptionHandler();

app.UseRateLimiter();

app.UseAuthentication();

// Tenant resolution middleware - now it can see authentication claims
app.UseTenantResolution();

// ADR-6: Dealer suspension check — runs after tenant resolution so DealerId is known.
// SuperAdmin requests bypass this check via the platform_role claim.
app.UseDealerSuspensionCheck();

var flags = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<Web.Api.Infrastructure.FeatureFlagsOptions>>().Value;
if (flags.SubscriptionEnforcement)
{
    app.UseSubscriptionGuard();
}

app.UseAuthorization();

// Mapear endpoints despuÃ©s de configurar middleware
app.MapGet("/debug-test", () => "OK");
app.MapStripeWebhook();

ApiVersionSet apiVersionSet = app.NewApiVersionSet()
    .HasApiVersion(new ApiVersion(1))
    .ReportApiVersions()
    .Build();

RouteGroupBuilder versionedGroup = app
    .MapGroup("api/v{version:apiVersion}")
    .WithApiVersionSet(apiVersionSet);

versionedGroup.MapEndpoints();
versionedGroup.MapBillingEndpoints();
app.MapHealthChecks("health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.Run();

// REMARK: Required for functional and integration tests to work.
namespace Web.Api
{
    public partial class Program;
}
