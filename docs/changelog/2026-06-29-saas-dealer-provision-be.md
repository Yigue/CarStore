# PR1 BE — SaaS Dealer Provisioning (2026-06-29)

## Summary

First PR of the SaaS dealer onboarding feature. Adds the backend provisioning
pipeline: a validated command that atomically creates a DealerSettings row and
its first Admin user inside a single EF Core transaction, a subdomain
availability check endpoint, a notification service for the welcome email, and
a UNIQUE partial index on `HostName` to prevent concurrent slug races.

## Backend Changes

### Domain (1.1)
- `DealerSettings/Events/DealerProvisionedDomainEvent` — raised after a
  successful provision; carries `DealerId`, `AdminUserId`, `AdminEmail`,
  `Subdomain`, and `DashboardUrl`.
- `DealerSettingsErrors.HostNameNotUnique` (`Error.Conflict`, maps to 409) and
  `DealerSettingsErrors.ReservedSubdomain(name)` (`Error.Validation`).
- XML doc on `DealerSettings.HostName` updated to note the DB unique index.

### Application (1.2)
- `ProvisionDealerCommand` + `ProvisionDealerResponse` — CQRS command/response.
- `ProvisionDealerCommandValidator` — validates dealer name (2–200 chars),
  subdomain (3–32 chars, `^[a-z0-9](?:[a-z0-9-]{1,30}[a-z0-9])?$` regex,
  reserved-list check against 16 system slugs), admin email, password
  (min 10 + upper + lower + digit + non-alphanumeric), and name fields (1–100).
- `ProvisionDealerCommandHandler` — opens an EF Core transaction, mints the
  `DealerSettings` row and `User` with the same Guid per ADR-1, commits, and
  publishes the domain event. Catches `DbUpdateException` for HostName unique
  violations and returns `Conflict`.
- `CheckSubdomainAvailabilityQuery` + handler — returns `Available`/`Reserved`
  status; checks the in-memory blocklist first, then the DB.
- `ReservedSubdomains` — `static readonly HashSet<string>` of 16 reserved slugs
  (admin, api, www, app, mail, support, dashboard, static, cdn, auth, help,
  status, billing, root, system, internal).
- `IDealerNotificationService` + `DealerProvisionedDomainEventHandler` — sends
  the welcome email after successful provisioning via the existing email service
  (NoOp when SMTP is not configured).

### Infrastructure (1.3)
- `DealerSettingsConfiguration.HasIndex(s => s.HostName).IsUnique().HasFilter(...)`
  — PostgreSQL partial unique index ignoring NULLs for legacy seed rows.
- Migration `20260629225855_AddDealerSettingsHostNameUniqueIndex`.
- `DealerNotificationService` — builds `https://{subdomain}.carstore.com/dashboard`
  URL; SMTP errors are caught and logged (D3 isolation).
- DI registration: `IDealerNotificationService` scoped, `DbContext` for
  `BeginTransactionAsync` access.

### Endpoints (1.4)
- `POST /api/v1/dealers/provision` — anonymous; returns 201 with
  `{dealerId, adminUserId, subdomain}`; 400 on validation errors; 409 on
  duplicate subdomain.
- `GET /api/v1/dealers/check-subdomain?subdomain={slug}` — anonymous; returns
  200 with `{available, reason, reserved}`; `Cache-Control: no-store`.
- Both registered via `IEndpoint` auto-discovery, tagged with `Tags.Dealers`.

### Tests (1.5)
- **Unit** (ApplicationTests):
  - `ProvisionDealerCommandValidatorTests` — table-driven: valid command passes;
    reserved slugs (all 16); malformed shapes; length violations; weak passwords
    (7 variants); invalid emails; empty names.
  - `ProvisionDealerCommandHandlerTests` — happy path creates DealerSettings +
    Admin user; same-Guid assertion for PK/FK; rollback on user write failure;
    domain event published exactly once; not published on failure; lowercase
    subdomain enforcement.
  - `CheckSubdomainAvailabilityQueryHandlerTests` — unused → Available; taken
    → NotAvailable; reserved (5 slugs, case-insensitive); Reserved flag + reason.
