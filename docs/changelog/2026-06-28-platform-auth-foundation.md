# Platform Auth Foundation — 2026-06-28

## Summary

Backend foundation for the SaaS Super Admin panel (PR1 of `saas-super-admin`).

## Changes

### Domain
- `UserRole.SuperAdmin = 5` — new enum value for platform operators
- `User.CreateSuperAdmin()` — factory method for seeding the platform admin user
- `DealerSettings` — added `CreatedAt`, `IsActive`, `SuspendedAt`, `SuspendReason`, `RowVersion`
- `DealerSettings.Suspend()` / `Activate()` — domain methods with idempotent behavior
- `DealerSuspendedDomainEvent`, `DealerReactivatedDomainEvent` — new domain events

### Infrastructure
- `TokenProvider` — emits `platform_role: super_admin` JWT claim for SuperAdmin users
- `CurrentTenantService` — positive-claim `HasTenant` gate (checks `platform_role` first)
- `NoTenantServiceProductionGuard` — startup assertion prevents `NoTenantService` in production
- `DealerSuspensionMiddleware` — returns HTTP 403 for requests from suspended dealers
- Migrations: `20260628_AddDealerSuspensionColumns`, `20260629_AddDealerCreatedAt`
- `UsersSeeder.SeedSuperAdminAsync()` — seeds the SuperAdmin user with `platform:*` permissions

### Application / Platform
- `GetAllDealers` query — lists all dealers (IgnoreQueryFilters, ordered by CreatedAt desc)
- `SuspendDealer` command — suspends a dealer with reason + ETag concurrency check
- `ActivateDealer` command — reactivates a suspended dealer with ETag check
- `GetPlatformMetrics` query — returns TotalDealers, TotalUsers, MRR stub

### Web.Api
- `GET /api/platform/dealers` — requires `platform:dealers:read`
- `POST /api/platform/dealers/{id}/suspend` — requires `platform:dealers:write`
- `POST /api/platform/dealers/{id}/activate` — requires `platform:dealers:write`
- `GET /api/platform/metrics` — requires `platform:metrics:read`

## Tests

520 tests GREEN across 9 test assemblies (xUnit + FluentAssertions + NetArchTest).
