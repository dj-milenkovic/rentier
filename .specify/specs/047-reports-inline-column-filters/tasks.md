# Tasks: Reports Inline Column Filters

**Feature**: `047-reports-inline-column-filters`  
**Branch**: `047-reports-inline-column-filters`  
**Input**: `.specify/specs/047-reports-inline-column-filters/` (spec.md, plan.md, data-model.md, contracts/filter-contracts.md, research.md, quickstart.md)  
**Tests**: Included — constitution quality gates require Application/Infrastructure/Desktop coverage.

## Format: `[ID] [P?] [Story?] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: User story label (US1–US5); Setup/Foundational/Polish phases carry no story label
- Exact file paths are included in every task description

---

## Phase 1: Setup (Application Layer New Types)

**Purpose**: Create the three new Application-layer types and localized strings that every subsequent layer references. Compile-order requires T001 → T002 → T003; T004 is fully independent.

- [X] T001 Create `ComparisonOperator` enum (`Equals = 0`, `GreaterThan = 1`, `LessThan = 2`) with XML doc comment in `src/Rentier.Application/Enums/ComparisonOperator.cs`
- [X] T002 Create `ReportColumnFilter` sealed record with all-optional fields (`NameContains`, `ImporterContains`, `ImportDateOperator`/`ImportDateValue`, `EmailDateOperator`/`EmailDateValue`, `FilingCountOperator`/`FilingCountValue`, `StatusFilter`) and sensible defaults (operators default to `Equals`, value fields default to `null`) in `src/Rentier.Application/DTOs/ReportColumnFilter.cs`
- [X] T003 Add `ReportColumnFilter? Filter = null` optional parameter to the `GetReportsQuery` record in `src/Rentier.Application/Queries/GetReportsQuery.cs`
- [X] T004 [P] Add localization strings for the filter row UI: filter TextBox watermark/placeholder, operator ComboBox items (`=`, `>`, `<`), "Clear filters" button label, and "All" status option to `src/Rentier.Desktop/Resources/Strings.resx`

**Checkpoint**: Solution compiles; existing `GetReportsQuery` callers are unaffected (`Filter` defaults to `null`).

---

## Phase 2: Foundational (Repository Contract + Infrastructure + Handler Refactoring)

**Purpose**: Push filtering and pagination to the database layer. No user story UI can produce correct results against a paged dataset until this phase is complete.

**⚠️ CRITICAL**: All user story phases depend on this phase completing successfully.

- [X] T005 Add `GetPagedAsync(ReportColumnFilter? filter, int skip, int take, bool sortDescending, CancellationToken ct = default)` returning `Task<(IReadOnlyList<Report> Items, int TotalCount)>` to `IReportRepository` in `src/Rentier.Application/Repositories/IReportRepository.cs`
- [X] T006 Implement `ReportRepository.GetPagedAsync` using EF Core `IQueryable` composition: apply `NameContains` via `r.ReportName.Contains(...)`, `ImportDateValue` via `ApplyDateFilter` helper (Equals/GreaterThan/LessThan switch on `r.ImportDate`), `EmailDateValue` by first excluding rows where `r.EmailDate == null` then applying operator, `StatusFilter` via `r.Status == filter.StatusFilter.Value`; apply existing sort order; call `CountAsync` for total; then `Skip/Take` for the page in `src/Rentier.Infrastructure/Repositories/ReportRepository.cs`
- [X] T007 Refactor `GetReportsQueryHandler.HandleAsync` to replace `_reports.GetAllAsync` + in-memory pagination with `_reports.GetPagedAsync`: (1) if `query.Filter?.ImporterContains` is set, pre-resolve matching `ImporterId` values via `_importers.GetAllAsync` filtered by display name; (2) call `GetPagedAsync` with resolved filter and pagination params; (3) resolve importer name and filing count for the returned page rows (max 30); (4) if `query.Filter?.FilingCountValue` is set, post-filter the page rows by the resolved filing count using the specified `FilingCountOperator`; (5) return `ReportsPageResult` with `TotalCount` from repository in `src/Rentier.Application/Handlers/GetReportsQueryHandler.cs`
- [X] T008 [P] Write integration tests for `ReportRepository.GetPagedAsync` covering: no filter returns all rows paged; `NameContains` substring match (case-insensitive); `ImportDateValue` with `Equals`/`GreaterThan`/`LessThan` operators; `EmailDateValue` excludes rows where `EmailDate` is null; `StatusFilter` exact enum match; two filters combined with AND logic; `Skip`/`Take` pagination correctness; `TotalCount` reflects filtered count not page size in `tests/Rentier.Infrastructure.Tests/ReportRepositoryTests.cs`
- [X] T009 [P] Update `GetReportsQueryHandler` unit tests to verify: null `Filter` preserves existing behavior (backward compatibility); `ImporterContains` triggers `IImporterRepository.GetAllAsync` for pre-resolution; constructed filter is forwarded to `IReportRepository.GetPagedAsync`; `FilingCountValue` post-filter is applied to paged results; `TotalCount` from repository drives `TotalPages` calculation in `tests/Rentier.UnitTests/Application/GetReportsQueryHandlerTests.cs`

