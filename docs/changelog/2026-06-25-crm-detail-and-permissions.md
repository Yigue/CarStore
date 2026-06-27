# CRM Detail View, Client Activity & Permission System (PR2)

**Date**: 2026-06-25  
**Branch**: sdd-implement  
**Change**: crm-hardening-2026-06-25 — PR2

---

## Summary

PR2 completes the CRM hardening cycle started in PR1 by wiring the activity timeline, permissions model, and fixing live numeric-status regressions.

---

## Backend Changes

### New Endpoints

| Method | Route | Permission | Description |
|--------|-------|------------|-------------|
| `PUT`  | `/api/v1/clients/{id}/notes` | `clients:write` | Update client notes; returns updated `ClientResponse` |
| `GET`  | `/api/v1/clients/{id}/activity` | `clients:read` | Paginated activity timeline from outbox (50 per page) |

### Application Layer

- `Application/Clients/GetActivity/` — new CQRS slice: `GetActivityQuery`, `GetActivityQueryHandler`, `ClientActivityResponse`
- `GetActivityQueryHandler` projects `OutboxMessages` filtered by `AggregateId + AggregateType='Client'`, ordered descending, paginated

### Infrastructure Fix

- `ApplicationDbContext.SaveChangesAsync` now populates `OutboxMessage.AggregateId`, `AggregateType`, and `DealerId` from the entity context (ADR-3 prerequisite). Previously these columns were always `NULL`.

### Feature Flags

- `CrmEnumV2: true` — string enum serialization is now the default; dual-format fallback path removed
- `CrmActivityTl: true` — activity timeline enabled by default
- `CrmClientNotes: true` — client notes endpoint enabled by default

---

## Frontend Changes

### Live Bug Fixes

- **W3 double mutation** (`LeadPipeline.tsx`): Removed spurious `updateLeadStatus` call after `convertLeadMutation` — the BE already sets status to `Ganado` in the convert handler
- **Numeric status regression** (`LeadPipeline.tsx`, `LeadDetailDialog.tsx`, `useLeads.ts`, `types/leads/models.ts`): `UpdateLeadStatusDto.newStatus` changed from `number` to `LeadStatus` string to comply with `JsonStringEnumConverter`

### Permission System

- `src/lib/permissions.ts` — `PERMISSIONS` const + `PermissionString` type (source of truth matching BE policy names)
- `src/hooks/dashboard/usePermission.ts` — rewritten to accept `PermissionString` and return `{ hasPermission, isLoading }` (supports hydration loading state)
- `src/types/auth/models.ts` — added missing permissions: `clients:delete`, `leads:read`, `leads:write`, `leads:archive`

### Client Detail View

- `ClientDetailView.tsx` — `TimelineTab` now fetches from `GET /clients/{id}/activity` via `useClientActivity`; `NotesTab` uses `useUpdateClientNotes` with 500ms debounce autosave + optimistic update + rollback
- `hooks/clients/useUpdateClientNotes.ts` — new mutation hook with optimistic update, rollback, and activity timeline invalidation on success
- `hooks/clients/useClientActivity.ts` — updated to handle `ClientActivityResponse { items, totalCount }`
- Mock data (`mockTimeline`, `mockQuotes`) replaced with real API calls

### Pipeline Hardening

- `LeadDetailDialog.tsx` — `window.confirm` replaced with shadcn `AlertDialog`; `num` field removed from `STATUS_OPTIONS`; status sent as string enum
- `LeadToClientConversionDialog.tsx` — added `type` selector (`Individual` / `Corporate`) wired to `ConvertLeadDto.type`
- `services/clientService.ts` — `getClientActivity` returns `ClientActivityResponse`; `ClientActivityEntry` fields aligned with BE response

---

## Tests Added

### Backend

- `ApplicationTests/Clients/GetActivityQueryHandlerTests.cs` — 5 unit tests (empty, isolation, aggregate type filter, ordering, pagination)
- `WebApiTests/IntegrationTests/Clients/NotesAndActivityEndpointTests.cs` — 7 integration tests (200 response, 404, validation, null clear, outbox populated, activity after event)

### Frontend

- `npm run typecheck`: 0 errors
- `npm run lint`: 0 errors (warnings pre-existing)

---

## Breaking Changes

None. All changes are additive or fix silent regressions.

---

## Migration Required

No schema changes. PR1 migration added all required columns (`AggregateId`, `AggregateType`, `DealerId` on `OutboxMessages`).
