# Application Layer - Agent Context

## Purpose

The Application layer orchestrates use cases using CQRS pattern with MediatR. Contains Commands (writes) and Queries (reads) with their handlers.

---

## CQRS Pattern

```
┌─────────────┐     ┌─────────────┐
│   Command   │     │    Query    │
│  (Write)    │     │   (Read)    │
└──────┬──────┘     └──────┬──────┘
       │                   │
       ▼                   ▼
┌─────────────┐     ┌─────────────┐
│   Handler   │     │   Handler   │
│ (Mutates)   │     │ (Returns)   │
└──────┬──────┘     └──────┬──────┘
       │                   │
       ▼                   ▼
┌─────────────┐     ┌─────────────┐
│ Repository  │     │  DbContext  │
│  (Write)    │     │  (ReadOnly) │
└─────────────┘     └─────────────┘
```

---

## Folder Structure

```
Application/
├── Cars/
│   ├── Commands/
│   │   ├── CreateCar/
│   │   │   ├── CreateCarCommand.cs
│   │   │   ├── CreateCarHandler.cs
│   │   │   └── CreateCarValidator.cs
│   │   ├── UpdateCar/
│   │   └── DeleteCar/
│   └── Queries/
│       ├── GetCar/
│       │   ├── GetCarQuery.cs
│       │   ├── GetCarHandler.cs
│       │   └── CarResponse.cs
│       └── ListCars/
├── Clients/
├── Sales/
├── Common/
│   ├── Behaviors/          # MediatR pipeline
│   │   ├── ValidationBehavior.cs
│   │   └── LoggingBehavior.cs
│   └── Interfaces/
│       └── IApplicationDbContext.cs
└── DependencyInjection.cs
```

---

## Command Example

```csharp
// Command
public sealed record CreateCarCommand(
    string Patente,
    decimal Price,
    Guid BrandId,
    Guid ModelId
) : IRequest<Result<Guid>>;

// Handler
internal sealed class CreateCarHandler(
    ICarRepository repository,
    IUnitOfWork unitOfWork
) : IRequestHandler<CreateCarCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreateCarCommand request, 
        CancellationToken ct)
    {
        var car = Car.Create(
            LicensePlate.Create(request.Patente),
            Money.FromARS(request.Price),
            request.BrandId
        );
        
        repository.Add(car);
        await unitOfWork.SaveChangesAsync(ct);
        
        return car.Id;
    }
}
```

---

## Query Example

```csharp
// Query
public sealed record GetCarQuery(Guid Id) : IRequest<Result<CarResponse>>;

// Response DTO
public sealed record CarResponse(
    Guid Id,
    string Patente,
    decimal Price,
    string Brand,
    string Model
);

// Handler
internal sealed class GetCarHandler(
    IApplicationDbContext context
) : IRequestHandler<GetCarQuery, Result<CarResponse>>
{
    public async Task<Result<CarResponse>> Handle(
        GetCarQuery request, 
        CancellationToken ct)
    {
        var car = await context.Cars
            .Where(c => c.Id == request.Id)
            .Select(c => new CarResponse(
                c.Id,
                c.Patente.Value,
                c.Price.Amount,
                c.Brand.Name,
                c.Model.Name
            ))
            .FirstOrDefaultAsync(ct);
            
        return car ?? Result.Failure<CarResponse>(CarErrors.NotFound);
    }
}
```

---

## Validation with FluentValidation

```csharp
public sealed class CreateCarValidator : AbstractValidator<CreateCarCommand>
{
    public CreateCarValidator()
    {
        RuleFor(x => x.Patente)
            .NotEmpty()
            .MinimumLength(6)
            .WithMessage("Patente inválida");
            
        RuleFor(x => x.Price)
            .GreaterThan(0)
            .WithMessage("El precio debe ser mayor a 0");
    }
}
```

---

## Rules

- ✅ One file per Command/Query
- ✅ Handlers are `internal sealed`
- ✅ Use `Result<T>` pattern, not exceptions
- ✅ Validators alongside Commands
- ❌ NO direct database access from Commands (use repositories)
- ❌ NO business logic here (belongs in Domain)
