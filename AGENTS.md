# CarStore Backend - Agent Context

## Project Overview

Backend API for CarStore SaaS - a multi-tenant platform for car dealerships.

| Stack | Version |
|-------|---------|
| .NET | 8.0 |
| EF Core | 8.0 |
| PostgreSQL | 17 |
| MediatR | 12.x |
| Serilog | + Seq |

---

## Architecture: Clean Architecture + CQRS

```
┌─────────────────────────────────────────────┐
│                 Web.Api                     │  ← Endpoints, Middleware
├─────────────────────────────────────────────┤
│               Application                   │  ← Commands, Queries, Handlers
├─────────────────────────────────────────────┤
│                 Domain                      │  ← Entities, Value Objects, Events
├─────────────────────────────────────────────┤
│              Infrastructure                 │  ← EF Core, External Services
├─────────────────────────────────────────────┤
│              SharedKernel                   │  ← Common abstractions
└─────────────────────────────────────────────┘
```

---

## Domain Contexts

| Context | Description | Key Entities |
|---------|-------------|--------------|
| **Inventory** | Vehicle stock management | `Car`, `Brand`, `Model` |
| **CRM** | Lead tracking and client management | `Lead`, `Client` |
| **Sales** | Transactions and reservations | `Sale`, `Reservation` |
| **Finance** | Cash flow and expenses | `Transaction`, `Expense` |

---

## Project Structure

```
src/
├── Domain/                # Core business logic
│   ├── Cars/             # Aggregate: Car
│   ├── Clients/          # Aggregate: Client
│   ├── Sales/            # Aggregate: Sale
│   └── Common/           # Shared Value Objects
│
├── Application/           # Use cases (CQRS)
│   ├── Cars/
│   │   ├── Commands/     # CreateCar, UpdateCar, DeleteCar
│   │   └── Queries/      # GetCar, ListCars
│   ├── Clients/
│   └── Sales/
│
├── Infrastructure/        # External concerns
│   ├── Database/         # EF Core context, migrations
│   ├── Repositories/     # Repository implementations
│   └── Services/         # External services
│
├── SharedKernel/          # Cross-cutting abstractions
│   ├── Entity.cs
│   ├── Result.cs
│   └── IRepository.cs
│
└── Web.Api/               # HTTP layer
    ├── Endpoints/        # Minimal API endpoints
    ├── Middleware/       # Error handling, auth
    └── Program.cs        # Composition root

tests/
├── Domain.Tests/
├── Application.Tests/
└── Integration.Tests/
```

---

## Development Commands

```bash
# Build
dotnet build CleanArchitecture.sln

# Run
dotnet run --project src/Web.Api

# Test
dotnet test

# Add migration
dotnet ef migrations add <Name> -p src/Infrastructure -s src/Web.Api

# Apply migrations
dotnet ef database update -p src/Infrastructure -s src/Web.Api

# Run with Docker
cd ../CarStore-Platform/docker
docker compose up web-api -d
```

---

## Key Files

| File | Purpose |
|------|---------|
| `src/Web.Api/Program.cs` | Composition root |
| `src/Infrastructure/Database/ApplicationDbContext.cs` | EF Core context |
| `src/Domain/Cars/Car.cs` | Main aggregate root |
| `Directory.Packages.props` | Central package management |

---

## Skills

Use `carstore-backend` skill for detailed patterns on:

- Domain entities and value objects
- CQRS with MediatR
- EF Core configurations
- Minimal API endpoints

### Auto-invoke Skills

When performing these actions, ALWAYS invoke the corresponding skill FIRST:

| Action | Skill |
|--------|-------|
| Generic DRF patterns | `django-drf` |
| Writing Python tests with pytest | `pytest` |

---

## See Also

- [docs/architecture.md](docs/architecture.md) - Detailed architecture
- [docs/domain.md](docs/domain.md) - Domain model
- [docs/patterns.md](docs/patterns.md) - Design patterns
- [postman/](postman/) - API collection
