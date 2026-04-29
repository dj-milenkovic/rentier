# Implementation Plan: 048-reports-sync-refresh

**Branch**: `048-reports-sync-refresh` | **Date**: 2025-07-15 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `.specify/specs/048-reports-sync-refresh/spec.md`

## Summary

Bug fix: After a successful "Sync Mailbox" operation on the Reports page, the reports table does not refresh — newly synced reports are invisible until the user navigates away and back. The root cause is that `HandleSyncAsync` in `ReportsViewModel` omits the `LoadPageAsync()` call that both `ImportAsync` and `DeleteAsync` already perform after their operations. The fix adds a single `await LoadPageAsync(ct)` call after successful sync, preserving the current page and sort state.

## Technical Context

**Language/Version**: C# 12 / .NET 8
**Primary Dependencies**: Avalonia UI 11+, ReactiveUI, CommunityToolkit.Mvvm
**Storage**: SQLite via EF Core 8
**Testing**: xUnit + FluentAssertions + NSubstitute
**Target Platform**: Windows + macOS (cross-platform desktop)
**Project Type**: Desktop application (Clean Architecture)
**Performance Goals**: Table refresh completes within 2 seconds of sync completion (SC-001)
**Constraints**: Must not block UI thread; must use reactive async command flow
**Scale/Scope**: Single ViewModel method change + 5 new unit tests

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- [x] Clean Architecture boundary is preserved (`Desktop -> Application -> Domain`; Infrastructure implements Application contracts only).
  - **Justification**: Fix is entirely within `Rentier.Desktop` (ViewModel layer). It calls an existing Application query (`GetReportsQuery`) that is already injected. No new cross-layer dependencies.
- [x] All monetary/rate/percentage values are modeled as `decimal`.
  - **Justification**: No monetary fields introduced or modified.
- [x] All business dates are modeled as `DateOnly`; boundary conversions are identified.
  - **Justification**: No date fields introduced or modified.
- [x] Security/privacy constraints hold: local-first data, OS credential store for secrets, no telemetry.
  - **Justification**: No change to storage model or network behavior. Refresh is a local DB query.
- [x] External network usage is limited to approved endpoints (IMAP and NBS) or explicitly justified.
  - **Justification**: No new network calls. The refresh is a local SQLite query.
- [x] All I/O paths are async; UI work avoids blocking calls and uses reactive async command flow.
  - **Justification**: `LoadPageAsync` is already async. It is called within `HandleSyncAsync` which is a `ReactiveCommand.CreateFromTask` handler. UI updates go through `RxApp.MainThreadScheduler` via the `outputScheduler` parameter.
- [x] Tests and coverage impact are defined for Domain (rule/state coverage) and Application (>=90%).
  - **Justification**: No Domain or Application changes. 5 new Desktop ViewModel tests defined. Existing Application tests unaffected.
- [x] Feature work is mapped to an approved spec task under `.specify/tasks/`.
  - **Justification**: Spec exists at `.specify/specs/048-reports-sync-refresh/spec.md`.

## Project Structure

### Documentation (this feature)

```text
.specify/specs/048-reports-sync-refresh/
├── spec.md                              # Feature specification
├── plan.md                              # This file
├── research.md                          # Phase 0: Root cause analysis
├── data-model.md                        # Phase 1: Entity impact (none)
├── quickstart.md                        # Phase 1: Implementation guide
└── contracts/
    └── ui-behavior-contract.md          # Phase 1: Post-sync refresh behavior
```

### Source Code (affected files)

```text
src/
└── Rentier.Desktop/
    └── ViewModels/
        └── ReportsViewModel.cs          # MODIFY: HandleSyncAsync method

tests/
└── Rentier.UnitTests/
    └── Desktop/
        └── ReportsViewModelTests.cs     # MODIFY: Add 5 sync-refresh tests
```

## Complexity Tracking

No constitution violations. No complexity escalation needed.

## Design Decisions

### D-001: Follow Established Refresh Pattern

