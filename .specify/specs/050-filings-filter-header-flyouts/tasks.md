# Tasks: 050 — Filings Filter Header Flyouts

**Input**: Design documents from `.specify/specs/050-filings-filter-header-flyouts/`
**Branch**: `050-filings-filter-flyouts`
**Prerequisites**: plan.md ✅ spec.md ✅ research.md ✅ data-model.md ✅ contracts/ui-contracts.md ✅ quickstart.md ✅

**Tests**: Included. Desktop unit tests for flyout ViewModel state machines; Infrastructure integration
tests for new WHERE clauses; update to existing FilingsViewModelTests.

**Organization**: Tasks grouped by user story. US5 (remove filter row) comes first as a structural
prerequisite. US1+US2 are the P1 MVP. US3+US4+US6+US7 follow as P2.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel with other [P] tasks in the same phase (different files)
- **[Story]**: User story label from spec.md (US1–US7)
- Exact file paths are provided in every task description

---

## Phase 1: Setup

**Purpose**: Confirm branch state and verify no stale filter row artefacts will conflict.

- [ ] T001 Confirm active branch is `050-filings-filter-flyouts`; verify `FilingColumnFilter.cs`, `FilingRepository.cs`, and `FilingsView.axaml` are at HEAD with no uncommitted changes
- [ ] T002 [P] Add constitution compliance note to plan.md: Architecture ✅, decimal N/A ✅, DateOnly ✅ (FilterDeadline moves to string? at UI boundary only), Async ✅, Security ✅

---

## Phase 2: Foundational — Backend Extension + Shared ViewModel Infrastructure

**Purpose**: Core infrastructure that MUST be complete before any user story AXAML or ViewModel work begins.

**⚠️ CRITICAL**: No user story implementation can start until this phase is complete.

### Backend Extension (Application + Infrastructure layers)

- [ ] T003 Extend `FilingColumnFilter` record with three additive optional fields (`IReadOnlySet<FilingStatus>? Statuses = null`, `IReadOnlySet<IncomeType>? IncomeTypes = null`, `string? FilingDeadlineText = null`) while keeping all existing fields unchanged in `src/Rentier.Application/Queries/FilingColumnFilter.cs`
- [ ] T004 Add three repository WHERE clause blocks to `FilingRepository.GetPagedAsync` following the existing `if (field is not null)` pattern: `Statuses` → `WHERE Status IN (...)`, `IncomeTypes` → `WHERE IncomeType IN (...)`, `FilingDeadlineText` → `EF.Functions.Like(f.FilingDeadline.ToString(), $"%{text}%")` in `src/Rentier.Infrastructure/Repositories/FilingRepository.cs`

### Shared UI Resources

- [ ] T005 [P] Add four string resources to `src/Rentier.Desktop/Resources/Strings.resx`: `Filter_Search` = `Pretraži...`, `Filter_Apply` = `Primeni`, `Filter_SelectAll` = `Izaberi sve`, `Filter_Clear` = `Obriši`
- [ ] T006 [P] Add `FilterIcon` StreamGeometry to `src/Rentier.Desktop/Assets/Icons.axaml`: `<StreamGeometry x:Key="FilterIcon">M22 3H2l8 9.46V19l4 2v-8.54L22 3z</StreamGeometry>` (Lucide funnel, 24×24 viewport)

### Shared ViewModel Infrastructure

