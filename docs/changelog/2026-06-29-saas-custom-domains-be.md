# PR1 — SaaS Custom Domains (Backend) (2026-06-29)

## Summary

Multi-tenant SaaS custom domain infrastructure for the backend. Every dealer is
now identified by a public `Slug` on `{slug}.carstore.com` with database-level
uniqueness guarantees. The critical cross-tenant data leak in
`CurrentTenantService` (first-row fallback on host miss) is eliminated with a
strict null-on-miss policy and a production startup assertion.

## Domain

- `DealerSettings`: added `Slug` (required, ≤63 chars) + `IsActive` (default
  `true`) with private setters (task 1.1.1).
- `DealerSettings.ChangeSlug(newSlug, newHostname)`: RFC 1035 validation —
  lowercase ASCII + hyphens only, no leading/trailing/consecutive hyphens,
  1–63 chars per label, ≤253 chars total for the FQDN (task 1.1.2).

## Infrastructure

### Tenant Safety (CRITICAL — ADR-1)

- `CurrentTenantService`: deleted the old `FirstOrDefault` first-row fallback
  on host miss. Replaced with: JWT claim → header chain
  (`X-Tenant-Host → Origin → Host`) → Development-only convenience fallback →
  `Guid.Empty` (tasks 1.4.1, 1.4.2). Critical log in Production on miss.
- `TenantFallbackOptions` (NEW): options record for
  `Tenant:DevFallbackDealerId` (task 1.2.3).
- `Web.Api/Program.cs`: startup assertion mirrors JWT-secret check — throws
  `InvalidOperationException` if `Tenant:DevFallbackDealerId` is set outside
  Development (task 1.4.3).
- `appsettings.Production.json`: added `Cors:AllowedHostSuffixes:
  [".carstore.com"]` (task 1.4.4).

### Configuration & Migrations

- `DealerSettingsConfiguration.cs`: `HostName` (max 253, unique, partial
  filter), `Slug` (max 63, unique, partial filter), `IsActive` (default
  `true`), composite partial index `(HostName, IsActive)` (tasks 1.2.1, 1.2.2).
- `BackfillDealerSettingsHostName` migration: lengthens `host_name` to
  `varchar(253)`, adds `slug` + `is_active` columns, idempotent slugify of
  `DealerName` for all existing rows (task 1.3.1).
- `AddDealerSettingsHostNameUniqueIndex` migration: `SET NOT NULL` on
  `host_name` + `slug`, `CREATE UNIQUE INDEX CONCURRENTLY` for both, partial
  lookup index `(host_name) WHERE is_active = true` (task 1.3.2).

## Tests

- `DomainTests/DealerSettings/DealerSettingsTenantIdentityTests.cs`: 9 tests
  covering Slug defaults, `ChangeSlug` RFC 1035 validation (7 invalid cases,
  3 valid) (tasks 1.1.1, 1.1.2).
- `InfrastructureTests/Tenancy/CurrentTenantServiceTests.cs`: 4 smoke tests
  for the refactored constructor (task 1.6.1).
- `InfrastructureTests/Tenancy/CurrentTenantServiceTenantSafetyTests.cs`: 8
  tests — host miss never leaks; dev fallback gated by `IsDevelopment()`;
  Production/Staging host miss → `Guid.Empty`; empty headers → `Guid.Empty`;
  case-insensitive host match (tasks 1.6.1, 1.6.2, 1.6.3).
- `ArchitectureTests/Layers/TenantIndexesTests.cs`: 3 tests — EF model
  snapshot declares UNIQUE on `HostName`, UNIQUE on `Slug`, and partial
  composite `(HostName, IsActive)` (tasks 1.6.5, 1.6.6).

## Commits

| Commit | Message |
|--------|---------|
| `b9e8389` | `feat(backend): add Slug + IsActive to DealerSettings with RFC 1035 validation` |
| `880afc4` | `fix(backend): return null on tenant host miss (critical leak)` |
| (next) | `feat(backend): configure HostName/Slug indexes on DealerSettings with EF snapshot tests` |
| (next) | `feat(backend): add backfill and unique index migrations for DealerSettings host/slug` |
