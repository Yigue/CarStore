using System.Reflection;
using System.Threading.RateLimiting;
using Application;
using HealthChecks.UI.Client;
using Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
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
    .AddInfrastructure(builder.Configuration);

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
    
    options.AddPolicy("CorsPolicy", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

WebApplication app = builder.Build();

if (app.Environment.IsDevelopment())
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

app.UseAuthorization();

// Mapear endpoints despuÃ©s de configurar middleware
app.MapGet("/debug-test", () => "OK");

ApiVersionSet apiVersionSet = app.NewApiVersionSet()
    .HasApiVersion(new ApiVersion(1))
    .ReportApiVersions()
    .Build();

RouteGroupBuilder versionedGroup = app
    .MapGroup("api/v{version:apiVersion}")
    .WithApiVersionSet(apiVersionSet);

versionedGroup.MapEndpoints();
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
