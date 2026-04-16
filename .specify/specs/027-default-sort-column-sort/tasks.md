# Tasks: Default Sort & Column Sort for Filings and Reports

**Feature**: 027-default-sort-column-sort  
**Branch**: `feat/027-031-ux-improvements`  
**Input**: `.specify/specs/027-default-sort-column-sort/` (spec.md, plan.md, data-model.md, contracts/query-sort-contracts.md, research.md, quickstart.md)  
**Tests**: Included — Application and Infrastructure changes require coverage tasks per project constitution (CA-006).

## Format: `[ID] [P?] [Story?] Description`

- **[P]**: Can run in parallel (different files, no shared dependencies)
- **[Story]**: Which user story this task belongs to (US1–US4)
- Exact file paths are in every task description

---

## Phase 1: Setup

**Purpose**: Confirm working context before making any changes.

- [ ] T001 Verify active branch is `feat/027-031-ux-improvements` (`git status` / `git branch --show-current`) and that the solution builds clean with `dotnet build Rentier.slnx`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Introduce the `FilingSortColumn` enum and extend all query records and repository interfaces with sort parameters (backward-compatible defaults). Every user story phase depends on this phase being complete.

**⚠️ CRITICAL**: No user story work can begin until T002–T006 are done.

- [ ] T002 Create `FilingSortColumn` enum with members `FilingDeadline = 0`, `Status = 1`, `IncomeType = 2`, `PayingEntity = 3`, `TaxPayable = 4`, `PaymentReference = 5` in `src/Rentier.Application/Enums/FilingSortColumn.cs`
- [ ] T003 [P] Extend `GetFilingsQuery` record with two new parameters `FilingSortColumn SortColumn = FilingSortColumn.FilingDeadline` and `bool SortDescending = true` (after existing `ReportIdFilter`) in `src/Rentier.Application/Queries/GetFilingsQuery.cs`
- [ ] T004 [P] Extend `GetReportsQuery` record with `bool SortDescending = true` parameter in `src/Rentier.Application/Queries/GetReportsQuery.cs`
- [ ] T005 [P] Update `IFilingRepository.GetPagedAsync` signature to add `FilingSortColumn sortColumn = FilingSortColumn.FilingDeadline` and `bool sortDescending = true` before the existing `CancellationToken ct = default` parameter in `src/Rentier.Application/Repositories/IFilingRepository.cs`
- [ ] T006 [P] Update `IReportRepository.GetAllAsync` signature to add `bool sortDescending = true` before the existing `CancellationToken ct = default` parameter in `src/Rentier.Application/Repositories/IReportRepository.cs`

**Checkpoint**: All Application layer types compile. Existing call sites unchanged (default parameters). User story implementation can now begin in parallel.

---

## Phase 3: User Story 1 — Default Filings Sort Order (Priority: P1) 🎯 MVP

**Goal**: Filings table defaults to `FilingDeadline DESC` on first load so the latest deadlines appear at the top without any user interaction.

**Independent Test**: Open the Filings page with several filings having different deadlines. Verify the row with the furthest-future deadline appears first. Apply the "Show All" filter and confirm the descending order is preserved.

### Tests for User Story 1

> **Write these first — confirm they FAIL before implementing T009 and T010.**

- [ ] T007 [P] [US1] Extend `GetFilingsQueryHandlerTests` with tests verifying: (a) default query passes `FilingSortColumn.FilingDeadline` and `SortDescending=true` to `IFilingRepository.GetPagedAsync`, (b) explicit sort params are forwarded unchanged, and (c) an invalid `FilingSortColumn` value returns a validation failure in `tests/Rentier.UnitTests/Application/GetFilingsQueryHandlerTests.cs`
- [ ] T008 [P] [US1] Extend `FilingRepositoryTests` with integration tests verifying: (a) default call returns rows ordered by `FilingDeadline DESC`, (b) each `FilingSortColumn` value produces the correct `ORDER BY` clause, and (c) deterministic tie-breaker `ThenBy(Id ASC)` is present in every case in `tests/Rentier.Infrastructure.Tests/FilingRepositoryTests.cs`

### Implementation for User Story 1

- [ ] T009 [US1] Update `GetFilingsQueryHandler.Handle` to extract `query.SortColumn` and `query.SortDescending`, validate the enum value, and pass both parameters to `IFilingRepository.GetPagedAsync` in `src/Rentier.Application/Handlers/GetFilingsQueryHandler.cs`
- [ ] T010 [US1] Replace the hardcoded `.OrderBy(f => f.FilingDeadline)` in `FilingRepository.GetPagedAsync` with a switch expression that maps each `FilingSortColumn` member to its EF Core `OrderBy`/`OrderByDescending` expression (cast enums to `(int)`), append `.ThenBy(f => f.Id)` as a deterministic tie-breaker, and update the method signature to accept the new parameters in `src/Rentier.Infrastructure/Repositories/FilingRepository.cs`

**Checkpoint**: Filings page opens with the latest deadline first. Filter changes preserve sort order. All US1 tests pass.