- [ ] T007 [P] Create `CheckableItem<T>` as a sealed `ReactiveObject` with `T Value`, `string Label`, and `bool IsChecked` (using `RaiseAndSetIfChanged`) in `src/Rentier.Desktop/ViewModels/CheckableItem.cs`
- [ ] T008 Create `EnumFilterFlyoutViewModel<T>` with: `ObservableCollection<CheckableItem<T>> Items`, `bool IsOpen` (light-dismiss sets false externally → discard), `bool IsActive` (computed: committed set ≠ all values), `ReactiveCommand ApplyCommand` (commits checked items via `Action<IReadOnlySet<T>?>` callback, sets `IsOpen = false`), `ReactiveCommand SelectAllCommand` (checks all items without applying), `ReactiveCommand ClearCommand` (unchecks all without applying); constructor takes `IEnumerable<(T Value, string Label)>` and `Action<IReadOnlySet<T>?>` callback in `src/Rentier.Desktop/ViewModels/EnumFilterFlyoutViewModel.cs`
- [ ] T009 [P] Create `TextFilterFlyoutViewModel` with: `string? SearchText` (working copy), `string? CommittedText` (private), `bool IsOpen`, `bool IsActive` (computed: CommittedText is non-empty), `ReactiveCommand ApplyCommand` (commits SearchText via `Action<string?>` callback, sets `IsOpen = false`); on `IsOpen` going true, reset `SearchText` to `CommittedText`; on `IsOpen` going false without Apply, reset `SearchText` to `CommittedText` in `src/Rentier.Desktop/ViewModels/TextFilterFlyoutViewModel.cs`
- [ ] T010 [P] Create `FilterActiveConverter` implementing `IValueConverter`: converts `bool isActive` → `IBrush` (true → `Application.Current.FindResource("RentierAccentBrush")`, false → `Application.Current.FindResource("RentierTextSecondaryBrush")`); register in `src/Rentier.Desktop/Converters/FilterActiveConverter.cs` and add entry to `App.axaml` converter resources

### Tests for Foundational ViewModel Infrastructure

- [ ] T011 [P] Write `EnumFilterFlyoutViewModelTests` covering: (1) `ApplyCommand` commits checked items via callback and sets `IsOpen = false`, (2) setting `IsOpen = false` externally (light-dismiss) discards uncommitted item changes and does NOT invoke callback, (3) `SelectAllCommand` checks all `Items.IsChecked` without invoking callback, (4) `ClearCommand` unchecks all `Items.IsChecked` without invoking callback, (5) `IsActive` is `false` when all values are committed, `true` when committed set is a proper subset in `tests/Rentier.UnitTests/Desktop/EnumFilterFlyoutViewModelTests.cs`
- [ ] T012 [P] Write `TextFilterFlyoutViewModelTests` covering: (1) `ApplyCommand` commits `SearchText` via callback and sets `IsOpen = false`, (2) setting `IsOpen = false` externally discards `SearchText` changes and reverts to `CommittedText`, (3) opening flyout when committed text is set shows the committed value in `SearchText`, (4) `IsActive` is `false` when `CommittedText` is null/empty, `true` when non-empty, (5) empty string apply clears the active state in `tests/Rentier.UnitTests/Desktop/TextFilterFlyoutViewModelTests.cs`

**Checkpoint**: Foundational complete — FilingColumnFilter extended, repository clauses added, all flyout ViewModel classes exist and are tested.

---

## Phase 3: US5 — Remove Filter Row (Priority: P1)

**Goal**: Eliminate the inline filter row Grid element so data rows appear directly below column headers.

**Independent Test**: Load filings page → verify no filter controls appear between column headers and data rows; resize columns → verify no misalignment.

- [ ] T013 [US5] Remove the entire `FilterRow` `Grid` element (containing all `ComboBox`, `TextBox`, and `CalendarDatePicker` filter controls and their `Grid.Row="1"` positioning) from `src/Rentier.Desktop/Views/FilingsView.axaml`; adjust the `DataGrid` row number accordingly so it directly follows the column header row
- [ ] T014 [US5] Remove obsolete properties and bindings from `src/Rentier.Desktop/ViewModels/FilingsViewModel.cs`: delete `StatusFilterOptions`, `IncomeTypeFilterOptions`, `IsFilterRowEnabled` properties and any ComboBox-targeted filter command wiring that belonged to the removed filter row
- [ ] T015 [US5] Update `tests/Rentier.UnitTests/Desktop/FilingsViewModelTests.cs`: remove or update test cases that referenced `StatusFilterOptions`, `IncomeTypeFilterOptions`, and `IsFilterRowEnabled` so the test file compiles and passes cleanly

**Checkpoint**: Filter row removed — DataGrid rows appear immediately under column headers; ViewModel compiles without obsolete members.

---

## Phase 4: US1 — Filter by Status via Header Flyout (Priority: P1) 🎯 MVP

**Goal**: Clicking the funnel icon in the Status column header opens a checkbox flyout; applying the selection filters filings by status.

**Independent Test**: Open Status flyout → uncheck "Paid" → click Apply → verify table shows only Init/Filed filings. Verify funnel icon is accent-colored. Reopen flyout → dismiss without Apply → verify filter unchanged.

### Tests for User Story 1

