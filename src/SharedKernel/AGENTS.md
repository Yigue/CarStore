# SharedKernel Layer - Agent Context

## Purpose

The SharedKernel contains common abstractions and base classes used across all layers. This is the foundation of the architecture.

---

## Contents

| Component | Purpose |
|-----------|---------|
| `Entity.cs` | Base class for all entities |
| `AggregateRoot.cs` | Base for aggregate roots |
| `ValueObject.cs` | Base for value objects |
| `Result.cs` | Result pattern for error handling |
| `Error.cs` | Error type definition |
| `IDomainEvent.cs` | Domain event marker interface |
| `IRepository.cs` | Generic repository interface |
| `IUnitOfWork.cs` | Unit of work interface |

---

## Base Classes

### Entity

```csharp
public abstract class Entity
{
    public Guid Id { get; protected set; }
    
    private readonly List<IDomainEvent> _domainEvents = [];
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();
    
    protected void RaiseDomainEvent(IDomainEvent domainEvent) 
        => _domainEvents.Add(domainEvent);
    
    public void ClearDomainEvents() => _domainEvents.Clear();
}
```

### Result Pattern

```csharp
public class Result<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public Error? Error { get; }
    
    public static Result<T> Success(T value) => new(true, value, null);
    public static Result<T> Failure(Error error) => new(false, default, error);
    
    public TResult Match<TResult>(
        Func<T, TResult> onSuccess,
        Func<Error, TResult> onFailure)
        => IsSuccess ? onSuccess(Value!) : onFailure(Error!);
}

public sealed record Error(string Code, string Message)
{
    public static Error None => new(string.Empty, string.Empty);
    public static Error Unexpected => new("Unexpected", "An unexpected error occurred");
}
```

---

## Folder Structure

```
SharedKernel/
├── Entity.cs
├── AggregateRoot.cs
├── ValueObject.cs
├── Result.cs
├── Error.cs
├── IDomainEvent.cs
├── IRepository.cs
├── IUnitOfWork.cs
└── SharedKernel.csproj
```

---

## Rules

- ✅ Keep abstractions minimal and focused
- ✅ No dependencies on other layers
- ✅ All classes/interfaces are public
- ❌ NO business logic specific to CarStore
- ❌ NO EF Core or external dependencies