**Checkpoint**: `dotnet test` passes; Reports page loads and behaves identically to the pre-feature baseline with no active filters.

---

## Phase 3: User Story 1 — Filter by Text Column (Priority: P1) 🎯 MVP

**Goal**: Users type part of a report name or importer name into the filter row and immediately see only matching reports — the most common lookup workflow.

**Independent Test**: Load the Reports page, type "IBKR" into the Name filter TextBox, verify only matching rows appear; clear the input, verify all rows return.

- [X] T010 [US1] Add `NameFilter` (`string?`) and `ImporterFilter` (`string?`) reactive properties to `ReportsViewModel`; in `WhenActivated` wire a debounced reactive pipeline for both (`this.WhenAnyValue(x => x.NameFilter, x => x.ImporterFilter) → Throttle(TimeSpan.FromMilliseconds(300), _scheduler) → reset `CurrentPage` to 1 → InvokeCommand(LoadPageCommand)`); update `LoadPageAsync` to construct `new ReportColumnFilter(NameContains: NameFilter, ImporterContains: ImporterFilter, ...)` and pass it as `Filter` in the `GetReportsQuery` in `src/Rentier.Desktop/ViewModels/ReportsViewModel.cs`
- [X] T011 [P] [US1] Add a filter row `Grid` structure directly above the `DataGrid` in `ReportsView.axaml` (mirroring all DataGrid columns: checkbox placeholder, Name, ImportDate, EmailDate, Importer, Status, FilingCount, Actions placeholder); add `TextBox` controls to the Name and Importer filter cells with `Watermark` bound to the localized placeholder string and `Text` bound TwoWay to `NameFilter`/`ImporterFilter` in `src/Rentier.Desktop/Views/ReportsView.axaml`
- [X] T012 [P] [US1] Add `ReportsViewModel` unit tests for: `NameFilter` change triggers `LoadPageCommand` only after 300ms debounce (advance `TestScheduler` by 299ms → no call; advance 1ms more → call); `ImporterFilter` change triggers reload; empty or whitespace value is treated as no filter (does not add `NameContains` to `ReportColumnFilter`); `CurrentPage` resets to 1 when filter changes in `tests/Rentier.UnitTests/Desktop/ReportsViewModelTests.cs`

**Checkpoint**: The text filter row is visible; typing in the Name or Importer cell narrows the grid with the expected 300ms debounce.

---

## Phase 4: User Story 2 — Filter by Date Column with Operators (Priority: P1)

**Goal**: Users select a comparison operator (>, <, =) and enter a date value in the Import Date or Email Date filter cells; only reports satisfying the condition appear. Null Email Date rows are excluded when an Email Date filter is active.

**Independent Test**: Select ">" operator in the Import Date filter, enter "2024-06-01", verify only reports imported after that date appear; set Email Date filter and verify reports with no email date are absent.