**Decision**: Add `await LoadPageAsync(ct)` in `HandleSyncAsync` after successful sync.

**Rationale**: This is the exact pattern used by `ImportAsync` (line 391) and `DeleteAsync` (line 421) in the same ViewModel. Consistency reduces cognitive load and maintenance risk.

**Alternatives Rejected**:
| Alternative | Why Rejected |
|-------------|-------------|
| Event bus from Application layer | Over-engineered for a single ViewModel; adds coupling |
| Reactive subscription on SyncCommand completion | Less readable; breaks the established inline pattern |
| Domain event triggering UI refresh | Violates Clean Architecture (Domain → UI knowledge) |
| Timer-based polling | Fragile, wastes resources, poor UX |

### D-002: Refresh Before Status Message

**Decision**: Call `LoadPageAsync(ct)` before setting `SyncStatusMessage`, so the table updates first and then the status message appears.

**Rationale**: The user sees the table update and then the confirmation message, creating a natural "done" signal. If `LoadPageAsync` fails, `ErrorMessage` is set by the existing error handler but the sync status message still gets set afterward (using the separate `SyncStatusMessage` property).

### D-003: No Page Reset on Sync

**Decision**: Do not reset `CurrentPage` to 1 after sync.

**Rationale**: Preserves the user's position. New reports sorted to a different page will appear when the user navigates there. This matches the spec requirement FR-003/FR-004 (preserve sort and filter state).

## Implementation Tasks

### Task 1: Modify HandleSyncAsync (ViewModel)

**File**: `src/Rentier.Desktop/ViewModels/ReportsViewModel.cs`
**Method**: `HandleSyncAsync`
**Change**: Add `await LoadPageAsync(ct);` as the first statement inside the `if (result.IsSuccess)` block.

### Task 2: Add Unit Tests

**File**: `tests/Rentier.UnitTests/Desktop/ReportsViewModelTests.cs`
**Tests**:

1. **`SyncCommand_WhenSucceeds_RefreshesReportsList`**
   - Setup: Configure `_getReports` mock to return different data on second call
   - Act: Execute SyncCommand
   - Assert: `_getReports.HandleAsync` received 2 calls (activation + post-sync)

2. **`SyncCommand_WhenSucceeds_PreservesSortOrder`**
   - Setup: Set `SortDescending = false`, then execute SyncCommand
   - Assert: The `GetReportsQuery` passed to `_getReports` on the post-sync call has `SortDescending = false`

3. **`SyncCommand_WhenSucceeds_PreservesCurrentPage`**
   - Setup: Navigate to page 2, then execute SyncCommand
   - Assert: The `GetReportsQuery` passed to `_getReports` on the post-sync call has `Page = 2`

4. **`SyncCommand_WhenFails_DoesNotRefreshReportsList`**
   - Setup: Configure sync handler to return failure
   - Act: Execute SyncCommand
   - Assert: `_getReports.HandleAsync` received exactly 1 call (activation only, no post-sync refresh)

5. **`SyncCommand_WhenSucceeds_NewRowsAppearInCollection`**
   - Setup: Initial load returns 1 row; configure `_getReports` to return 2 rows on second call
   - Act: Execute SyncCommand
   - Assert: `vm.Rows.Count == 2`

## Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| `LoadPageAsync` throws during post-sync refresh | Low | Low | Existing `ThrownExceptions` subscription on `SyncCommand` handles this; `ErrorMessage` is set |
| Sync is slow + refresh adds perceptible delay | Low | Low | Both operations are async; UI remains responsive. DB query is local SQLite (<100ms) |
| Regression in existing sync status message behavior | Low | Medium | Test 1 verifies refresh happens; existing tests verify status message still works |

## Post-Design Constitution Re-Check

- [x] No new cross-layer dependencies introduced
- [x] No new `decimal`/`double` concerns
- [x] No new date handling
- [x] No new network calls
- [x] Async pattern maintained (`await LoadPageAsync(ct)` inside async method)
- [x] Test coverage defined (5 new tests for Desktop layer)

**All gates pass. Ready for task generation.**