- [ ] T016 [US1] Write `FilingsViewModelTests` cases for Status flyout: (1) `StatusFlyout.ApplyCommand` sets `FilterStatus`-equivalent multi-select and triggers `LoadPageCommand`, (2) dismissing `StatusFlyout.IsOpen` (set to false) does not change the active filter, (3) `HasActiveFilters` is true after partial status selection is applied in `tests/Rentier.UnitTests/Desktop/FilingsViewModelTests.cs`

### Implementation for User Story 1

- [ ] T017 [US1] Add `StatusFlyout` property (`EnumFilterFlyoutViewModel<FilingStatus>`) to `FilingsViewModel`; initialize with all `FilingStatus` enum values and a callback that sets the multi-select filter and invokes the reactive reload pipeline; update `BuildColumnFilter` (or equivalent) to populate `FilingColumnFilter.Statuses` from the flyout's committed selection in `src/Rentier.Desktop/ViewModels/FilingsViewModel.cs`
- [ ] T018 [US1] Convert the Status `DataGridTextColumn` to a `DataGridTemplateColumn` with a custom header `StackPanel` containing: `TextBlock` (column label), existing sort `PathIcon`, and a `Button` (funnel icon using `FilterIcon` geometry) that toggles `StatusFlyout.IsOpen`; add an Avalonia `Popup` (`IsLightDismissEnabled="True"`, `PlacementTarget="{Binding ElementName=StatusFunnelButton}"`) containing an `ItemsControl` of `CheckBox` items bound to `StatusFlyout.Items`, a row with Select All and Clear `Button` controls bound to `StatusFlyout.SelectAllCommand`/`StatusFlyout.ClearCommand`, and an Apply `Button` bound to `StatusFlyout.ApplyCommand` in `src/Rentier.Desktop/Views/FilingsView.axaml`

**Checkpoint**: Status flyout fully functional and independently testable — most important filtering action works end-to-end.

---

## Phase 5: US2 — Filter by Text Columns via Header Flyout (Priority: P1)

**Goal**: Funnel icons in PayingEntity and PaymentReference column headers open text search flyouts; typing a term and applying filters the table.

**Independent Test**: Open PayingEntity flyout → type partial name → Apply → verify only matching rows shown. Open flyout again → verify typed text is pre-filled. Open PaymentReference flyout → type partial reference → Apply → verify filter applied independently.

### Tests for User Story 2

- [ ] T019 [P] [US2] Write `FilingsViewModelTests` cases for `PayingEntityFlyout`: (1) Apply commits search text and triggers reload, (2) dismiss discards text, (3) reopening shows previously committed text in `tests/Rentier.UnitTests/Desktop/FilingsViewModelTests.cs`
- [ ] T020 [P] [US2] Write `FilingsViewModelTests` cases for `PaymentReferenceFlyout`: same three cases as PayingEntity flyout in `tests/Rentier.UnitTests/Desktop/FilingsViewModelTests.cs`

### Implementation for User Story 2

- [ ] T021 [P] [US2] Add `PayingEntityFlyout` property (`TextFilterFlyoutViewModel`) to `FilingsViewModel` with callback updating `FilterPayingEntity` and triggering reload; add `PaymentReferenceFlyout` property similarly for `FilterPaymentReference`; update `BuildColumnFilter` to map both flyout committed texts to `FilingColumnFilter.PayingEntity` and `FilingColumnFilter.PaymentReference` in `src/Rentier.Desktop/ViewModels/FilingsViewModel.cs`
- [ ] T022 [P] [US2] Convert PayingEntity `DataGridTextColumn` to `DataGridTemplateColumn` with header containing sort arrow + funnel `Button` (toggles `PayingEntityFlyout.IsOpen`) + `Popup` containing `TextBox` (bound to `PayingEntityFlyout.SearchText`, placeholder from `Filter_Search`) + Apply `Button` (bound to `PayingEntityFlyout.ApplyCommand`) in `src/Rentier.Desktop/Views/FilingsView.axaml`
- [ ] T023 [P] [US2] Convert PaymentReference `DataGridTextColumn` to `DataGridTemplateColumn` with same header pattern as PayingEntity (funnel button + text search Popup bound to `PaymentReferenceFlyout`) in `src/Rentier.Desktop/Views/FilingsView.axaml`

**Checkpoint**: Both text column flyouts functional — payer and payment reference searches work independently and in combination.

