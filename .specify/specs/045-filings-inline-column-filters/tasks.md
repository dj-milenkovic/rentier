# Tasks: Filings Inline Column Filters

**Feature**: 045-filings-inline-column-filters  
**Input**: `.specify/specs/045-filings-inline-column-filters/` (spec.md, plan.md, data-model.md, contracts/ui-filter-contract.md, research.md)  
**Branch**: `045-filings-inline-column-filters`

**Tests**: Included — Application handler, Infrastructure repository integration, Desktop ViewModel.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing. Phase 2 (Foundational) must complete before any user story phase begins. US1 and US2 (P1) may proceed in any order once Phase 2 is done. US3–US7 (P2/P3) each depend on Phase 2 only.

---

## Phase 1: Setup

**Purpose**: Branch preparation and constitution compliance acknowledgment.

- [ ] T001 Create branch `045-filings-inline-column-filters` from `main` and verify solution builds with `dotnet build Rentier.slnx`
- [ ] T002 Add constitution compliance checklist entry for this feature: Architecture (Desktop→Application→Domain only), DateOnly boundary (FilterDeadline DateTimeOffset→DateOnly conversion), no monetary filter fields, ephemeral filter state (no persistence), async I/O (LoadPageAsync), test gates (Application ≥90%, Desktop ViewModel coverage)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Application-layer query contract and Infrastructure repository extension that ALL user stories depend on. No user story implementation may begin until this phase is complete.

**⚠️ CRITICAL**: Complete and verify T003–T011 before any user story work begins.

### Application Layer Contract

- [ ] T003 Create `src/Rentier.Application/Queries/FilingColumnFilter.cs` — sealed record with nullable fields: `FilingStatus? Status`, `IncomeType? IncomeType`, `string? PayingEntity`, `DateOnly? FilingDeadline`, `string? PaymentReference` — all default to `null`
- [ ] T004 Modify `src/Rentier.Application/Queries/GetFilingsQuery.cs` — add `FilingColumnFilter? ColumnFilter = null` as the final parameter (backward-compatible default)
- [ ] T005 Modify `src/Rentier.Application/Repositories/IFilingRepository.cs` — add `FilingColumnFilter? columnFilter = null` parameter to `GetPagedAsync` signature (backward-compatible default)
- [ ] T006 Modify `src/Rentier.Application/Handlers/GetFilingsQueryHandler.cs` — when `ReportIdFilter` is null, pass `query.ColumnFilter` through to `_filings.GetPagedAsync(columnFilter: query.ColumnFilter)`; when `ReportIdFilter` is set, pass `columnFilter: null`

### Infrastructure Layer Implementation

- [ ] T007 Modify `src/Rentier.Infrastructure/Repositories/FilingRepository.cs` — extend `GetPagedAsync` to accept `FilingColumnFilter? columnFilter` and chain `Where()` clauses for each non-null field: Status enum equality, IncomeType enum equality, PayingEntity `EF.Functions.Like` case-insensitive contains, FilingDeadline exact `DateOnly` equality, PaymentReference nullable `EF.Functions.Like` contains

### Application Tests

- [ ] T008 [P] Modify `tests/Rentier.Application.Tests/Handlers/GetFilingsQueryHandlerTests.cs` — add test: handler passes `ColumnFilter` to repository mock when `ReportIdFilter` is null
- [ ] T009 [P] Modify `tests/Rentier.Application.Tests/Handlers/GetFilingsQueryHandlerTests.cs` — add test: handler passes `columnFilter: null` to repository when `ReportIdFilter` is set (mutual exclusivity)

### Infrastructure Tests

- [ ] T010 [P] Modify `tests/Rentier.Infrastructure.Tests/Repositories/FilingRepositoryTests.cs` — add integration tests: filter by Status (Init only), filter by IncomeType (Dividend only), filter by PayingEntity contains (case-insensitive), filter by FilingDeadline exact match, filter by PaymentReference contains, combined AND filter (Status + IncomeType), combined AND filter (PayingEntity + Status), empty result when no match
- [ ] T011 [P] Modify `tests/Rentier.Infrastructure.Tests/Repositories/FilingRepositoryTests.cs` — add test: `columnFilter: null` returns same results as before (no regression)

