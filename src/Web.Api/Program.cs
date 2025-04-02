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
// builder.Services.AddCors(options =>
// {
//     options.AddPolicy("CorsPolicy", policy =>
//     {
//         policy.AllowAnyOrigin() // En producción, restrinja esto a su dominio React
//               .AllowAnyMethod()
//               .AllowAnyHeader();
//     });
// });

WebApplication app = builder.Build();

app.MapEndpoints();

if (app.Environment.IsDevelopment())
{
    app.UseSwaggerWithUi();
    app.ApplyMigrations();
}

// Habilitar CORS
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
   

app.MapHealthChecks("health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.UseRequestContextLogging();

app.UseSerilogRequestLogging();

app.UseExceptionHandler();

app.UseAuthentication();

app.UseAuthorization();

// REMARK: If you want to use Controllers, you'll need this.
app.MapControllers();

await app.RunAsync();

// REMARK: Required for functional and integration tests to work.
namespace Web.Api
{
    public partial class Program;
}
