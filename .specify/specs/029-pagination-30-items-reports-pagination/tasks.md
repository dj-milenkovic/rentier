# Tasks: Pagination — 30 Items per Page & Reports Pagination

**Feature**: 029-pagination-30-items-reports-pagination  
**Branch**: `feat/027-031-ux-improvements`  
**Input**: `.specify/specs/029-pagination-30-items-reports-pagination/` (spec.md · plan.md · data-model.md · research.md · quickstart.md · contracts/ui-pagination-contract.md)  
**Tests**: Included — Application handler and Desktop ViewModel changes require coverage per CA-006.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no incomplete-task dependencies)
- **[Story]**: User story the task belongs to (US1 / US2 / US3)
- File paths are relative to the repository root

---

## Phase 1: Setup

**Purpose**: Confirm working environment — no new projects, migrations, or infrastructure are required for this feature.

- [X] T001 Confirm branch is `feat/027-031-ux-improvements` and run `dotnet restore Rentier.slnx` to verify the solution builds cleanly before any changes

---

## Phase 2: Foundational (Blocking Prerequisite)

**Purpose**: Create the `ReportsPageResult` DTO that the Application handler (Phase 4) and all downstream ViewModel/test tasks depend on. No US2 or US3 work can start until T002 is complete.

**⚠️ CRITICAL**: US2 and US3 implementation cannot begin until this phase is complete.

- [X] T002 Create `src/Rentier.Application/DTOs/ReportsPageResult.cs` — sealed record `ReportsPageResult(IReadOnlyList<ReportRowDto> Rows, int TotalCount, int TotalPages)` mirroring the shape of `FilingsPageResult` exactly (see data-model.md §ReportsPageResult)

**Checkpoint**: `ReportsPageResult.cs` compiles — US2 and US3 implementation can now proceed.

---

## Phase 3: User Story 1 — Filings Page Size Increase to 30 (Priority: P1) 🎯 MVP

**Goal**: Change the single page-size constant from 20 to 30 so every Filings page renders 30 items, immediately reducing navigation overhead for the most-used page.

**Independent Test**: Load the Filings page with > 30 filings; confirm exactly 30 appear on page 1 and the page indicator reads "Page 1 of N" where N = ⌈total/30⌉. Can run independently of US2/US3.

- [X] T003 [P] [US1] Update `src/Rentier.Application/Queries/GetFilingsQuery.cs` — change `PageSize` default parameter value from `20` to `30` (single-line change in the record declaration)
- [X] T004 [P] [US1] Update `src/Rentier.Desktop/ViewModels/FilingsViewModel.cs` — change the explicit `PageSize: 20` argument passed to `GetFilingsQuery` in `LoadPageAsync` to `PageSize: 30`
- [X] T005 [P] [US1] Update `tests/Rentier.UnitTests/Application/GetFilingsQueryHandlerTests.cs` — replace all page-size-20 expectations (counts, page totals, slice assertions) with page-size-30 equivalents so existing tests reflect the new default
- [X] T006 [P] [US1] Update `tests/Rentier.UnitTests/Desktop/FilingsViewModelTests.cs` — replace all page-size-20 expectations with page-size-30 equivalents; verify test data sets used in arrange steps have more than 30 items where boundary behaviour is tested

**Checkpoint**: `dotnet test --filter "FullyQualifiedName~GetFilingsQueryHandler|FullyQualifiedName~FilingsViewModel"` passes — User Story 1 is fully functional and independently testable.

---

## Phase 4: User Story 2 — Reports Page Pagination with Navigation Controls (Priority: P1)

**Goal**: Introduce server-side in-memory pagination on the Reports page (30 per page), add Previous/page-indicator/Next controls to `ReportsView`, and wire the full reactive pagination state machine in `ReportsViewModel`.

**Independent Test**: Load the Reports page with > 30 reports; confirm 30 appear on page 1 with "Page 1 of N"; click Next to reach page 2 ("Page 2 of N", Previous enabled); reach the last page ("Page N of N", Next disabled). Requires T002 complete first.

