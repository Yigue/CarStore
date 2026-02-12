# Web.Api Layer - Agent Context

## Purpose

The Web.Api layer is the entry point for HTTP requests. Uses Minimal APIs pattern for endpoints, middleware for cross-cutting concerns.

---

## Tech Stack

| Component | Technology |
|-----------|------------|
| Endpoints | Minimal APIs |
| Auth | JWT Bearer |
| Docs | OpenAPI/Swagger |
| Logging | Serilog |
| Validation | FluentValidation |

---

## Folder Structure

```
Web.Api/
├── Endpoints/
│   ├── Cars/
│   │   ├── CarsEndpoints.cs
│   │   └── CarRequests.cs
│   ├── Clients/
│   ├── Sales/
│   └── Auth/
│
├── Middleware/
│   ├── ExceptionHandlingMiddleware.cs
│   ├── RequestLoggingMiddleware.cs
│   └── CorrelationIdMiddleware.cs
│
├── Extensions/
│   ├── ResultExtensions.cs
│   └── ClaimsPrincipalExtensions.cs
│
├── Program.cs              # Composition root
├── Dockerfile
└── appsettings.json
```

---

## Endpoint Example

```csharp
public static class CarsEndpoints
{
    public static void MapCarsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/cars")
            .WithTags("Cars")
            .RequireAuthorization();

        group.MapGet("/", GetCars);
        group.MapGet("/{id:guid}", GetCarById);
        group.MapPost("/", CreateCar);
        group.MapPut("/{id:guid}", UpdateCar);
        group.MapDelete("/{id:guid}", DeleteCar);
    }

    private static async Task<IResult> GetCars(
        ISender sender,
        [AsParameters] ListCarsQuery query,
        CancellationToken ct)
    {
        var result = await sender.Send(query, ct);
        return result.ToMinimalApiResult();
    }

    private static async Task<IResult> CreateCar(
        ISender sender,
        CreateCarRequest request,
        CancellationToken ct)
    {
        var command = new CreateCarCommand(
            request.Patente,
            request.Price,
            request.BrandId,
            request.ModelId
        );
        
        var result = await sender.Send(command, ct);
        
        return result.Match(
            id => Results.Created($"/api/cars/{id}", new { id }),
            error => Results.BadRequest(error)
        );
    }
}
```

---

## Request/Response DTOs

```csharp
// Requests (incoming)
public sealed record CreateCarRequest(
    string Patente,
    decimal Price,
    Guid BrandId,
    Guid ModelId
);

// Use extension to convert Result to IResult
public static class ResultExtensions
{
    public static IResult ToMinimalApiResult<T>(this Result<T> result)
    {
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.BadRequest(new { error = result.Error.Message });
    }
}
```

---

## Program.cs Structure

```csharp
var builder = WebApplication.CreateBuilder(args);

// 1. Add services
builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 2. Build app
var app = builder.Build();

// 3. Configure pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

// 4. Map endpoints
app.MapCarsEndpoints();
app.MapClientsEndpoints();
app.MapSalesEndpoints();
app.MapAuthEndpoints();

app.MapHealthChecks("/health");

app.Run();
```

---

## Exception Handling Middleware

```csharp
public class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (ValidationException ex)
        {
            logger.LogWarning("Validation error: {Errors}", ex.Errors);
            context.Response.StatusCode = 400;
            await context.Response.WriteAsJsonAsync(new
            {
                type = "ValidationError",
                errors = ex.Errors.Select(e => new { e.PropertyName, e.ErrorMessage })
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception");
            context.Response.StatusCode = 500;
            await context.Response.WriteAsJsonAsync(new
            {
                type = "ServerError",
                message = "An unexpected error occurred"
            });
        }
    }
}
```

---

## Rules

- ✅ Group endpoints by domain (Cars, Clients, Sales)
- ✅ Use `ISender` to dispatch commands/queries
- ✅ Return `IResult` from handlers
- ✅ Keep endpoints thin - logic in Application layer
- ❌ NO business logic in endpoints
- ❌ NO direct DbContext access
