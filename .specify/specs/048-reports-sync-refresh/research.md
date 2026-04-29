# Research: 048-reports-sync-refresh

**Feature**: Reports Table Refresh After Sync
**Date**: 2025-07-15

## Research Items

### R-001: Root Cause of Missing Refresh

**Question**: Why do newly synced reports not appear in the table after sync completes?

**Finding**: The `HandleSyncAsync` method in `ReportsViewModel.cs` (lines 463–482) calls `_syncHandler.HandleAsync()` and updates `SyncStatusMessage` and `SyncProgressValue`, but **never calls `LoadPageAsync()`** to re-query the reports list from the database.

By contrast, both `ImportAsync` (line 391) and `DeleteAsync` (line 421) call `await LoadPageAsync(ct)` after their respective operations succeed. The sync handler simply omits this step.

**Decision**: Add `await LoadPageAsync(ct)` after a successful sync in `HandleSyncAsync`.
**Rationale**: This follows the exact same pattern already used by Import and Delete operations in the same ViewModel. No new infrastructure or query changes are needed.
**Alternatives Considered**:
- Event bus / messaging from Application layer → Overkill for a single ViewModel fix; adds cross-cutting complexity.
- Observable subscription on SyncCommand completion in `WhenActivated` → Less readable than inline call; the Import/Delete pattern is already established.
- Domain event triggering UI refresh → Violates Clean Architecture (Domain shouldn't know about UI concerns).

### R-002: Sort/Filter State Preservation

**Question**: Will calling `LoadPageAsync()` after sync preserve the current sort order and filter state?

**Finding**: `LoadPageAsync()` uses `_currentPage` and `_sortDescending` fields directly when constructing `GetReportsQuery`. These fields are not modified by `HandleSyncAsync`. Therefore, calling `LoadPageAsync()` after sync will re-query with the exact same pagination and sort parameters, preserving the user's view state.

**Decision**: No additional state management is needed. The existing `LoadPageAsync()` method handles this correctly.
**Rationale**: The query parameters are driven by ViewModel properties that are untouched during sync.

### R-003: Error Handling for Post-Sync Refresh Failure

**Question**: What should happen if sync succeeds but the subsequent `LoadPageAsync()` fails?

**Finding**: `LoadPageAsync()` already has error handling — it sets `ErrorMessage` on failure and the `finally` block resets `IsLoading`. However, calling `LoadPageAsync()` after sync means we should preserve the sync status message even if the refresh fails. The current `ImportAsync` pattern shows the precedent: it preserves the import error through the reload.

**Decision**: Call `LoadPageAsync()` after sync success, then re-apply the sync status message after the page load completes. If `LoadPageAsync` sets an `ErrorMessage`, the sync status message (which uses a separate property `SyncStatusMessage`) will still be visible.
**Rationale**: `SyncStatusMessage` and `ErrorMessage` are separate properties, so there is no conflict. The sync status survives naturally.

### R-004: Scroll Position and UX

**Question**: Will the refresh cause visible flicker or scroll position loss?

**Finding**: `LoadPageAsync()` calls `Rows.Clear()` then re-populates via `Rows.Add()`. This is the existing pattern for all page loads. Since the DataGrid in Avalonia rebinds to the `ObservableCollection`, there may be a brief visual update but no full-page reload or navigation event. Scroll position within the same page of data is managed by the DataGrid control and will be reset to top when the collection is replaced — this is consistent with existing Import/Delete behavior and acceptable per the spec (which requires no *page navigation*, not pixel-perfect scroll preservation).

**Decision**: Accept the existing `Rows.Clear()` + `Rows.Add()` pattern. No additional scroll preservation logic needed.
**Rationale**: Matches existing behavior for Import and Delete. The spec's scroll preservation requirement (SC-003) refers to not navigating away from the page, which is satisfied.

## Summary

All research items resolved. The fix is a single-line addition (`await LoadPageAsync(ct)`) in `HandleSyncAsync` after a successful sync, following the established pattern in `ImportAsync` and `DeleteAsync`. No new dependencies, no architectural changes, no Application/Domain/Infrastructure modifications required.
