# PR1 — CRM Data Contract (2026-06-25)

## Summary

Establishes the foundational data contract for CRM hardening across BE and FE.
Implements soft-delete, full `ClientResponse` projection, permission rename, and
feature-flag infrastructure without breaking existing API consumers.

## Backend Changes

### Domain (1.1)
- `Client`: Added `IsDeleted`, `DeletedAtUtc`, `DeletedBy`, `AcquisitionSource`, `AssignedAgentId` fields
- `Client.Delete(actorId)`: Idempotent soft-delete; raises `ClientSoftDeletedDomainEvent`
- `Client.Restore(actorId)`: Returns `Result.Failure` if not deleted; raises `ClientRestoredDomainEvent`
- `Client.UpdateNotes(notes, actorId)`: Max 2000 chars; raises `ClientNotesUpdatedDomainEvent`
- New enum `AcquisitionSource` (Web/Portal/Referral/Otro)
- New `ClientErrors`: `AlreadyDeleted`, `NotDeleted`, `NotesTooLong`

### Application (1.2)
- `ClientResponse` DTO expanded: `firstName/lastName/fullName/city/zipCode/documentNumber/notes/acquisitionSource/assignedAgentId/totalSalesAmount/purchaseHistory/lastPurchaseDate/tenantId/isDeleted`
- New `UpdateNotes` command (Application/Clients/UpdateNotes/)
- `ClientResponseMapper` updated to map all new fields

### Infrastructure (1.3)
- Migration `AddClientSoftDeleteAndOutboxColumns`:
  - `clients`: `is_deleted`, `deleted_at_utc`, `deleted_by`, `acquisition_source`, `assigned_agent_id`, `ix_clients_is_deleted`
  - `outbox_messages`: `aggregate_id`, `aggregate_type`, `dealer_id`, `ix_outbox_type_occurred`, `ix_outbox_dealer`
  - `clients.notes`: max length 1000 → 2000
- Soft-delete global query filter folded into `ApplicationDbContext.OnModelCreating` (ADR-2)
- **Q1A Permission rename**:
  - `clients:write` replaces `clients:create` + `clients:update` (aliases kept for 1 PR)
  - `leads:write` replaces `leads:create` + `leads:update` (aliases kept for 1 PR)
  - `leads:archive` added
- `UsersSeeder`: new permissions added to reconciler list
- `FeatureFlagsOptions` + DI registered; `appsettings.json` section added
- `CRM_ENUM_V2=false`, `CRM_SOFT_DELETE=true`, `CRM_BULK_EXPORT=false`

### Tests (1.4)
- `DomainTests/Clients/ClientAggregateTests.cs`: 8 invariant tests
- `ApplicationTests/Clients/UpdateNotesCommandHandlerTests.cs`: 4 tests
- `ApplicationTests/Clients/CreateClientCommandHandlerTests.cs`: 4 tests
- `ApplicationTests/Clients/GetAllClientsQueryHandlerTests.cs`: 5 tests
- `WebApiTests/IntegrationTests/Clients/ClientsJsonShape.IntegrationTests.cs`: 3 tests
- `WebApiTests/IntegrationTests/Clients/ConvertLeadAcceptsType.cs`: 2 tests

## Frontend Changes

### Types (1.5.1)
- `types/clients/models.ts`: Replaced `name?` with `firstName/lastName/fullName`; added all PR1 DTO fields; `purchaseHistory: PurchaseHistoryEntry[]`
- `types/clients/enums.ts`: Enum values are PascalCase strings matching BE `JsonStringEnumConverter`
- `types/leads/models.ts`: Added `type?: CLIENT_TYPE` to `ConvertLeadDto`
- `data/clients.ts`: Updated mock data to new field names

### Services (1.5.2)
- `services/clientService.ts`: Removed `typeMap`/`statusMap`; added `getClientActivity()` and `updateClientNotes()`

### Hooks (1.5.3/1.5.4/1.5.7)
- `hooks/useFeatureFlag.ts`: New — reads `NEXT_PUBLIC_<FLAG>` env vars
- `hooks/clients/useClientActivity.ts`: New — flag-gated on `CRM_ACTIVITY_TL`

### Tests (1.5.5)
- `tests/integration/leadService.test.ts`: Fixed URLs `/leads` → `/api/v1/leads*`
- `tests/integration/clientService.test.ts`: Added `getClientActivity` and `updateClientNotes` cases; removed `as any`

## Breaking Changes

None within PR1. `clients:create`, `clients:update`, `leads:create`, `leads:update` are aliased via `[Obsolete]` constants for 1 PR.

## ADR References

- ADR-1: PascalCase enums via `JsonStringEnumConverter`
- ADR-2: Soft-delete global query filter in `OnModelCreating`
- ADR-3: Outbox columns `AggregateId/AggregateType/DealerId` added (Q3A)
- ADR-6: `IOptions<FeatureFlagsOptions>` for feature flags
