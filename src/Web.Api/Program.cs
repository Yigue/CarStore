using System.Reflection;
using Application;
using HealthChecks.UI.Client;
using Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Serilog;
using Web.Api;
using Web.Api.Extensions;
using Microsoft.Extensions.FileProviders;
using System.IO;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, loggerConfig) => loggerConfig.ReadFrom.Configuration(context.Configuration));

builder.Services.AddSwaggerGenWithAuth();

builder.Services
    .AddApplication()
    .AddPresentation()
    .AddInfrastructure(builder.Configuration);

builder.Services.AddEndpoints(Assembly.GetExecutingAssembly());

// Configurar CORS para permitir solicitudes desde la aplicación React
builder.Services.AddCors(options =>
{
    // Use an empty array if configuration is null, avoiding CA1861 with a static field (or just ignore for simplicity here by being explicit)
    // Actually CA1861 complains about "new[] { ... }" being allocated every time.
    // Since this is startup code run once, it's a minor optimization, but I'll fix it by suppressing or changing logic.
    // The easiest fix for the linter without creating a static field class is to disable the warning here or move it.
    // I'll move defaults to a local variable.

    string[] defaultOrigins = ["http://localhost:3000", "http://localhost:5173"];
    var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() 
        ?? defaultOrigins;
    
    options.AddPolicy("CorsPolicy", policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials());
});

WebApplication app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwaggerWithUi();
    app.ApplyMigrations();
    // Seeding data is optional but useful
    // app.SeedTestData();
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

app.UseAuthentication();

app.UseAuthorization();

// Mapear endpoints después de configurar middleware
app.MapEndpoints();

app.MapHealthChecks("health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

// REMARK: If you want to use Controllers, you'll need this.
app.MapControllers();

await app.RunAsync();

// REMARK: Required for functional and integration tests to work.
namespace Web.Api
{
    public partial class Program; // IDE0053: Expression body for class declaration (or semi-colon for empty class in C# 12)
}
