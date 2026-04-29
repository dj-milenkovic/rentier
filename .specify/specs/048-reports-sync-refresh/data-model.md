# Data Model: 048-reports-sync-refresh

**Feature**: Reports Table Refresh After Sync
**Date**: 2025-07-15

## Entity Changes

**None.** This feature is a bug fix that modifies ViewModel behavior only. No entities, value objects, DTOs, or database schema changes are required.

## Affected Types (Read-Only Reference)

The following existing types are involved but **not modified**:

| Type | Layer | Role in Fix |
|------|-------|-------------|
| `ReportsViewModel` | Desktop | **Modified**: Add `LoadPageAsync()` call after sync success |
| `GetReportsQuery` | Application | Used as-is to re-query reports |
| `ReportsPageResult` | Application (DTO) | Used as-is to populate Rows |
| `ReportRowDto` | Application (DTO) | Used as-is for individual row data |
| `ReportRowViewModel` | Desktop | Used as-is via `ReportRowViewModel.From(dto)` |
| `SyncResult` | Application | Used as-is to determine sync success/failure |

## State Transitions

No new state transitions. The existing sync flow gains one additional step:

```text
Before (buggy):
  User clicks Sync → HandleSyncAsync → _syncHandler.HandleAsync() → Update SyncStatusMessage → DONE
  (Table still shows stale data)

After (fixed):
  User clicks Sync → HandleSyncAsync → _syncHandler.HandleAsync() → LoadPageAsync() → Update SyncStatusMessage → DONE
  (Table shows fresh data including newly synced reports)
```

## Validation Rules

No new validation rules. Existing `LoadPageAsync()` error handling applies.
