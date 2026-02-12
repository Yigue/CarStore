# Domain Layer - Agent Context

## Purpose

The Domain layer contains the core business logic, entities, value objects, and domain events. This layer has **zero dependencies** on other layers.

---

## Key Principles

1. **Rich Domain Model**: Entities contain behavior, not just data
2. **Encapsulation**: Private setters, factory methods
3. **Immutable Value Objects**: `Money`, `LicensePlate`, `Email`
4. **Domain Events**: Raised when something meaningful happens

---

## Aggregates

| Aggregate | Root Entity | Related Entities |
|-----------|-------------|------------------|
| **Car** | `Car` | `Brand`, `Model` |
| **Client** | `Client` | `ContactInfo` |
| **Sale** | `Sale` | `SaleItem`, `Payment` |
| **Lead** | `Lead` | `LeadNote` |

---

## Patterns Used

### Entity Base Class

```csharp
public abstract class Entity
{
    public Guid Id { get; protected set; }
    private readonly List<IDomainEvent> _domainEvents = [];
    
    protected void RaiseDomainEvent(IDomainEvent domainEvent)
        => _domainEvents.Add(domainEvent);
}
```

### Value Object

```csharp
public sealed record Money(decimal Amount, string Currency)
{
    public static Money FromARS(decimal amount) => new(amount, "ARS");
    public static Money FromUSD(decimal amount) => new(amount, "USD");
}
```

### Factory Methods

```csharp
public static Car Create(LicensePlate patente, Money price, Guid brandId)
{
    var car = new Car { Patente = patente, Price = price, BrandId = brandId };
    car.RaiseDomainEvent(new CarCreatedDomainEvent(car.Id));
    return car;
}
```

---

## Folder Structure

```
Domain/
├── Cars/
│   ├── Car.cs              # Aggregate root
│   ├── CarStatus.cs        # Enum
│   ├── Events/
│   │   └── CarCreatedDomainEvent.cs
│   └── ValueObjects/
│       └── LicensePlate.cs
├── Clients/
│   ├── Client.cs
│   └── ValueObjects/
│       └── ContactInfo.cs
├── Sales/
│   └── Sale.cs
└── Common/
    ├── Money.cs
    └── Email.cs
```

---

## Rules

- ✅ NO dependencies on Application, Infrastructure, or Web.Api
- ✅ Use factory methods for entity creation
- ✅ Raise domain events for significant state changes
- ✅ Value objects are immutable (use `record`)
- ❌ NO EF Core attributes (use Fluent API in Infrastructure)
- ❌ NO public setters on entities