**Checkpoint**: Run `dotnet test` — all Application and Infrastructure tests must pass before proceeding to user story phases.

---

## Phase 3: User Story 1 — Filter Filings by Status (Priority: P1) 🎯 MVP

**Goal**: Status ComboBox in filter row lets users select Init / Filed / Paid / All; DataGrid updates immediately showing only matching filings.

**Independent Test**: Load Filings page with mixed-status filings. Select "Init" — only Init filings visible. Switch to "Filed" — only Filed visible. Select "All" — all filings restored.

### ViewModel Tests for US1

- [ ] T012 [P] [US1] Modify `tests/Rentier.Desktop.Tests/ViewModels/FilingsViewModelTests.cs` — add test: setting `FilterStatus` to a non-null value triggers `LoadPageAsync` and resets `CurrentPage` to 1
- [ ] T013 [P] [US1] Modify `tests/Rentier.Desktop.Tests/ViewModels/FilingsViewModelTests.cs` — add test: `HasActiveFilters` is `false` when `FilterStatus` is null, `true` when non-null
- [ ] T014 [P] [US1] Modify `tests/Rentier.Desktop.Tests/ViewModels/FilingsViewModelTests.cs` — add test: `StatusFilterOptions` contains one entry with `Value = null` (label "Svi") and one entry per `FilingStatus` enum value

### ViewModel Implementation for US1

- [ ] T015 [US1] Modify `src/Rentier.Desktop/ViewModels/FilingsViewModel.cs` — add `FilterStatus` (`FilingStatus?`) reactive property, `StatusFilterOptions` (`IReadOnlyList<FilterOption<FilingStatus?>>`) computed from enum values with localized labels (Svi/Inicijalan/Podnet/Plaćen), `HasActiveFilters` derived bool (starts as `false`)
- [ ] T016 [US1] Modify `src/Rentier.Desktop/ViewModels/FilingsViewModel.cs` — wire `WhenAnyValue(FilterStatus)` to reset `CurrentPage = 1` then invoke `LoadPageAsync`; pass `FilterStatus` into `GetFilingsQuery.ColumnFilter` construction
- [ ] T017 [US1] Create `src/Rentier.Desktop/Models/FilterOption.cs` — `public sealed record FilterOption<T>(string Label, T Value);`

### View Implementation for US1

- [ ] T018 [US1] Modify `src/Rentier.Desktop/Views/FilingsView.axaml` — add filter row `Grid` panel between column header area and DataGrid; insert Status `ComboBox` in correct column position with `ItemsSource="{Binding StatusFilterOptions}"`, `SelectedItem="{Binding FilterStatus}"`, display member `Label`; bind `IsEnabled` to `IsFilterRowEnabled`
- [ ] T019 [US1] Modify `src/Rentier.Desktop/Resources/Strings.resx` — add keys: `Filter_All` ("Svi"), `Filter_StatusInit` ("Inicijalan"), `Filter_StatusFiled` ("Podnet"), `Filter_StatusPaid` ("Plaćen")

**Checkpoint**: Select each Status option in running app — DataGrid updates immediately, page resets to 1, pagination totals reflect filtered count.

---

## Phase 4: User Story 2 — Filter Filings by Text Columns (Priority: P1)

**Goal**: TextBox filter inputs for Isplatilac (payer) and Referenca plaćanja (payment reference) perform case-insensitive contains matching with 300ms debounce.

**Independent Test**: Type partial payer name — only matching filings shown after ~300ms. Clear text — all filings restored.

### ViewModel Tests for US2

