# CarStore Backend - Pendientes y Mejoras

## Estado Actual ✅

El backend ya tiene implementado:

- Clean Architecture (5 capas)
- CQRS con MediatR
- Outbox Pattern con Quartz
- OpenTelemetry (traces, metrics, logs)
- Redis Caching
- JWT Authentication
- Health Checks
- 6 Bounded Contexts

---

## Mejoras Pendientes

### 🔴 Alta Prioridad

#### 1. Rate Limiting

**Tiempo:** 15 min | **Impacto:** Alto

```csharp
// Program.cs
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("api", opt =>
    {
        opt.Window = TimeSpan.FromMinutes(1);
        opt.PermitLimit = 100;
        opt.QueueLimit = 10;
    });
});

// Uso en endpoints
app.MapGroup("/api/cars")
   .RequireRateLimiting("api");
```

#### 2. API Versioning

**Tiempo:** 20 min | **Impacto:** Medio

```csharp
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
});

// Endpoints
app.MapGroup("/api/v1/cars").MapCarsEndpointsV1();
app.MapGroup("/api/v2/cars").MapCarsEndpointsV2();
```

---

### 🟡 Media Prioridad

#### 3. Audit Trail Completo

**Tiempo:** 1-2 hrs | **Impacto:** Medio

```csharp
public interface IAuditable
{
    string CreatedBy { get; set; }
    DateTime CreatedAt { get; set; }
    string? ModifiedBy { get; set; }
    DateTime? ModifiedAt { get; set; }
}

// Interceptor EF Core
public class AuditInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(...)
    {
        foreach (var entry in context.ChangeTracker.Entries<IAuditable>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedBy = _currentUser.Id;
                entry.Entity.CreatedAt = DateTime.UtcNow;
            }
            // ...
        }
    }
}
```

#### 4. Polly para Resiliencia

**Tiempo:** 30 min | **Impacto:** Medio

```csharp
// Para HTTP clients externos
services.AddHttpClient<IBlobStorageService>()
    .AddPolicyHandler(Policy
        .Handle<HttpRequestException>()
        .WaitAndRetryAsync(3, i => TimeSpan.FromSeconds(i * 2)));
```

#### 5. Idempotencia en Event Handlers

**Tiempo:** 1 hr | **Impacto:** Medio

```csharp
// Tabla para tracking de mensajes procesados
public class ProcessedMessage
{
    public Guid MessageId { get; set; }
    public DateTime ProcessedAt { get; set; }
}

// En handler
if (await _context.ProcessedMessages.AnyAsync(m => m.MessageId == eventId))
    return; // Ya procesado
```

---

### 🟢 Baja Prioridad

#### 6. Hangfire Dashboard (opcional)

Migrar de Quartz a Hangfire para tener UI de jobs visible.

#### 7. Soft Delete

```csharp
public interface ISoftDeletable
{
    bool IsDeleted { get; set; }
    DateTime? DeletedAt { get; set; }
}

// Query filter global
modelBuilder.Entity<Car>().HasQueryFilter(c => !c.IsDeleted);
```

#### 8. Specification Pattern

Para queries complejas reutilizables.

---

## Tests Pendientes

| Tipo | Cobertura Actual | Meta |
|------|------------------|------|
| Unit Tests | ? | 80% Domain/Application |
| Integration Tests | ? | CRUD + Auth flows |
| E2E Tests | ❌ | Happy paths críticos |

### Herramientas Recomendadas

- **Unit:** xUnit + NSubstitute
- **Integration:** Testcontainers + WebApplicationFactory
- **E2E:** Playwright (sincronizado con frontend)

---

## Próximos Pasos Recomendados

1. [ ] Agregar Rate Limiting (15 min)
2. [ ] Agregar API Versioning (20 min)
3. [ ] Implementar Audit Trail (1-2 hrs)
4. [ ] Verificar cobertura de tests
5. [ ] Documentar endpoints en Swagger

---

## Notas Técnicas

> **Quartz vs Hangfire:** Actualmente usamos Quartz para el Outbox Pattern. Funciona bien, pero Hangfire ofrece un dashboard visual útil para debugging. Migrar solo si se necesita visualización.

> **Microservicios:** El diseño actual es Monolito Modular. Cada bounded context puede extraerse a microservicio si escala. Por ahora, mantener monolito.
