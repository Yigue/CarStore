# PR3 — CRM Search, Soft-Delete UX and Bulk Export (2026-06-25)

## Summary

Completes the CRM hardening trilogy. Adds server-side search and filtering on
the clients list, a full soft-delete / restore flow with a dedicated trash view,
CSV bulk export capped at 10k rows, and removes the obsolete
`clients:create` / `clients:update` permission aliases from the database.

## Backend Changes

### Domain (3.1)
- `Client` now implements `ISoftDeletable` — enables architecture-test reflection
  to assert that all soft-deletable entities have a `!IsDeleted` query filter.

### Application (3.1)
- New `Application/Clients/SoftDelete/SoftDeleteClientCommand` + handler:
  idempotent soft-delete via `IgnoreQueryFilters()` so re-deleting returns 200.
- `Application/Clients/Delete/DeleteClientCommandHandler`: same idempotent fix.
- `Application/Clients/GetAll/GetAllClientsQuery` + handler: filter builder for
  status / type / source / agent / date range / sales range; server-side search
  on `fullName` (collation-aware on Postgres; RemoveAccents fallback elsewhere);
  pagination (`Page`, `PageSize`) → `PaginatedResult<ClientResponse>`.
- `Application/Clients/GetDeleted/` (existing): paginated list of soft-deleted
  clients using `IgnoreQueryFilters()`.
- `Application/Clients/Restore/` (existing): 409 on not-deleted; `clients:delete`.
- `Application/Clients/Export/`:
  - `CsvRowWriter.cs`: RFC-4180 escaping + UTF-8 BOM.
  - `ExportClientsQuery` + handler: mirrors GetAll filters; `CountAsync` cap at
    10 000 rows → `413 Payload Too Large`; streams via `IAsyncEnumerable`.
  - `ClientExportRow.cs`, `ExportErrors.cs`.

### Infrastructure / API (3.2)
- Migration `20260627231940_DropLegacyCRMPermissions`: removes
  `clients:create` and `clients:update` rows from `user_permissions`.
- `UsersSeeder.cs`: admin seed list updated to `clients:read|write|delete`.
- `Endpoints/Clients/Delete.cs` → wired to `SoftDeleteClientCommand`.
- `Endpoints/Clients/GetDeleted.cs`: permission fixed to `clients:delete`.
- `Endpoints/Clients/Restore.cs`: `MapPut` → `MapPost`; `clients:delete`.
- `Endpoints/Clients/Export.cs` (new): `GET /api/v1/clients/export`; streams CSV
  via `Results.File`; returns 413 on limit exceeded.
- `Endpoints/Clients/Get.cs`: accepts full filter + pagination query params.
- `appsettings.json`: `CrmSoftDelete` and `CrmBulkExport` flags set to `true`.

### Tests (3.3)
- `ApplicationTests/Common/CsvRowWriterTests.cs` (9 tests).
- `ApplicationTests/Clients/SoftDeleteClientCommandHandlerTests.cs` (3 tests):
  soft-delete, idempotent re-delete, not-found.
- `ApplicationTests/Clients/ExportClientsQueryHandlerTests.cs` (4 tests):
  BOM presence, header row, id filter, limit constant.
- `ApplicationTests/Clients/GetAllClientsQueryHandlerTests.cs`: extended with
  filter, pagination, and search tests.
- `ArchitectureTests/Layers/QueryFilterTests.cs`: reflection asserts every
  `ISoftDeletable` entity has a `!IsDeleted` query filter.

## Frontend Changes

### Services (3.4)
- `clientService.ts`: added `softDeleteClient`, `restoreClient`,
  `getDeletedClients`, `exportClients` (returns `Blob` via `responseType: blob`),
  and `getIncompleteClients`.
- `PaginatedResult<T>` import from `@/types/cars`.

### Components (3.4–3.5)
- `clients-table-columns.tsx`: `accessorKey: "name"` → `accessorKey: "fullName"`.
- `ClientFilters.tsx` (new): search + status + type filter bar.
- `ClientsKPICards.tsx`: rewired to use `useClientStats`, `useRecentClients`,
  `useIncompleteClients` hooks — no longer computes from prop data.
- `ClientsPage.tsx`: Delete action wired to `useSoftDeleteClient`; KPI cards
  no longer receive `clients` prop.
- `TrashView.tsx` (new): paginated list of soft-deleted clients with restore.
- `app/(dashboard)/dashboard/clientes/papelera/page.tsx` (new): gated by
  `clients:delete` permission; renders `TrashView`.

### Hooks (3.4–3.5)
- `hooks/clients/useSoftDeleteClient.ts`: invalidates `['clients']` + `['clients', id]`.
- `hooks/clients/useRestoreClient.ts`: invalidates deleted list + client lists.
- `hooks/clients/useDeletedClients.ts`: paginated query for trash.
- `hooks/clients/useExportClients.ts`: mutation that triggers CSV download.
- `hooks/clients/useClientStats.ts`, `useRecentClients.ts`, `useTopClients.ts`,
  `useIncompleteClients.ts`: KPI data hooks.

### Tests (3.6)
- `tests/integration/clientService.test.ts`: added `softDeleteClient`,
  `restoreClient`, `getDeletedClients`, `exportClients` test cases.
- `tests/unit/clients/useSoftDeleteClient.spec.ts`: invalidation contract.