- [ ] T020 [P] [US2] Modify `tests/Rentier.Desktop.Tests/ViewModels/FilingsViewModelTests.cs` — add test: setting `FilterPayingEntity` does NOT immediately invoke `LoadPageAsync`; after 300ms `Throttle` elapses, `LoadPageAsync` is called once
- [ ] T021 [P] [US2] Modify `tests/Rentier.Desktop.Tests/ViewModels/FilingsViewModelTests.cs` — add test: `HasActiveFilters` is `true` when `FilterPayingEntity` is non-empty string
- [ ] T022 [P] [US2] Modify `tests/Rentier.Desktop.Tests/ViewModels/FilingsViewModelTests.cs` — add test: setting `FilterPaymentReference` to non-empty triggers debounced reload and sets `HasActiveFilters = true`
- [ ] T023 [P] [US2] Modify `tests/Rentier.Desktop.Tests/ViewModels/FilingsViewModelTests.cs` — add test: clearing `FilterPayingEntity` to empty string triggers debounced reload; `HasActiveFilters` returns to `false` (assuming no other filters active)

### ViewModel Implementation for US2

- [ ] T024 [US2] Modify `src/Rentier.Desktop/ViewModels/FilingsViewModel.cs` — add `FilterPayingEntity` (`string?`) and `FilterPaymentReference` (`string?`) reactive properties
- [ ] T025 [US2] Modify `src/Rentier.Desktop/ViewModels/FilingsViewModel.cs` — wire `WhenAnyValue(FilterPayingEntity, FilterPaymentReference).Throttle(300ms, RxApp.TaskpoolScheduler)` to reset `CurrentPage = 1` then invoke `LoadPageAsync`; extend `FilingColumnFilter` construction to include text fields
- [ ] T026 [US2] Modify `src/Rentier.Desktop/ViewModels/FilingsViewModel.cs` — extend `HasActiveFilters` derived property to include `!string.IsNullOrEmpty(FilterPayingEntity) || !string.IsNullOrEmpty(FilterPaymentReference)`

### View Implementation for US2

- [ ] T027 [US2] Modify `src/Rentier.Desktop/Views/FilingsView.axaml` — add `TextBox` with `Watermark="Filter..."` for Isplatilac column in filter row, bound to `FilterPayingEntity`; add `TextBox` with `Watermark="Filter..."` for Referenca plaćanja column, bound to `FilterPaymentReference`; both bind `IsEnabled` to `IsFilterRowEnabled`
- [ ] T028 [US2] Modify `src/Rentier.Desktop/Resources/Strings.resx` — add key: `Filter_Placeholder` ("Filter...")

**Checkpoint**: Type partial payer name in running app — table updates after ~300ms delay. Rapid typing causes only one query (debounced). Clearing text restores full list.

---

## Phase 5: User Story 3 — Combine Multiple Filters (Priority: P2)

**Goal**: Multiple simultaneous active filters combine with AND logic — result set narrows progressively as each filter is applied.

**Independent Test**: Set Status="Init" AND Tip prihoda="Dividend" — only Init Dividend filings shown. Clear one filter — set expands to match remaining active filter.

### ViewModel Tests for US3

- [ ] T029 [P] [US3] Modify `tests/Rentier.Desktop.Tests/ViewModels/FilingsViewModelTests.cs` — add test: when `FilterStatus` and `FilterIncomeType` are both non-null, `GetFilingsQuery` is constructed with a `FilingColumnFilter` carrying both values (verify mock call args)
- [ ] T030 [P] [US3] Modify `tests/Rentier.Desktop.Tests/ViewModels/FilingsViewModelTests.cs` — add test: changing one filter while another is active does not reset the other filter property
- [ ] T031 [P] [US3] Modify `tests/Rentier.Desktop.Tests/ViewModels/FilingsViewModelTests.cs` — add test: `HasActiveFilters` is `true` when any combination of filters is non-default

### ViewModel Implementation for US3

- [ ] T032 [US3] Modify `src/Rentier.Desktop/ViewModels/FilingsViewModel.cs` — refactor reactive pipeline to merge all filter streams (instant: Status, IncomeType, Deadline; debounced: PayingEntity, PaymentReference) into a single merged observable that fires `LoadPageAsync` with the current combined `FilingColumnFilter` — verify no double-firing when multiple instant filters change simultaneously (use `CombineLatest` or `Merge` appropriately)
- [ ] T033 [US3] Verify `src/Rentier.Infrastructure/Repositories/FilingRepository.cs` — confirm `Where()` chain from T007 correctly intersects all non-null fields (AND logic); add combined filter integration test if not already covered by T010