- [X] T007 [P] [US2] Modify `src/Rentier.Application/Queries/GetReportsQuery.cs` — change `public sealed record GetReportsQuery;` to `public sealed record GetReportsQuery(int Page = 1, int PageSize = 30);` (see data-model.md §GetReportsQuery)
- [X] T008 [US2] Modify `src/Rentier.Application/Handlers/GetReportsQueryHandler.cs` — (1) add validation guards: throw/return Error if `Page < 1` or `PageSize < 1` or `PageSize > 100`, mirroring `GetFilingsQueryHandler` lines 34–40; (2) after building the full `List<ReportRowDto>`, compute `totalCount`, `totalPages = Math.Max(1, (int)Math.Ceiling((double)totalCount / query.PageSize))`; (3) slice with `.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToList()`; (4) return `Result.Success(new ReportsPageResult(slicedRows, totalCount, totalPages))` — change return type from `Result<IReadOnlyList<ReportRowDto>, Error>` to `Result<ReportsPageResult, Error>` (depends on T002, T007)
- [X] T009 [P] [US2] Add three new string entries to `src/Rentier.Desktop/Resources/Strings.resx` — `Reports_Page_Previous` = `← Previous`, `Reports_Page_Next` = `Next →`, `Reports_Page_Indicator` = `Page {0} of {1}` — following the `Reports_*` naming convention (see data-model.md §Localisation and research.md R-003)
- [X] T010 [US2] Add pagination state and commands to `src/Rentier.Desktop/ViewModels/ReportsViewModel.cs` — (1) backing fields `_currentPage = 1`, `_totalPages = 1`, `_totalCount = 0`; (2) public properties `CurrentPage`, `TotalPages`, `TotalCount`, computed `HasPreviousPage` (`_currentPage > 1 && !IsLoading`), `HasNextPage` (`_currentPage < _totalPages && !IsLoading`), `PageIndicator` (formatted from `Strings.Reports_Page_Indicator`); (3) `PreviousPageCommand` and `NextPageCommand` as `ReactiveCommand.CreateFromTask` — each decrements/increments `_currentPage` then calls `LoadPageAsync`; enabled via `IObservable<bool>` from `HasPreviousPage`/`HasNextPage`; (4) update `LoadReportsAsync` (rename to `LoadPageAsync`) to pass `_currentPage` and `30` to `GetReportsQuery`, then update `_totalCount`, `_totalPages`, clamp `_currentPage = Math.Min(_currentPage, _totalPages)`, and raise all pagination property-changed notifications — mirror `FilingsViewModel` reactive pattern throughout (depends on T008)
- [X] T011 [US2] Add pagination bar to `src/Rentier.Desktop/Views/ReportsView.axaml` — insert `<StackPanel DockPanel.Dock="Bottom" Orientation="Horizontal" Spacing="8" Margin="8" HorizontalAlignment="Center" IsVisible="{Binding HasItems}">` with a Previous `<Button>` bound to `PreviousPageCommand` / `Reports_Page_Previous`, a `<TextBlock>` bound to `PageIndicator`, and a Next `<Button>` bound to `NextPageCommand` / `Reports_Page_Next` — placement and structure identical to the pagination bar in `FilingsView.axaml`; see contracts/ui-pagination-contract.md §AXAML Structure (depends on T009, T010)
- [X] T012 [US2] Add delete and bulk-delete page-decrement guards to `src/Rentier.Desktop/ViewModels/ReportsViewModel.cs` — in the `DeleteCommand` handler: `if (Rows.Count == 1 && _currentPage > 1) _currentPage--;` before calling `LoadPageAsync`; in the `BulkDeleteCommand` handler: `if (selectedIds.Count == Rows.Count && _currentPage > 1) _currentPage--;` — mirroring `FilingsViewModel` delete guards exactly (see research.md R-006) (depends on T010)
- [X] T013 [P] [US2] Update `tests/Rentier.UnitTests/Application/GetReportsQueryHandlerTests.cs` — (1) update all existing tests for the new `ReportsPageResult` return type (access `.Value.Rows` instead of `.Value`); (2) add: pagination slicing test (75 reports, page 1 of 3 returns 30 rows with TotalCount=75 TotalPages=3), last page test (page 3 returns 15 rows), boundary validation tests (Page=0 returns error, PageSize=0 returns error, PageSize=101 returns error), empty collection test (0 reports returns TotalCount=0 TotalPages=1 Rows empty) (depends on T008)
- [X] T014 [P] [US2] Update `tests/Rentier.UnitTests/Desktop/ReportsViewModelTests.cs` — add: (1) initial state (CurrentPage=1, HasPreviousPage=false, HasNextPage based on data); (2) PageIndicator format "Page 1 of 3"; (3) NextPageCommand increments CurrentPage and reloads; (4) PreviousPageCommand decrements CurrentPage and reloads; (5) NextPageCommand disabled on last page; (6) PreviousPageCommand disabled on page 1; (7) commands disabled while IsLoading; (8) delete on single-item last page decrements page; (9) bulk delete of all items on non-first page decrements page (depends on T012)

