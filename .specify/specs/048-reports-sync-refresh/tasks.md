# Tasks: Reports Table Refresh After Sync (048-reports-sync-refresh)

**Input**: Design documents from `.specify/specs/048-reports-sync-refresh/`
**Prerequisites**: plan.md ✅ spec.md ✅ quickstart.md ✅ contracts/ui-behavior-contract.md ✅

**Scope**: Desktop-layer only — 1-line bug fix in `ReportsViewModel.HandleSyncAsync` + 5 unit tests.  
No Setup or Foundational phase required (no project init, no new infrastructure, no new entities).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: User story label (US1/US2/US3)

---

## Phase 1: User Story 1 — New Reports Appear Immediately After Sync (Priority: P1) 🎯 MVP

**Goal**: Fix the core bug. After a successful sync, `LoadPageAsync(ct)` is called inside `HandleSyncAsync`
so the `Rows` collection is refreshed in-place — no navigation required.

**Independent Test**: Run `dotnet test tests/Rentier.UnitTests --filter "ReportsViewModelTests"` and verify
all 5 new tests pass. Manually: press "Sync Mailbox" on the Reports page and confirm a newly synced report
appears in the table without navigating away.

### Tests for User Story 1

- [X] T001 [P] [US1] Add test `SyncCommand_WhenSucceeds_RefreshesReportsList` to `tests/Rentier.UnitTests/Desktop/ReportsViewModelTests.cs` — assert `_getReports.HandleAsync` is called a second time (activation + post-sync)
- [X] T002 [P] [US1] Add test `SyncCommand_WhenSucceeds_PreservesSortOrder` to `tests/Rentier.UnitTests/Desktop/ReportsViewModelTests.cs` — assert the `GetReportsQuery` on the post-sync call carries the current `SortDescending` value
- [X] T003 [P] [US1] Add test `SyncCommand_WhenSucceeds_PreservesCurrentPage` to `tests/Rentier.UnitTests/Desktop/ReportsViewModelTests.cs` — assert the `GetReportsQuery` on the post-sync call carries the current `CurrentPage` value
- [X] T004 [P] [US1] Add test `SyncCommand_WhenFails_DoesNotRefreshReportsList` to `tests/Rentier.UnitTests/Desktop/ReportsViewModelTests.cs` — assert `_getReports.HandleAsync` is called exactly once (activation only, no post-sync refresh) when sync returns failure
- [X] T005 [P] [US1] Add test `SyncCommand_WhenSucceeds_NewRowsAppearInCollection` to `tests/Rentier.UnitTests/Desktop/ReportsViewModelTests.cs` — configure `_getReports` to return 2 rows on the second call; assert `vm.Rows.Count == 2` after sync

### Implementation for User Story 1

- [X] T006 [US1] In `src/Rentier.Desktop/ViewModels/ReportsViewModel.cs`, inside `HandleSyncAsync`, add `await LoadPageAsync(ct);` as the first statement in the `if (result.IsSuccess)` block — before `SyncProgressValue = 100` (follow the same pattern as `ImportAsync` and `DeleteAsync`)

**Checkpoint**: All 5 tests in `ReportsViewModelTests` pass; sync on the Reports page refreshes the table in-place without navigation.

---

## Phase 2: User Story 2 — Refresh Preserves Sort and Filter State (Priority: P2)

**Goal**: Verify that the existing `LoadPageAsync(ct)` call (added in US1) correctly preserves `SortDescending`
and filter parameters, satisfying FR-003 and FR-004. No additional code changes are expected — this phase
is covered by T002 and T003 above, plus a manual verification step.

**Independent Test**: Set a sort order and/or filter in the Reports table, trigger sync, and verify the
sort/filter is still active and new reports slot into the correct position.

- [X] T007 [US2] Manually verify (or automate if desired) that after sync the `SortDescending` and `CurrentPage` state are unchanged — cross-reference against T002 and T003 results to confirm this is already covered by the US1 tests

**Checkpoint**: US2 acceptance criteria fully satisfied by T002, T003, and T007 verification. No code changes required beyond the US1 fix.