**Checkpoint**: Set three filters simultaneously — result count correctly narrows. Clear one — result expands. No filter interaction bugs.

---

## Phase 6: User Story 4 — Clear All Filters (Priority: P2)

**Goal**: "Clear filters" button visible when any filter is active; clicking it resets all filter controls to defaults and restores unfiltered view.

**Independent Test**: Set two filters, click "Clear filters" — all controls reset to "All"/empty, full list restored. With no filters active, button is hidden.

### ViewModel Tests for US4

- [ ] T034 [P] [US4] Modify `tests/Rentier.Desktop.Tests/ViewModels/FilingsViewModelTests.cs` — add test: executing `ClearFiltersCommand` resets all five filter properties to their null/empty defaults
- [ ] T035 [P] [US4] Modify `tests/Rentier.Desktop.Tests/ViewModels/FilingsViewModelTests.cs` — add test: `ClearFiltersCommand.CanExecute` is `false` when `HasActiveFilters` is `false`, `true` when any filter is active
- [ ] T036 [P] [US4] Modify `tests/Rentier.Desktop.Tests/ViewModels/FilingsViewModelTests.cs` — add test: after `ClearFiltersCommand` executes, `LoadPageAsync` is invoked exactly once (not once per filter reset)

### ViewModel Implementation for US4

- [ ] T037 [US4] Modify `src/Rentier.Desktop/ViewModels/FilingsViewModel.cs` — implement `ClearFiltersCommand` as `ReactiveCommand<Unit, Unit>` with `canExecute: this.WhenAnyValue(x => x.HasActiveFilters)`; command body sets all five filter properties to null in a single batch, then triggers `LoadPageAsync` once (suppress intermediate reactive triggers during batch clear via `_suppressFilterReload` flag or by using a dedicated observable subscription)
- [ ] T038 [US4] Modify `src/Rentier.Desktop/Resources/Strings.resx` — add key: `Filter_ClearAll` ("Obriši filtere")

### View Implementation for US4

- [ ] T039 [US4] Modify `src/Rentier.Desktop/Views/FilingsView.axaml` — add "Clear filters" `Button` in the Actions column area of the filter row; bind `Command` to `ClearFiltersCommand`; bind `IsVisible` to `HasActiveFilters`; apply appropriate icon or label from `Filter_ClearAll` resource

**Checkpoint**: Set multiple filters, verify Clear button appears. Click it — all controls reset and full list loads in single query.

---

## Phase 7: User Story 5 — Navigate from Reports to a Specific Filing (Priority: P2)

**Goal**: When Reports page navigates to a specific filing via `ReportIdFilter`, all inline column filters are cleared and the filter row is disabled, guaranteeing target filing visibility.

**Independent Test**: Set active filters on Filings page that would hide a filing. Navigate from Reports to that filing. Filters are cleared, filing is visible and selected.

### ViewModel Tests for US5

- [ ] T040 [P] [US5] Modify `tests/Rentier.Desktop.Tests/ViewModels/FilingsViewModelTests.cs` — add test: setting `ReportIdFilter` to a non-null `Guid` clears all five filter properties to their null/empty defaults
- [ ] T041 [P] [US5] Modify `tests/Rentier.Desktop.Tests/ViewModels/FilingsViewModelTests.cs` — add test: `IsFilterRowEnabled` is `false` when `ReportIdFilter` is set, `true` when `ReportIdFilter` is null
- [ ] T042 [P] [US5] Modify `tests/Rentier.Desktop.Tests/ViewModels/FilingsViewModelTests.cs` — add test: `GetFilingsQuery` constructed with `ColumnFilter: null` when `ReportIdFilter` is set, even if filter properties happen to be non-null
- [ ] T043 [P] [US5] Modify `tests/Rentier.Desktop.Tests/ViewModels/FilingsViewModelTests.cs` — add test: clearing `ReportIdFilter` (set to null) re-enables `IsFilterRowEnabled` and inline filters become usable again