**Checkpoint**: `dotnet test --filter "FullyQualifiedName~GetReportsQueryHandler|FullyQualifiedName~ReportsViewModel"` passes — User Story 2 is fully functional and independently testable.

---

## Phase 5: User Story 3 — Page Reset on Sort or Filter Changes (Priority: P2)

**Goal**: Proactively wire the page-reset mechanism in `ReportsViewModel` so that any future sort direction or filter property change automatically resets `_currentPage` to 1 and triggers a reload — matching the `FilingsViewModel.ShowAll` pattern and satisfying FR-016/FR-017.

**Independent Test**: Set `CurrentPage` to 3, then change `SortDirection`; verify `CurrentPage` resets to 1 and `LoadPageAsync` is called. Depends on US2 being complete.

- [X] T015 [US3] Add `SortDirection` reactive property to `src/Rentier.Desktop/ViewModels/ReportsViewModel.cs` — backing field `_sortDirection`, public property setter: assign backing field, set `_currentPage = 1`, raise `PropertyChanged` for `CurrentPage`/`HasPreviousPage`/`HasNextPage`/`PageIndicator`, then invoke `LoadPageCommand`; follow the exact pattern of `FilingsViewModel.ShowAll` setter; add a parallel `Filter` property using the same setter pattern if a filter backing field already exists in the ViewModel (depends on T010)
- [X] T016 [P] [US3] Add page reset tests to `tests/Rentier.UnitTests/Desktop/ReportsViewModelTests.cs` — (1) given CurrentPage=3, when SortDirection changes, then CurrentPage resets to 1 and LoadPageAsync is invoked; (2) given CurrentPage=2, when Filter property changes, then CurrentPage resets to 1 and LoadPageAsync is invoked (depends on T015)

**Checkpoint**: All US3 tests pass — page-reset pipeline is wired and ready for future sort/filter UI controls.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Final validation across all changed files.

- [X] T017 [P] Verify `src/Rentier.Desktop/Resources/Strings.Designer.cs` reflects the three new `Reports_Page_*` properties added in T009 — if MSBuild has not auto-regenerated it, manually add the three public static string properties following the existing generated pattern
- [X] T018 [P] Run `dotnet build Rentier.slnx` and confirm zero build errors and zero new warnings on all modified files
- [X] T019 [P] Run `dotnet test tests/Rentier.UnitTests --filter "FullyQualifiedName~GetReportsQueryHandler|FullyQualifiedName~GetFilingsQueryHandler|FullyQualifiedName~ReportsViewModel|FullyQualifiedName~FilingsViewModel"` and confirm all tests pass
- [X] T020 [P] Run the full test suite `dotnet test Rentier.slnx` and confirm no regressions outside the changed test files
- [X] T021 Verify SC-006: grep for hardcoded `"← Previous"`, `"Next →"`, and `"Page"` display literals in `ReportsView.axaml` and `ReportsViewModel.cs` — confirm zero hits (all strings sourced from `Strings.resx`)