---

## Phase 4: User Story 2 — Default Reports Sort Order (Priority: P1)

**Goal**: Reports table defaults to `ImportDate DESC` so the most recently imported report appears at the top without any user interaction.

**Independent Test**: Navigate to the Reports page with several reports imported on different dates. Verify the most recently imported report is the first row.

### Tests for User Story 2

> **Write these first — confirm they FAIL before implementing T012 and T013.**

- [ ] T011 [US2] Extend `GetReportsQueryHandlerTests` with tests verifying: (a) default query passes `SortDescending=true` to `IReportRepository.GetAllAsync`, and (b) `SortDescending=false` is forwarded unchanged to the repository in `tests/Rentier.UnitTests/Application/GetReportsQueryHandlerTests.cs`

### Implementation for User Story 2

- [ ] T012 [US2] Update `GetReportsQueryHandler.Handle` to pass `query.SortDescending` to `IReportRepository.GetAllAsync` in `src/Rentier.Application/Handlers/GetReportsQueryHandler.cs`
- [ ] T013 [US2] Implement conditional `OrderByDescending`/`OrderBy` on `Report.ImportDate` (based on `sortDescending` parameter) followed by `.ThenBy(r => r.Id)` tie-breaker, replacing any existing fixed ordering in `ReportRepository.GetAllAsync` in `src/Rentier.Infrastructure/Repositories/ReportRepository.cs`
- [ ] T014 [P] [US2] Explicitly set `CanUserSortColumns="False"` on the Reports DataGrid to prevent non-functional header clicks (per R-006) in `src/Rentier.Desktop/Views/ReportsView.axaml`

**Checkpoint**: Reports page opens with the most recently imported report first. US2 tests pass. US1 functionality is unaffected.

---

## Phase 5: User Story 3 + User Story 4 — Interactive Column Sorting & Pagination Persistence (Priority: P2)

**Goal (US3)**: Users can click any sortable Filings column header to re-sort; clicking the same header again reverses the direction.  
**Goal (US4)**: Sort column and direction persist across page navigation; toggling direction does NOT reset the page (FR-009); changing the sort column resets the page to 1 (FR-010).

**Independent Test (US3)**: With Filings loaded, click the "Tax Payable" header — verify rows reorder by `TaxPayableRsd ASC`. Click again — verify `TaxPayableRsd DESC`.  
**Independent Test (US4)**: On page 2 sorted by TaxPayable, click the same column header to toggle direction — verify page stays on 2. Click a different column header — verify page resets to 1.

### Tests for User Story 3 + 4

> **Write these first — confirm they FAIL before implementing T016–T019.**

- [ ] T015 [US3] Extend `FilingsViewModelTests` with tests verifying: (a) initial sort state is `FilingSortColumn.FilingDeadline` / `SortDescending=true`, (b) `ApplySortCommand` with the same column toggles `SortDescending` and does NOT reset `CurrentPage` (FR-009), (c) `ApplySortCommand` with a different column sets the new column, sets `SortDescending=false` (ascending), and resets `CurrentPage` to 1 (FR-010), and (d) `LoadPageAsync` constructs `GetFilingsQuery` with the current sort state in `tests/Rentier.UnitTests/Desktop/FilingsViewModelTests.cs`

### Implementation for User Story 3 + 4

- [ ] T016 [US3] Add `_sortColumn` (`FilingSortColumn`) and `_sortDescending` (`bool`) backing fields with defaults `FilingDeadline`/`true`; expose as `[Reactive] public FilingSortColumn SortColumn` and `[Reactive] public bool SortDescending`; and implement `ApplySortCommand` (`ReactiveCommand<(string ColumnTag, bool? CurrentDirection), Unit>`) that maps `ColumnTag` string to `FilingSortColumn`, applies FR-009 (toggle direction, keep page) and FR-010 (new column, ascending, reset to page 1), and executes `LoadPageCommand` in `src/Rentier.Desktop/ViewModels/FilingsViewModel.cs`
- [ ] T017 [US3] Thread `_sortColumn` and `_sortDescending` into the `GetFilingsQuery` construction inside `LoadPageAsync` so every data load uses the current sort state in `src/Rentier.Desktop/ViewModels/FilingsViewModel.cs`
- [ ] T018 [P] [US3] Set `CanUserSortColumns="True"` on the Filings DataGrid; add `Tag="FilingDeadline"` to the Deadline column, `Tag="Status"` to the Status column, `Tag="IncomeType"` to the IncomeType column, `Tag="PayingEntity"` to the PayingEntity column, `Tag="TaxPayable"` to the TaxPayable column, and `Tag="PaymentReference"` to the PaymentReference column; wire the `Sorting="DataGrid_Sorting"` event on the DataGrid in `src/Rentier.Desktop/Views/FilingsView.axaml`
- [ ] T019 [P] [US3] Add `DataGrid_Sorting` event handler: set `e.Handled = true`, extract `e.Column.Tag` as the column identifier and the column's current `SortDirection` as the current direction hint, then execute `ViewModel!.ApplySortCommand.Execute((tag, currentDirection))` in `src/Rentier.Desktop/Views/FilingsView.axaml.cs`