- **Integration** (WebApiTests):
  - `ProvisionEndpointTests` — valid body returns created; weak password returns
    400; reserved slug returns 400; malformed slug returns 400; sequential
    duplicate subdomain returns 409 with exactly one row persisted; anonymous
    access (no 401).
  - `CheckSubdomainEndpointTests` — available; reserved; missing param returns
    400; `Cache-Control: no-store` header; anonymous access.
  - `ProvisionConcurrencyTests` — **skipped for SQLite** (requires PostgreSQL
    connection pooling); documented manual verification procedure.

## Files Changed

| File | Action |
|------|--------|
| `src/Domain/DealerSettings/Events/DealerProvisionedDomainEvent.cs` | Created |
| `src/Domain/DealerSettings/DealerSettingsErrors.cs` | Modified |
| `src/Domain/DealerSettings/DealerSettings.cs` | Modified (XML doc) |
| `src/Application/Common/ReservedSubdomains.cs` | Created |
| `src/Application/Dealers/Provision/ProvisionDealerCommand.cs` | Created |
| `src/Application/Dealers/Provision/ProvisionDealerResponse.cs` | Created |
| `src/Application/Dealers/Provision/ProvisionDealerCommandValidator.cs` | Created |
| `src/Application/Dealers/Provision/ProvisionDealerCommandHandler.cs` | Created |
| `src/Application/Abstractions/Messaging/IDealerNotificationService.cs` | Created |
| `src/Application/Dealers/Provision/DealerProvisionedDomainEventHandler.cs` | Created |
| `src/Application/Dealers/CheckSubdomain/CheckSubdomainAvailabilityQuery.cs` | Created |
| `src/Application/Dealers/CheckSubdomain/SubdomainAvailabilityResponse.cs` | Created |
| `src/Application/Dealers/CheckSubdomain/CheckSubdomainAvailabilityQueryHandler.cs` | Created |
| `src/Infrastructure/Database/Configurations/DealerSettingsConfiguration.cs` | Modified |
| `src/Infrastructure/Migrations/20260629225855_AddDealerSettingsHostNameUniqueIndex.cs` | Created |
| `src/Infrastructure/Dealers/DealerNotificationService.cs` | Created |
| `src/Infrastructure/DependencyInjection.cs` | Modified |
| `src/Web.Api/Endpoints/Tags.cs` | Modified |
| `src/Web.Api/Endpoints/Dealers/Provision.cs` | Created |
| `src/Web.Api/Endpoints/Dealers/CheckSubdomain.cs` | Created |
| `tests/ApplicationTests/Dealers/Provision/ProvisionDealerCommandValidatorTests.cs` | Created |
| `tests/ApplicationTests/Dealers/Provision/ProvisionDealerCommandHandlerTests.cs` | Created |
| `tests/ApplicationTests/Dealers/CheckSubdomain/CheckSubdomainAvailabilityQueryHandlerTests.cs` | Created |
| `tests/WebApiTests/Dealers/ProvisionEndpointTests.cs` | Created |
| `tests/WebApiTests/Dealers/ProvisionConcurrencyTests.cs` | Created |
| `tests/WebApiTests/Dealers/CheckSubdomainEndpointTests.cs` | Created |
| `tests/WebApiTests/CustomWebApplicationFactory.cs` | Modified |

## Commits

```
27850d2 feat(backend): add ProvisionDealerCommand + validator with reserved subdomain blocklist
18f598f feat(backend): add DealerProvisionedDomainEvent + HostName errors
6772dae feat(backend): add ProvisionDealerCommandHandler with atomic transaction + same-Guid provision ctor
e64437e feat(backend): add CheckSubdomainAvailabilityQuery + handler with reserved-list + DB lookup
69f769b feat(backend): add DealerNotificationService + welcome email domain event handler
fc6b2f6 feat(backend): add UNIQUE partial index on DealerSettings.HostName
9a07053 feat(backend): add /dealers/provision + /dealers/check-subdomain endpoints
a973fb3 feat(backend): add HostName unique violation handler + endpoint integration tests
```

## Next Steps

- PR2 FE: onboarding wizard UI with Zod validation, Zustand store, and
  React Hook Form wiring.
- Postman collection update with `/dealers/provision` and `/dealers/check-subdomain`.