---

## Dependencies & Execution Order

### Phase Dependencies

```
Phase 1 (Setup)
    └── Phase 2 (Foundational: ReportsPageResult DTO)
            ├── Phase 3 (US1 — Filings page size) ← Independent of US2/US3
            ├── Phase 4 (US2 — Reports pagination) ← Depends on T002
            └── Phase 5 (US3 — Page reset)        ← Depends on Phase 4 complete
                        └── Phase 6 (Polish)
```

### User Story Dependencies

| Story | Depends On | Can Start After |
|-------|-----------|-----------------|
| US1 (P1) | Phase 1 only | T001 — no foundational dependency |
| US2 (P1) | T002 (ReportsPageResult DTO) | T002 complete |
| US3 (P2) | US2 complete (T010) | T010 complete |

### Within Each User Story

- **US1**: T003–T006 are all independent (different files) — run in parallel
- **US2**: T007 and T009 are independent; T008 depends on T002+T007; T010 depends on T008; T011 depends on T009+T010; T012 depends on T010; T013 depends on T008; T014 depends on T012
- **US3**: T015 depends on T010; T016 depends on T015

### Critical Path

```
T001 → T002 → T007 → T008 → T010 → T011 → T015 → T016 → T017-T021
```

---

## Parallel Execution Examples

### US1 — All 4 tasks run in parallel (different files, no shared state)

```
Task A: T003 — src/Rentier.Application/Queries/GetFilingsQuery.cs
Task B: T004 — src/Rentier.Desktop/ViewModels/FilingsViewModel.cs
Task C: T005 — tests/Rentier.UnitTests/Application/GetFilingsQueryHandlerTests.cs
Task D: T006 — tests/Rentier.UnitTests/Desktop/FilingsViewModelTests.cs
```

### US2 — Parallel pairs after T008

```
After T002:  Task A: T007 (GetReportsQuery) ‖ Task B: T009 (Strings.resx)
After T007:  Task A: T008 (handler — sequential, depends on T007)
After T008:  Task A: T010 (ViewModel) ‖ Task B: T013 (handler tests)
After T010:  Task A: T011 (View, also needs T009) ‖ Task B: T012 (delete guards)
After T012:  Task A: T014 (ViewModel tests)
```

---

## Implementation Strategy

### MVP First (US1 only — zero risk, immediate value)

1. Complete Phase 1 (T001)
2. Complete Phase 3 (T003–T006) — all parallelisable
3. **STOP and VALIDATE**: `dotnet test --filter "FilingsQueryHandler|FilingsViewModel"` passes
4. Deploy — Filings page now shows 30 items

### Incremental Delivery

1. T001 → T002 → Foundation ready
2. T003–T006 in parallel → US1 complete → Deploy (Filings 30/page)
3. T007–T014 with critical path → US2 complete → Deploy (Reports pagination)
4. T015–T016 → US3 complete → Deploy (page reset wired)
5. T017–T021 → Polish → Merge

### Single-Developer Recommended Order

```
T001 → T002 → T003 → T004 → T005 → T006
           → T007 → T008 → T009 → T010 → T011 → T012 → T013 → T014
                                               → T015 → T016
                                                            → T017 → T018 → T019 → T020 → T021
```

---

## Notes

- **[P]** = different files, no dependency on any incomplete task in the same phase
- **[Story]** label maps each task to its user story for traceability and independent delivery
- US1 tasks (T003–T006) have zero dependencies on US2/US3 — safe to implement before or in parallel with Phase 2
- The `FilingsViewModel` / `FilingsView.axaml` / `GetFilingsQueryHandler` are the **reference patterns** for every US2 change — consult them at each step
- `Strings.Designer.cs` (T017) is auto-generated by MSBuild from `Strings.resx`; verify it after build rather than editing manually
- Commit after each phase checkpoint to keep the branch bisectable
- Run `dotnet test Rentier.slnx` (T020) as the final gate before raising a PR