**Checkpoint**: All US3 + US4 tests pass. Column headers re-sort the full data set (not just the visible page). Direction toggle preserves page position. Column change resets to page 1.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Final build verification, test coverage gate, and acceptance validation.

- [ ] T020 Run `dotnet build Rentier.slnx` with zero errors and zero warnings; resolve any issues introduced by sort parameter changes across all four projects
- [ ] T021 [P] Run `dotnet test Rentier.slnx --no-build` and confirm all existing tests still pass plus all new sort-related tests pass (Application ≥ 90% coverage gate per CA-006)
- [ ] T022 [P] Walk through all acceptance scenarios in `quickstart.md` against the running application on the `feat/027-031-ux-improvements` branch and confirm SC-001 through SC-007 are satisfied

---

## Dependencies & Execution Order

### Phase Dependencies

```
Phase 1: Setup        — No dependencies. Start immediately.
Phase 2: Foundational — Depends on Phase 1. BLOCKS all user story phases.
Phase 3: US1          — Depends on Phase 2 (T002, T003, T005).
Phase 4: US2          — Depends on Phase 2 (T002, T004, T006). Can run in parallel with Phase 3.
Phase 5: US3+US4      — Depends on Phase 3 (T009, T010 in place so ViewModel can pass sort state).
Phase 6: Polish       — Depends on all previous phases being complete.
```

### User Story Dependencies

- **US1 (P1)**: Requires Foundational phase. No dependency on US2.
- **US2 (P1)**: Requires Foundational phase. No dependency on US1.
- **US3+US4 (P2)**: Requires US1 complete (Application layer and FilingRepository must accept sort params before the ViewModel wires them up).
- **Polish**: Requires US1, US2, and US3+US4.

### Within Each Phase

- Tests MUST be written and confirmed failing before implementation tasks in the same phase.
- T003 and T004 can run in parallel once T002 is done.
- T005 and T006 can run in parallel once T002 is done.
- T007 and T008 (US1 tests) can run in parallel.
- T016 and T017 both edit `FilingsViewModel.cs` — apply sequentially in that order.
- T018 and T019 edit different files (`FilingsView.axaml` and `FilingsView.axaml.cs`) — can run in parallel.

### Parallel Opportunities

```bash
# Phase 2 — after T002 is done:
Task T003: Update GetFilingsQuery  (src/Rentier.Application/Queries/GetFilingsQuery.cs)
Task T004: Update GetReportsQuery  (src/Rentier.Application/Queries/GetReportsQuery.cs)
Task T005: Update IFilingRepository  (src/Rentier.Application/Repositories/IFilingRepository.cs)
Task T006: Update IReportRepository  (src/Rentier.Application/Repositories/IReportRepository.cs)

# Phase 3 — write tests in parallel, then implement:
Task T007: GetFilingsQueryHandlerTests  (tests/Rentier.UnitTests/Application/...)
Task T008: FilingRepositoryTests        (tests/Rentier.Infrastructure.Tests/...)

# Phase 5 — after T016/T017 are done:
Task T018: FilingsView.axaml     (src/Rentier.Desktop/Views/FilingsView.axaml)
Task T019: FilingsView.axaml.cs  (src/Rentier.Desktop/Views/FilingsView.axaml.cs)
```

---

## Implementation Strategy

### MVP First (US1 only — Filings default sort)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (T002–T006)
3. Complete Phase 3: US1 (T007–T010)
4. **STOP and VALIDATE**: Open Filings page, confirm latest deadline first
5. Demo / merge as MVP if needed

### Incremental Delivery

1. **Setup + Foundational** → Phase 2 gates pass
2. **US1** → Filings default sort correct → Demo (MVP)
3. **US2** → Reports default sort correct → Demo
4. **US3+US4** → Interactive column sort + pagination persistence → Demo
5. **Polish** → Coverage gates + acceptance validation → Merge-ready

### Single-Developer Sequence

```
T001 → T002 → T003 | T004 | T005 | T006 →
T007 | T008 → T009 → T010 →
T011 → T012 → T013 → T014 →
T015 → T016 → T017 → T018 | T019 →
T020 → T021 | T022
```

---

## Notes

- `[P]` tasks operate on different files with no incomplete shared dependencies — safe to parallelize.
- `[US#]` label maps each task to its user story for traceability to spec.md acceptance scenarios.
- T016 and T017 both modify `FilingsViewModel.cs` — they must be applied in order to avoid conflicts.
- The `FilingSortColumn` enum (T002) is the single foundational type that unblocks T003 and T005; T004 and T006 have no enum dependency and can proceed as soon as Phase 1 is done.
- Default parameter values on all new interface members (T005, T006) ensure zero call-site changes outside this feature.
- Reports DataGrid interactive sort is intentionally out of scope (R-006): only `CanUserSortColumns="False"` and the default `ImportDate DESC` order are delivered (T013, T014).