- [X] T013 [US2] Add `ImportDateOperator` (`ComparisonOperator`, default `Equals`), `ImportDateFilter` (`DateOnly?`), `EmailDateOperator` (`ComparisonOperator`, default `Equals`), and `EmailDateFilter` (`DateOnly?`) properties to `ReportsViewModel`; wire individual `WhenAnyValue → Skip(1) → reset page to 1 → InvokeCommand(LoadPageCommand)` pipelines for each property in `WhenActivated`; update `LoadPageAsync` to include `ImportDateOperator`/`ImportDateFilter` and `EmailDateOperator`/`EmailDateFilter` in the constructed `ReportColumnFilter` in `src/Rentier.Desktop/ViewModels/ReportsViewModel.cs`
- [X] T014 [P] [US2] Add `ComboBox`+`TextBox` operator/value pairs to the Import Date and Email Date filter cells in the filter row `Grid` in `ReportsView.axaml`: `ComboBox.SelectedItem` two-way bound to `ImportDateOperator`/`EmailDateOperator` via a `ComparisonOperatorConverter`; `TextBox.Text` two-way bound to `ImportDateFilter`/`EmailDateFilter` via a `NullableDateOnlyConverter` that silently returns `null` for unparseable input in `src/Rentier.Desktop/Views/ReportsView.axaml`
- [X] T015 [P] [US2] Add `ReportsViewModel` unit tests for: `ImportDateFilter` value change triggers `LoadPageCommand`; `ImportDateOperator` change when `ImportDateFilter` is null does not produce a net filter change (no value → no reload needed); `ImportDateOperator` change when `ImportDateFilter` has a value triggers reload; `EmailDateFilter` change triggers reload; unparseable date text leaves `EmailDateFilter` null and no filter is applied in `tests/Rentier.UnitTests/Desktop/ReportsViewModelTests.cs`

**Checkpoint**: Import Date and Email Date filter cells are visible with operator selectors; date entry narrows the list; null Email Date rows are excluded when the Email Date filter is active.

---

## Phase 5: User Story 5 — Combine Multiple Filters and Clear All (Priority: P1)

**Goal**: Multiple column filters apply simultaneously with AND logic; a single "Clear filters" button resets all inputs at once.

**Independent Test**: Set `NameFilter = "IBKR"` and `ImportDateFilter` with operator ">", verify only reports satisfying both conditions appear; click "Clear filters", verify all filter inputs reset and all rows return; verify the button is hidden when no filters are active.

- [X] T016 [US5] Add `HasActiveFilters` `ObservableAsPropertyHelper<bool>` (derived from `WhenAnyValue` over all filter value properties — true when any text filter is non-empty or any value filter is non-null) and `ClearFiltersCommand` (`ReactiveCommand.Create`, enabled only when `HasActiveFilters` is true, sets all filter properties back to their defaults and resets `CurrentPage` to 1) to `ReportsViewModel` in `src/Rentier.Desktop/ViewModels/ReportsViewModel.cs`
- [X] T017 [P] [US5] Add "Clear filters" `Button` to the existing toolbar area in `ReportsView.axaml`, bound to `ClearFiltersCommand`, with `IsVisible` (or equivalent style) bound to `HasActiveFilters` so it is hidden when no filter is active in `src/Rentier.Desktop/Views/ReportsView.axaml`
- [X] T018 [P] [US5] Add `ReportsViewModel` unit tests for: `HasActiveFilters` is `false` when all filter properties are at default; `HasActiveFilters` is `true` as soon as any filter property deviates from default; `ClearFiltersCommand.CanExecute` is `false` when `HasActiveFilters` is `false`; `ClearFiltersCommand.Execute()` resets every filter property to its default; `ClearFiltersCommand.Execute()` resets `CurrentPage` to 1; verify that with two filters active the constructed `ReportColumnFilter` carries both fields (AND logic is represented correctly) in `tests/Rentier.UnitTests/Desktop/ReportsViewModelTests.cs`

**Checkpoint**: "Clear filters" button appears only when a filter is active; clicking it resets everything; multiple active filters produce a single `ReportColumnFilter` with all fields set.

---

## Phase 6: User Story 3 — Filter by Numeric Column with Operators (Priority: P2)

**Goal**: Users select a comparison operator and enter an integer in the Filing Count filter cell; only reports with a matching count are shown. Non-numeric input is silently ignored.

**Independent Test**: Select ">" and enter "5" in the Filing Count filter; verify only reports with more than 5 filings appear. Enter "abc"; verify no filter is applied and the grid remains unchanged.

- [X] T019 [US3] Add `FilingCountOperator` (`ComparisonOperator`, default `Equals`) and `FilingCountFilter` (`int?`) properties to `ReportsViewModel`; wire `WhenAnyValue → Skip(1) → reset page → InvokeCommand(LoadPageCommand)` pipelines for both; update `LoadPageAsync` to include `FilingCountOperator`/`FilingCountFilter` in the constructed `ReportColumnFilter` in `src/Rentier.Desktop/ViewModels/ReportsViewModel.cs`
- [X] T020 [P] [US3] Add a `ComboBox`+`TextBox` operator/value pair to the Filing Count filter cell in the filter row `Grid` in `ReportsView.axaml`: `TextBox.Text` two-way bound to `FilingCountFilter` via a `NullableIntConverter` that calls `int.TryParse` and silently returns `null` for non-numeric input in `src/Rentier.Desktop/Views/ReportsView.axaml`
- [X] T021 [P] [US3] Add `ReportsViewModel` unit tests for: valid integer string sets `FilingCountFilter` and triggers reload; non-numeric string leaves `FilingCountFilter` null (no filter applied, no crash); `FilingCountOperator` change when `FilingCountFilter` has a value triggers reload in `tests/Rentier.UnitTests/Desktop/ReportsViewModelTests.cs`