### ViewModel Implementation for US5

- [ ] T044 [US5] Modify `src/Rentier.Desktop/ViewModels/FilingsViewModel.cs` — extend `ReportIdFilter` setter: when value is non-null, suppress reactive filter pipeline, reset all five filter properties to null/empty, then trigger `LoadPageAsync` via existing `ReportIdFilter` path; derive `IsFilterRowEnabled` as `this.WhenAnyValue(x => x.ReportIdFilter).Select(id => id == null)`

**Checkpoint**: Navigate from Reports page to a filing in running app with active filters — filters cleared, filter row grayed out, target filing visible.

---

## Phase 8: User Story 6 — Filter by Income Type (Priority: P3)

**Goal**: Tip prihoda ComboBox in filter row lets users select Dividend / Interest / All; DataGrid updates immediately.

**Independent Test**: Select "Dividenda" — only Dividend filings shown. Select "Svi" — all income types restored.

### ViewModel Tests for US6

- [ ] T045 [P] [US6] Modify `tests/Rentier.Desktop.Tests/ViewModels/FilingsViewModelTests.cs` — add test: setting `FilterIncomeType` to non-null triggers immediate `LoadPageAsync` and sets `HasActiveFilters = true`
- [ ] T046 [P] [US6] Modify `tests/Rentier.Desktop.Tests/ViewModels/FilingsViewModelTests.cs` — add test: `IncomeTypeFilterOptions` contains null-value entry ("Svi") plus one entry per `IncomeType` enum value with localized labels

### ViewModel Implementation for US6

- [ ] T047 [US6] Modify `src/Rentier.Desktop/ViewModels/FilingsViewModel.cs` — add `FilterIncomeType` (`IncomeType?`) reactive property; add `IncomeTypeFilterOptions` (`IReadOnlyList<FilterOption<IncomeType?>>`) with labels Svi/Dividenda/Kamata; wire `WhenAnyValue(FilterIncomeType)` to immediate (non-debounced) reload; extend `HasActiveFilters` to include `FilterIncomeType.HasValue`

### View Implementation for US6

- [ ] T048 [US6] Modify `src/Rentier.Desktop/Views/FilingsView.axaml` — add Income Type `ComboBox` in Tip prihoda column position of filter row; bind `ItemsSource` to `IncomeTypeFilterOptions`, `SelectedItem` to `FilterIncomeType`, display member `Label`; bind `IsEnabled` to `IsFilterRowEnabled`
- [ ] T049 [US6] Modify `src/Rentier.Desktop/Resources/Strings.resx` — add keys: `Filter_IncomeDividend` ("Dividenda"), `Filter_IncomeInterest` ("Kamata")

**Checkpoint**: Select each income type option — table updates immediately with correct filtered results.

---

## Phase 9: User Story 7 — Filter by Filing Deadline (Priority: P3)

**Goal**: `CalendarDatePicker` in Rok za podnošenje column position lets users filter to an exact deadline date; clearing the picker restores all results.

**Independent Test**: Pick a date — only filings with that exact `FilingDeadline` shown. Clear picker — all deadlines restored.

### ViewModel Tests for US7

- [ ] T050 [P] [US7] Modify `tests/Rentier.Desktop.Tests/ViewModels/FilingsViewModelTests.cs` — add test: setting `FilterDeadline` to a `DateTimeOffset?` value triggers immediate `LoadPageAsync`; `FilingColumnFilter.FilingDeadline` is `DateOnly` extracted from the `DateTimeOffset` date component
- [ ] T051 [P] [US7] Modify `tests/Rentier.Desktop.Tests/ViewModels/FilingsViewModelTests.cs` — add test: `HasActiveFilters` is `true` when `FilterDeadline` is non-null
- [ ] T052 [P] [US7] Modify `tests/Rentier.Desktop.Tests/ViewModels/FilingsViewModelTests.cs` — add test: `FilterDeadline` set to `null` produces `FilingColumnFilter.FilingDeadline = null` (no date filter applied)

### ViewModel Implementation for US7

