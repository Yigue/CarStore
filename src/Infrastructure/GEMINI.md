# Infrastructure Layer - Agent Context

## Purpose

The Infrastructure layer implements external concerns: database access, external services, file storage. It depends on Domain and Application.

---

## Responsibilities

| Component | Purpose |
|-----------|---------|
| **Database** | EF Core context, migrations |
| **Repositories** | Repository pattern implementations |
| **Services** | External APIs, blob storage |
| **Caching** | Redis/memory cache |
| **Identity** | JWT authentication |

---

## Folder Structure

```
Infrastructure/
├── Database/
│   ├── ApplicationDbContext.cs
│   ├── Migrations/
│   └── Configurations/
│       ├── CarConfiguration.cs
│       ├── ClientConfiguration.cs
│       └── SaleConfiguration.cs
│
├── Repositories/
│   ├── CarRepository.cs
│   ├── ClientRepository.cs
│   └── SaleRepository.cs
│
├── Services/
│   ├── BlobStorageService.cs
│   ├── EmailService.cs
│   └── CacheService.cs
│
├── Identity/
│   ├── JwtProvider.cs
│   └── PasswordHasher.cs
│
├── Outbox/                    # Future: Outbox pattern
│   ├── OutboxMessage.cs
│   └── OutboxProcessor.cs
│
└── DependencyInjection.cs
```

---

## EF Core Configuration (Fluent API)

```csharp
public class CarConfiguration : IEntityTypeConfiguration<Car>
{
    public void Configure(EntityTypeBuilder<Car> builder)
    {
        builder.ToTable("cars");
        
        builder.HasKey(c => c.Id);
        
        // Value Object mapping
        builder.ComplexProperty(c => c.Patente, b =>
        {
            b.Property(p => p.Value)
                .HasColumnName("patente")
                .HasMaxLength(10)
                .IsRequired();
        });
        
        builder.ComplexProperty(c => c.Price, b =>
        {
            b.Property(p => p.Amount)
                .HasColumnName("price")
                .HasPrecision(18, 2);
            b.Property(p => p.Currency)
                .HasColumnName("currency")
                .HasMaxLength(3);
        });
        
        // Relationships
        builder.HasOne(c => c.Brand)
            .WithMany()
            .HasForeignKey(c => c.BrandId);
            
        // Indexes
        builder.HasIndex(c => c.Patente)
            .IsUnique();
    }
}
```

---

## Repository Implementation

```csharp
internal sealed class CarRepository(ApplicationDbContext context) 
    : ICarRepository
{
    public void Add(Car car) => context.Cars.Add(car);
    
    public void Update(Car car) => context.Cars.Update(car);
    
    public void Remove(Car car) => context.Cars.Remove(car);
    
    public async Task<Car?> GetByIdAsync(Guid id, CancellationToken ct)
        => await context.Cars
            .Include(c => c.Brand)
            .Include(c => c.Model)
            .FirstOrDefaultAsync(c => c.Id == id, ct);
            
    public async Task<Car?> GetByPatenteAsync(string patente, CancellationToken ct)
        => await context.Cars
            .FirstOrDefaultAsync(c => c.Patente.Value == patente, ct);
}
```

---

## Dependency Injection

```csharp
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Database
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Database")));
        
        // Repositories
        services.AddScoped<ICarRepository, CarRepository>();
        services.AddScoped<IClientRepository, ClientRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        
        // Services
        services.AddScoped<IBlobStorageService, AzureBlobStorageService>();
        services.AddScoped<IJwtProvider, JwtProvider>();
        
        // Cache
        services.AddStackExchangeRedisCache(options =>
            options.Configuration = configuration.GetConnectionString("Redis"));
        
        return services;
    }
}
```

---

## Migration Commands

```bash
# Add migration
dotnet ef migrations add <Name> -p src/Infrastructure -s src/Web.Api

# Apply migrations
dotnet ef database update -p src/Infrastructure -s src/Web.Api

# Remove last migration
dotnet ef migrations remove -p src/Infrastructure -s src/Web.Api

# Generate SQL script
dotnet ef migrations script -p src/Infrastructure -s src/Web.Api -o migration.sql
```

---

## Rules

- ✅ Use Fluent API for EF configuration, NOT attributes
- ✅ One Configuration file per entity
- ✅ Repositories are `internal sealed`
- ✅ Register all services in `DependencyInjection.cs`
- ❌ NO business logic (belongs in Domain)
- ❌ NO direct use of DbContext in handlers (use repositories or IApplicationDbContext)