**Checkpoint**: Filing Count filter cell is visible with an operator selector; valid integer input narrows the list; non-numeric text is silently ignored.

---

## Phase 7: User Story 4 — Filter by Status Dropdown (Priority: P2)

**Goal**: Users select a `ReportStatus` value from a dropdown in the Status filter cell; only reports in that state are shown. Selecting "All" clears the status filter.

**Independent Test**: Select "Error" from the Status dropdown; verify only Error-status reports appear. Select "All"; verify the status filter is cleared and all reports return.

- [X] T022 [US4] Add `StatusFilter` (`ReportStatus?`, default `null` = "All") property to `ReportsViewModel`; wire `WhenAnyValue → Skip(1) → reset page → InvokeCommand(LoadPageCommand)` pipeline in `WhenActivated`; update `LoadPageAsync` to include `StatusFilter` in the constructed `ReportColumnFilter` in `src/Rentier.Desktop/ViewModels/ReportsViewModel.cs`
- [X] T023 [P] [US4] Add a `ComboBox` to the Status filter cell in the filter row `Grid` in `ReportsView.axaml`: items include an "All" entry (maps to `null`) followed by all four `ReportStatus` values displayed via `ReportStatusDisplayConverter`; `SelectedItem` two-way bound to `StatusFilter` via a `NullableReportStatusConverter`; default selection is "All" in `src/Rentier.Desktop/Views/ReportsView.axaml`
- [X] T024 [P] [US4] Add `ReportsViewModel` unit tests for: `StatusFilter = ReportStatus.Error` triggers `LoadPageCommand`; `StatusFilter = null` triggers reload with no status restriction; "All" selection produces `null` `StatusFilter` (verified via constructed `ReportColumnFilter`) in `tests/Rentier.UnitTests/Desktop/ReportsViewModelTests.cs`

**Checkpoint**: Status dropdown is visible in the filter row; selecting a status narrows the list; "All" restores the full (unfiltered by status) list.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Visual alignment, regression validation, and quickstart scenario verification across all user stories.

- [X] T025 Verify filter row `Grid` column widths align visually with `DataGrid` column widths in `ReportsView.axaml`; adjust `ColumnDefinition` widths or introduce `SharedSizeGroup` / width bindings to `DataGridColumn.ActualWidth` to ensure each filter cell sits directly beneath its corresponding header in `src/Rentier.Desktop/Views/ReportsView.axaml`
- [X] T026 [P] Confirm all pre-existing `ReportsViewModelTests.cs` and `ReportsViewModelBulkDeleteTests.cs` tests still compile and pass after the new filter properties and constructor changes; fix any test call sites that construct `ReportsViewModel` directly and now require updated arguments or stub setup in `tests/Rentier.UnitTests/Desktop/`
- [X] T027 [P] Run the full test suite (`dotnet test`) across `Rentier.UnitTests` and `Rentier.Infrastructure.Tests`; confirm all new filter tests are green and Application/Infrastructure coverage quality gates are met for `GetReportsQueryHandler` and `ReportRepository` filter paths in `tests/`
- [X] T028 Manually validate each quickstart.md scenario end-to-end on a local build: text filter (US1), date filter with all three operators (US2), combining two filters (US5), clear filters button (US5), filing count filter with invalid input (US3), status dropdown (US4) per `specs/047-reports-inline-column-filters/quickstart.md`

---

## Dependencies & Execution Order

### Phase Dependencies

| Phase | Depends On | Can Start |
|-------|-----------|-----------|
| Phase 1 — Setup | Nothing | Immediately |
| Phase 2 — Foundational | Phase 1 complete | After T001–T004 |
| Phase 3 — US1 Text | Phase 2 complete | After T005–T009 |
| Phase 4 — US2 Date | Phase 2 complete | After T005–T009 (parallel with US1) |
| Phase 5 — US5 Clear | US1 + US2 complete | After T010–T015 |
| Phase 6 — US3 Numeric | Phase 2 complete | After T005–T009 (parallel with US1/US2) |
| Phase 7 — US4 Status | Phase 2 complete | After T005–T009 (parallel with others) |
| Phase 8 — Polish | All user story phases | After T010–T024 |