- [ ] T053 [US7] Modify `src/Rentier.Desktop/ViewModels/FilingsViewModel.cs` — add `FilterDeadline` (`DateTimeOffset?`) reactive property; wire `WhenAnyValue(FilterDeadline)` to immediate reload; convert `FilterDeadline?.ToLocalTime().Date` to `DateOnly?` when constructing `FilingColumnFilter.FilingDeadline`; extend `HasActiveFilters` to include `FilterDeadline.HasValue`

### View Implementation for US7

- [ ] T054 [US7] Modify `src/Rentier.Desktop/Views/FilingsView.axaml` — add compact `CalendarDatePicker` in Rok za podnošenje column position of filter row; bind `SelectedDate` to `FilterDeadline`; bind `IsEnabled` to `IsFilterRowEnabled`; style to match filter row height

**Checkpoint**: Select a date — only filings with that exact deadline shown. Clear date — all deadlines restored. DateOnly conversion correct (no off-by-one from timezone).

---

## Phase 10: Polish & Cross-Cutting Concerns

**Purpose**: Empty state, edge cases, visual polish, and final integration validation.

### Empty State

- [ ] T055 [P] Modify `src/Rentier.Desktop/Views/FilingsView.axaml` — add empty state overlay message (text from `Filter_NoResults`) displayed when `Rows.Count == 0 && HasActiveFilters`; include inline "Obriši filtere" action button bound to `ClearFiltersCommand`
- [ ] T056 [P] Modify `src/Rentier.Desktop/Resources/Strings.resx` — add key: `Filter_NoResults` ("Nema prijava koje odgovaraju aktivnim filterima")

### Edge Cases & Polish

- [ ] T057 [P] Verify filter row visual alignment with DataGrid columns in `src/Rentier.Desktop/Views/FilingsView.axaml` — column widths of filter row `Grid` must match DataGrid column widths; apply `rentier-ui-design` token system for control styling (background, border, text colors)
- [ ] T058 [P] Verify `ShowAll` toggle interaction — inline column filters apply within `ShowAll` (Unpaid/All) selection; confirm `WhenAnyValue(ShowAll)` also resets filter state to prevent stale cross-filter results; add ViewModel test if interaction is not yet covered
- [ ] T059 [P] Verify pagination resets correctly — changing any filter resets `CurrentPage` to 1 and `TotalPages`/`TotalCount` reflect filtered counts; confirm existing pagination ViewModel tests still pass
- [ ] T060 [P] Verify sort-within-filter — changing sort column while filters are active does not clear filters; filtered + sorted results are correct; confirm existing sort tests still pass
- [ ] T061 Run `dotnet test` and confirm all new and existing tests pass; confirm Application handler coverage gate (≥90%) is met
- [ ] T062 Run quickstart.md validation scenario end-to-end: open Filings page, apply each filter type, combine filters, clear all, navigate from Reports to a filing, verify no regressions

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: No dependencies — start immediately
- **Phase 2 (Foundational)**: Depends on Phase 1 — **BLOCKS all user story phases**
- **Phase 3–9 (User Stories)**: All depend on Phase 2; US1/US2 (P1) should be delivered first, then US3–US5 (P2), then US6–US7 (P3)
- **Phase 10 (Polish)**: Depends on all desired user story phases being complete

### User Story Dependencies

| Story | Priority | Depends On | Notes |
|-------|----------|-----------|-------|
| US1 — Filter by Status | P1 | Phase 2 | No US dependencies; implements filter row scaffold |
| US2 — Filter by Text | P1 | Phase 2 | No US dependencies; independent of US1 |
| US3 — Combine Filters | P2 | Phase 2 (US1+US2 complete in practice) | Validates AND logic across all fields |
| US4 — Clear All Filters | P2 | Phase 2 | Depends on all filter properties existing (complete US1+US2 first) |
| US5 — Reports Navigation | P2 | Phase 2 | Independent; can be done alongside US1/US2 |
| US6 — Filter by Income Type | P3 | Phase 2 | Follows same dropdown pattern as US1 |
| US7 — Filter by Deadline | P3 | Phase 2 | Independent; CalendarDatePicker control |