---

## Phase 6: US3 — Filter by Income Type via Header Flyout (Priority: P2)

**Goal**: Funnel icon in Income Type column header opens a checkbox flyout with Dividend and Interest options.

**Independent Test**: Open IncomeType flyout → uncheck Interest → Apply → verify only Dividend filings shown. Verify Select All re-checks both.

### Tests for User Story 3

- [ ] T024 [P] [US3] Write `FilingsViewModelTests` cases for `IncomeTypeFlyout`: Apply commits selection and triggers reload; dismiss discards; IsActive reflects committed state in `tests/Rentier.UnitTests/Desktop/FilingsViewModelTests.cs`

### Implementation for User Story 3

- [ ] T025 [US3] Add `IncomeTypeFlyout` property (`EnumFilterFlyoutViewModel<IncomeType>`) to `FilingsViewModel`; initialize with all `IncomeType` enum values; update `BuildColumnFilter` to populate `FilingColumnFilter.IncomeTypes` from flyout committed selection in `src/Rentier.Desktop/ViewModels/FilingsViewModel.cs`
- [ ] T026 [P] [US3] Convert IncomeType `DataGridTextColumn` to `DataGridTemplateColumn` with same header pattern as Status (funnel button + `Popup` with `ItemsControl` of `CheckBox` items + Select All / Clear + Apply bound to `IncomeTypeFlyout`) in `src/Rentier.Desktop/Views/FilingsView.axaml`

**Checkpoint**: Income Type flyout functional — can filter to show only Dividend or only Interest filings.

---

## Phase 7: US4 — Filter by Deadline via Header Flyout (Priority: P2)

**Goal**: Funnel icon in Deadline column header opens a text flyout; typing a partial date string filters filings by deadline text match.

**Independent Test**: Open Deadline flyout → type "2025-07" → Apply → verify only filings with July 2025 deadlines are shown. Verify no date picker is present (text only).

### Tests for User Story 4

- [ ] T027 [US4] Write `FilingsViewModelTests` cases for `DeadlineFlyout`: Apply commits deadline text and triggers reload; dismiss discards; `FilterDeadline` is `string?` (not `DateTimeOffset?`); `BuildColumnFilter` maps non-empty committed text to `FilingColumnFilter.FilingDeadlineText` in `tests/Rentier.UnitTests/Desktop/FilingsViewModelTests.cs`

### Implementation for User Story 4

- [ ] T028 [US4] Add `DeadlineFlyout` property (`TextFilterFlyoutViewModel`) to `FilingsViewModel`; change `FilterDeadline` property type from `DateTimeOffset?` to `string?`; move `FilterDeadline` from the "instant reload" reactive group to the "debounced text" group; update `BuildColumnFilter` to populate `FilingColumnFilter.FilingDeadlineText` from `DeadlineFlyout`'s committed text in `src/Rentier.Desktop/ViewModels/FilingsViewModel.cs`
- [ ] T029 [US4] Convert Deadline `DataGridTextColumn` to `DataGridTemplateColumn` with header containing sort arrow + funnel `Button` (toggles `DeadlineFlyout.IsOpen`) + `Popup` with `TextBox` (placeholder `Filter_Search`) + Apply `Button` (bound to `DeadlineFlyout.ApplyCommand`) — no date picker, no operator selector in `src/Rentier.Desktop/Views/FilingsView.axaml`

**Checkpoint**: Deadline flyout functional — partial date text search works (e.g. "2025-07" matches all July 2025 deadlines).

---

## Phase 8: US6 + US7 — Clear All Filters + Active Filter Visual Indicator (Priority: P2)

**Goal**: "Clear All Filters" toolbar button resets all five flyout VMs; funnel icons change to accent color when their column has an active filter and revert when cleared.

**Independent Test**: Apply filters on Status and PayingEntity → verify both funnel icons are accent-colored → click "Clear All Filters" → verify all icons revert to default color and table reloads unfiltered.

### Tests for User Story 6 + 7

- [ ] T030 [P] [US6] [US7] Write `FilingsViewModelTests` cases: (1) `ClearFiltersCommand` resets all five flyout VMs to no-filter state, (2) `HasActiveFilters` is `true` when any flyout `IsActive` is true, `false` when all are inactive, (3) `ClearFiltersCommand.CanExecute` is false (or button hidden) when `HasActiveFilters` is false in `tests/Rentier.UnitTests/Desktop/FilingsViewModelTests.cs`