---

## Phase 3: User Story 3 — No Disruptive Full-Page Reload (Priority: P3)

**Goal**: Confirm the `LoadPageAsync(ct)` approach is a data-only refresh — no navigation, no page reload,
no scroll-position reset. This is an architectural property of the fix (calling the query directly in the
ViewModel rather than navigating), not a new code change.

**Independent Test**: After sync, verify the user remains on the Reports page with no navigation event.

- [X] T008 [US3] Verify in `src/Rentier.Desktop/ViewModels/ReportsViewModel.cs` that `HandleSyncAsync` does not call any navigation service or trigger a page reload — only `LoadPageAsync(ct)` mutates `Rows`; document finding as a code comment or in a PR note

**Checkpoint**: US3 acceptance criteria satisfied by design. No code or test changes required.

---

## Phase 4: Polish & Cross-Cutting Concerns

- [X] T009 Run `dotnet test tests/Rentier.UnitTests --filter "ReportsViewModelTests"` and confirm all tests pass (including the 5 new ones)
- [X] T010 Run `dotnet test` (full suite) and confirm no regressions
- [X] T011 [P] Verify quickstart.md manual steps: open app, navigate to Reports page, press "Sync Mailbox", confirm new reports appear without navigating away

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (US1)**: No dependencies — start immediately. Tests (T001–T005) can be written in parallel; fix (T006) depends on the tests being written first (TDD).
- **Phase 2 (US2)**: Depends on T006 (fix) being in place; T002 and T003 cover this phase.
- **Phase 3 (US3)**: Depends on T006 (fix) being in place; architectural verification only.
- **Phase 4 (Polish)**: Depends on all prior phases complete.

### Within Phase 1

1. Write all 5 tests (T001–T005) in parallel — same file, non-overlapping test methods ✅
2. Confirm tests **fail** (no production change yet)
3. Apply the 1-line fix (T006)
4. Confirm all 5 tests now **pass**

### Parallel Opportunities

```
# Write all 5 tests simultaneously (parallel, same file, different methods):
T001: SyncCommand_WhenSucceeds_RefreshesReportsList
T002: SyncCommand_WhenSucceeds_PreservesSortOrder
T003: SyncCommand_WhenSucceeds_PreservesCurrentPage
T004: SyncCommand_WhenFails_DoesNotRefreshReportsList
T005: SyncCommand_WhenSucceeds_NewRowsAppearInCollection

# Then apply the single fix:
T006: Add await LoadPageAsync(ct) in HandleSyncAsync
```

---

## Implementation Strategy

### MVP (User Story 1 Only — recommended)

1. Write tests T001–T005 (all failing)
2. Apply fix T006
3. Confirm all 5 tests pass
4. Run full suite (T009–T010)
5. **Done** — US2 and US3 are satisfied by design; no further code required

### Full Delivery

Same as MVP. US2 and US3 require only verification tasks (T007, T008), not code changes.

---

## Summary

| Phase | Tasks | Parallel? | New Files? |
|-------|-------|-----------|------------|
| US1 Tests | T001–T005 | ✅ Yes | No (modify existing) |
| US1 Fix | T006 | — | No (modify existing) |
| US2 Verify | T007 | — | No |
| US3 Verify | T008 | — | No |
| Polish | T009–T011 | Partial | No |

**Total tasks**: 11  
**Tasks per user story**: US1 = 6, US2 = 1, US3 = 1, Polish = 3  
**Parallel opportunities**: T001–T005 (all 5 tests), T009 + T011  
**Suggested MVP scope**: T001–T006 (US1 only — complete and independently testable)  
**Files modified**: 2 (`ReportsViewModel.cs`, `ReportsViewModelTests.cs`)

---

## Notes

- [P] tasks = different methods, no conflicts within the test file
- All 5 test tasks touch `ReportsViewModelTests.cs` — write as a single batch to avoid merge conflicts
- T006 is the single production code change; all other tasks are tests or verification
- US2 and US3 are satisfied by the US1 fix — no additional implementation required
- Commit after T001–T005 (failing tests), then again after T006 (passing fix)