### Within Each User Story

1. ViewModel tests (marked [P]) — write first, verify they fail
2. ViewModel implementation — make tests pass
3. View (AXAML) changes — bind controls to ViewModel
4. Strings.resx additions — localized labels
5. Checkpoint validation before moving to next story

### Parallel Opportunities

All tasks marked [P] within the same phase can run concurrently (different files, no shared state).

```
# Phase 2 parallel opportunities (after T003–T007 complete):
Task: T008 — Application handler test: ColumnFilter forwarding
Task: T009 — Application handler test: ReportIdFilter exclusivity
Task: T010 — Infrastructure repo integration tests (8 scenarios)
Task: T011 — Infrastructure repo regression test

# Phase 3 parallel opportunities (US1 tests):
Task: T012 — FilterStatus triggers LoadPageAsync
Task: T013 — HasActiveFilters derived from FilterStatus
Task: T014 — StatusFilterOptions contents

# Phase 4 parallel opportunities (US2 tests):
Task: T020 — FilterPayingEntity debounce behavior
Task: T021 — HasActiveFilters with text filter
Task: T022 — FilterPaymentReference debounce
Task: T023 — Clear text restores full list
```

---

## Parallel Example: Phase 2 Execution

```
Sequential (must be in order):
T003 → T004 → T005 → T006 → T007

Then parallel:
├── T008  (Application handler test: ColumnFilter forwarding)
├── T009  (Application handler test: ReportIdFilter exclusivity)
├── T010  (Infrastructure integration tests)
└── T011  (Infrastructure regression test)
```

---

## Implementation Strategy

### MVP First (US1 + US2 only — P1 stories)

1. Complete Phase 1: Setup (T001–T002)
2. Complete Phase 2: Foundational (T003–T011) — **critical gate**
3. Complete Phase 3: US1 Filter by Status (T012–T019)
4. Complete Phase 4: US2 Filter by Text (T020–T028)
5. **STOP and VALIDATE**: Both P1 stories independently testable, app is usable with status + text filters
6. Optionally deliver/demo at this point

### Incremental Delivery

1. Phase 1 + Phase 2 → Contract + repository ready
2. Phase 3 (US1) → Status filter live → Demo
3. Phase 4 (US2) → Text filters live → Demo
4. Phase 5 (US3) → Combined AND logic verified → Demo
5. Phase 6 (US4) → Clear all button → Demo
6. Phase 7 (US5) → Reports navigation safe → Demo
7. Phase 8 (US6) + Phase 9 (US7) → Income type + deadline filters → Demo
8. Phase 10 (Polish) → Empty state, edge cases, final QA → Merge

---

## Summary

| Phase | User Story | Priority | Tasks | Test Tasks | Impl Tasks |
|-------|-----------|----------|-------|-----------|-----------|
| 1 | Setup | — | T001–T002 | 0 | 2 |
| 2 | Foundational | — | T003–T011 | 4 | 5 |
| 3 | US1: Status Filter | P1 🎯 | T012–T019 | 3 | 5 |
| 4 | US2: Text Filters | P1 | T020–T028 | 4 | 5 |
| 5 | US3: Combine Filters | P2 | T029–T033 | 3 | 2 |
| 6 | US4: Clear All | P2 | T034–T039 | 3 | 3 |
| 7 | US5: Reports Nav | P2 | T040–T044 | 4 | 1 |
| 8 | US6: Income Type | P3 | T045–T049 | 2 | 3 |
| 9 | US7: Deadline | P3 | T050–T054 | 3 | 2 |
| 10 | Polish | — | T055–T062 | 1 | 7 |
| **Total** | | | **62 tasks** | **27 test** | **35 impl** |

**Parallel opportunities**: 27 tasks marked [P]  
**Suggested MVP scope**: Phase 1 + Phase 2 + Phase 3 (US1) + Phase 4 (US2) — T001–T028 (28 tasks)  
**Format validation**: All 62 tasks follow `- [ ] TXXX [P?] [USx?] Description with file path` format ✅