### Implementation for User Story 6 + 7

- [ ] T031 [US6] Update `ClearFiltersCommand` handler in `FilingsViewModel` to call a reset method on each of the five flyout VMs (`StatusFlyout`, `IncomeTypeFlyout`, `PayingEntityFlyout`, `DeadlineFlyout`, `PaymentReferenceFlyout`) that clears committed state and resets `IsActive = false`; update `HasActiveFilters` computation to be derived from the OR of all five `flyout.IsActive` observables in `src/Rentier.Desktop/ViewModels/FilingsViewModel.cs`
- [ ] T032 [P] [US7] Bind each funnel `Button`'s `PathIcon.Foreground` to the corresponding flyout's `IsActive` property using `FilterActiveConverter` in all five filterable column headers in `src/Rentier.Desktop/Views/FilingsView.axaml`
- [ ] T033 [P] [US6] Set `IsEnabled` on all five funnel `Button` controls to a binding that is `false` when `ReportIdFilter` is active (reuse or replace `IsFilterRowEnabled` pattern); ensure all flyout `IsOpen` values reset to `false` when `ReportIdFilter` is set in `src/Rentier.Desktop/Views/FilingsView.axaml`

**Checkpoint**: All seven user stories are complete. Full feature is functional end-to-end.

---

## Phase 9: Polish & Cross-Cutting Concerns

**Purpose**: Infrastructure validation tests, regression verification, and quickstart walkthrough.

- [ ] T034 [P] Add integration test in `tests/Rentier.Infrastructure.Tests/FilingRepositoryTests.cs`: seed filings with mixed statuses; assert `GetPagedAsync` with `Statuses = {Init, Filed}` returns only Init and Filed filings (verifies multi-select `WHERE Status IN` clause)
- [ ] T035 [P] Add integration test in `tests/Rentier.Infrastructure.Tests/FilingRepositoryTests.cs`: seed filings with mixed income types; assert `GetPagedAsync` with `IncomeTypes = {Dividend}` returns only Dividend filings
- [ ] T036 [P] Add integration test in `tests/Rentier.Infrastructure.Tests/FilingRepositoryTests.cs`: seed filings with various deadlines; assert `GetPagedAsync` with `FilingDeadlineText = "2025-07"` returns only filings whose `DateOnly` deadline contains "2025-07" when stored as ISO text (verifies `EF.Functions.Like` generates correct SQLite SQL)
- [ ] T037 Run full test suite (`dotnet test`) and confirm zero regressions; fix any compilation errors introduced by `FilterDeadline` type change (`DateTimeOffset?` → `string?`) in existing subscribers or bindings
- [ ] T038 Walk through `quickstart.md` manually: verify all 5 column flyout icons appear, each flyout opens correctly, Apply filters table, dismiss discards, Clear All resets all icons, ReportIdFilter disables all funnels

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: No dependencies — start immediately
- **Phase 2 (Foundational)**: Depends on Phase 1 — **BLOCKS all user story phases**
  - T003 (FilingColumnFilter) → T004 (Repository) — sequential within backend
  - T005, T006, T007, T008, T009, T010, T011, T012 — all parallelizable within Phase 2
- **Phase 3 (US5)**: Depends on Phase 2 — removes old bindings before new ones are added
- **Phase 4 (US1)**: Depends on Phase 3 — adds Status flyout to clean AXAML
- **Phase 5 (US2)**: Depends on Phase 3 — can run in parallel with Phase 4 (different columns)
- **Phase 6 (US3)**: Depends on Phase 3 — can run in parallel with Phases 4 and 5
- **Phase 7 (US4)**: Depends on Phase 3 — can run in parallel with Phases 4, 5, 6
- **Phase 8 (US6+US7)**: Depends on Phases 4–7 (needs all flyout VMs wired before clearing them)
- **Phase 9 (Polish)**: Depends on Phase 8 complete

### User Story Dependencies

- **US5 (P1)**: Must complete before US1–US4 (structural AXAML cleanup)
- **US1 (P1)**: Independent after US5 — establishes the enum flyout pattern
- **US2 (P1)**: Independent after US5 — can proceed in parallel with US1 (different columns)
- **US3 (P2)**: Independent after US5 — reuses enum pattern from US1 (no code dependency)
- **US4 (P2)**: Independent after US5 — reuses text pattern from US2 (no code dependency)
- **US6+US7 (P2)**: Depend on US1–US4 being wired (need all flyout VMs to clear/observe)