### Within Phase 1

- T001 → T002 → T003 (sequential; type references require compile order)
- T004 is independent of T001–T003 (different project, different file)

### Within Phase 2

- T005 → T006 → T008 (IReportRepository contract → implementation → integration tests)
- T005 → T007 → T009 (IReportRepository contract → handler refactor → handler tests)
- T008 ∥ T009 (different test files; can run in parallel once T006 and T007 are done)

### Within Each User Story Phase

- ViewModel task (T0x0) first; AXAML task (T0x1) and test task (T0x2) are parallel with each other after ViewModel

### Parallel Opportunities

```
Phase 1:  T001 ─→ T002 ─→ T003    T004 (independent)
Phase 2:  T005 ─→ T006 ─→ T008 ┐
                T007 ─→ T009 ┘  (T008 ∥ T009)
US1:      T010 ─→ T011 ∥ T012
US2:      T013 ─→ T014 ∥ T015      (∥ US1 — different VM props + AXAML cells)
US3:      T019 ─→ T020 ∥ T021      (∥ US1/US2 — different VM props + AXAML cells)
US4:      T022 ─→ T023 ∥ T024      (∥ US1/US2/US3 — independent)
US5:      T016 ─→ T017 ∥ T018      (after US1 + US2 complete)
Polish:   T026 ∥ T027              (independent checks)
```

---

## Parallel Example: Phase 2 (Foundational)

```
# After T005 (IReportRepository contract added):

Thread A:
  T006 — Implement ReportRepository.GetPagedAsync (EF Core IQueryable)
  T008 — Write integration tests for ReportRepository.GetPagedAsync

Thread B:
  T007 — Refactor GetReportsQueryHandler (GetAllAsync → GetPagedAsync)
  T009 — Update GetReportsQueryHandler unit tests

# T008 and T009 can run concurrently (different test files)
```

---

## Parallel Example: User Story 1 (after T010 ViewModel complete)

```
Thread A: T011 — Add filter row Grid + TextBox cells to ReportsView.axaml
Thread B: T012 — Write ViewModel unit tests for text filter debounce

# Both run concurrently — different files, no shared dependency
```

---

## Implementation Strategy

### MVP First (US1 — Text Filter Only)

1. Complete Phase 1: Setup (T001–T004)
2. Complete Phase 2: Foundational (T005–T009)
3. Complete Phase 3: US1 Text Filter (T010–T012)
4. **STOP and VALIDATE**: Name/Importer filter inputs narrow the list with 300ms debounce; page resets to 1 on filter change
5. **Demo**: User can type in the Name filter and immediately see only matching reports

### Incremental Delivery

1. Setup + Foundational → Database-level filtered paging ready
2. US1 (Text) → MVP: Name/Importer search
3. US2 (Date) → Date filter with >/</= operators
4. US5 (Combine + Clear) → All P1 stories complete; production-ready for typical workflows
5. US3 (Numeric) → Filing count operator filter
6. US4 (Status) → Status enum dropdown filter
7. Polish → Visual alignment + full regression

### Parallel Team Strategy (Two Developers)

- **Developer A**: Phase 1 + Phase 2 backend infrastructure
- **Developer B**: Can begin US1 ViewModel design and test scaffolding while Developer A finishes Phase 2

Once Phase 2 is done:
- **Developer A**: US2 (date filter) + US5 (clear)
- **Developer B**: US3 (numeric) + US4 (status)

---

## Notes

- `[P]` tasks touch different files — no merge conflicts expected within a phase
- `[Story]` label maps each task to its user story for traceability and independent demo
- `ComparisonOperator` enum is reusable — it will be referenced by feature 045 (Filings page) when it gains operator-based filters
- `new ReportColumnFilter()` (all defaults) is the safe "no filter" sentinel — backward compatible with all existing callers
- `IReportRepository.GetAllAsync` is preserved — sync pipeline and other callers remain unaffected
- FilingCount post-filter in the handler is acceptable for ≤30 rows/page; revisit with a JOIN/subquery if profiling indicates a bottleneck
- SQLite `LIKE` is case-insensitive for ASCII — covers all expected importer/report name patterns