### Within Each User Story

- Tests → ViewModel wiring → AXAML view (tests can be written before implementation and must fail first)
- `EnumFilterFlyoutViewModel` (T008) must exist before any US1/US3 ViewModel task
- `TextFilterFlyoutViewModel` (T009) must exist before any US2/US4 ViewModel task
- `FilterActiveConverter` (T010) must exist before US6+US7 AXAML binding (T032)

---

## Parallel Execution Examples

### Phase 2: Foundational (all parallelizable after T003→T004 sequence)

```
# Sequential dependency:
Task T003: Extend FilingColumnFilter record
  └─ Task T004: Add repository WHERE clauses (waits for T003)

# These are all independent and can run in parallel:
Task T005: Add Strings.resx resources
Task T006: Add FilterIcon geometry
Task T007: Create CheckableItem<T>
Task T009: Create TextFilterFlyoutViewModel
Task T010: Create FilterActiveConverter
Task T011: Write EnumFilterFlyoutViewModelTests
Task T012: Write TextFilterFlyoutViewModelTests

# Sequential within ViewModel chain:
Task T007: CheckableItem<T>
  └─ Task T008: EnumFilterFlyoutViewModel<T> (needs CheckableItem)
```

### Phases 4–7: User Stories (run in parallel after Phase 3)

```
# All four stories can proceed simultaneously (different columns, different VM properties):
Developer A → Phase 4 (US1: Status flyout)
Developer B → Phase 5 (US2: Text flyouts)
Developer C → Phase 6 (US3: IncomeType flyout)
Developer D → Phase 7 (US4: Deadline flyout)
```

### Phase 9: Polish (integration tests all parallel)

```
Task T034: FilingRepository Statuses integration test
Task T035: FilingRepository IncomeTypes integration test
Task T036: FilingRepository FilingDeadlineText integration test
```

---

## Implementation Strategy

### MVP First (US5 + US1 Only — removes filter row + adds Status flyout)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (backend + shared ViewModel classes)
3. Complete Phase 3: US5 (remove filter row — immediate visual improvement)
4. Complete Phase 4: US1 (Status flyout — most-used filter)
5. **STOP and VALIDATE**: Confirm Status flyout works, filter row is gone, no regressions
6. Demo/merge if sufficient — remaining columns still work (no filter UI, not broken)

### Incremental Delivery

1. Setup + Foundational → infrastructure ready
2. US5 → filter row removed (space reclaimed immediately)
3. US1 → Status flyout ✅ (MVP)
4. US2 → Text column flyouts (PayingEntity + PaymentReference)
5. US3 → IncomeType flyout
6. US4 → Deadline flyout
7. US6+US7 → Clear All + visual indicators polished
8. Polish → integration tests + full validation

---

## Summary

| Phase | User Story | Priority | Tasks | Parallelizable |
|-------|------------|----------|-------|----------------|
| 1 | Setup | — | T001–T002 | — |
| 2 | Foundational (backend + shared VMs) | — | T003–T012 | T005–T012 [P] |
| 3 | US5: Remove filter row | P1 | T013–T015 | — |
| 4 | US1: Status flyout | P1 🎯 | T016–T018 | T016 [P] |
| 5 | US2: Text column flyouts | P1 | T019–T023 | T019–T023 [P] |
| 6 | US3: IncomeType flyout | P2 | T024–T026 | T024, T026 [P] |
| 7 | US4: Deadline flyout | P2 | T027–T029 | — |
| 8 | US6+US7: Clear All + active indicator | P2 | T030–T033 | T030, T032, T033 [P] |
| 9 | Polish | — | T034–T038 | T034–T036 [P] |

**Total tasks**: 38  
**Tasks by user story**: US5: 3, US1: 3, US2: 5, US3: 3, US4: 3, US6+US7: 4  
**Foundational tasks**: 12 (T003–T012)  
**Setup/Polish tasks**: 7 (T001–T002, T034–T038)  
**Parallel opportunities**: 22 tasks marked [P]  
**MVP scope**: Phases 1–4 (T001–T018) — filter row removed + Status flyout working
