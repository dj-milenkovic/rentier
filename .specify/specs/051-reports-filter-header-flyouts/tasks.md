# Tasks: Reports Filter Header Flyouts

**Feature**: 051-reports-filter-header-flyouts  
**Input**: `.specify/specs/051-reports-filter-header-flyouts/` (spec.md, plan.md, data-model.md, contracts/ui-contracts.md, quickstart.md)  
**Branch**: `051-reports-filter-header-flyouts`

**Tests**: Included — spec (CA-006) requires ViewModel, handler, and repository tests updated.

**Organization**: Tasks grouped by user story to enable independent implementation and delivery.

## Format: `[ID] [P?] [Story?] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[US#]**: Maps to user story from spec.md
- Exact file paths in every description

---

## Phase 1: Setup

**Purpose**: Baseline verification before any breaking changes are made.

- [ ] T001 Verify current build and test suite pass before any changes: run `dotnet build Rentier.slnx` and `dotnet test Rentier.slnx` to establish a clean baseline

**Checkpoint**: Green build and tests confirmed — safe to proceed with DTO refactor.

---

## Phase 2: Foundational — Application DTO Simplification (Breaking Change)

**Purpose**: Modify `ReportColumnFilter` first — it is the shared contract between all layers. Infrastructure and handler changes follow immediately. Nothing in US1–US4 can compile cleanly until this phase is complete.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [ ] T002 Modify `src/Rentier.Application/DTOs/ReportColumnFilter.cs` — replace `ComparisonOperator ImportDateOperator`, `DateOnly? ImportDateValue`, `ComparisonOperator EmailDateOperator`, `DateOnly? EmailDateValue`, `ComparisonOperator FilingCountOperator`, and `ReportStatus? StatusFilter` with: `string? ImportDateContains`, `string? EmailDateContains`, and `IReadOnlySet<ReportStatus>? StatusFilters`; keep `NameContains`, `ImporterContains`, `ImporterIds`, and `FilingCountValue` unchanged

- [ ] T003 [P] Modify `src/Rentier.Infrastructure/Repositories/ReportRepository.cs` — in `GetPagedAsync`: replace `ApplyImportDateFilter` and `ApplyEmailDateFilter` helper method calls with inline `EF.Functions.Like(r.ImportDate.ToString(), $"%{term}%")` and `EF.Functions.Like(r.EmailDate.Value.ToString(), $"%{term}%")` guarded by `!string.IsNullOrWhiteSpace`; replace single `StatusFilter` equality with `filter.StatusFilters.Contains(r.Status)` IN-style predicate guarded by `filter.StatusFilters is { Count: > 0 }`; delete the `ApplyImportDateFilter` and `ApplyEmailDateFilter` private methods (depends on T002)

- [ ] T004 [P] Modify `src/Rentier.Application/Handlers/GetReportsQueryHandler.cs` — remove the `ComparisonOperator` switch for `FilingCountOperator` in the post-filter step; replace with a simple equality check `d.FilingCount == fcVal` using `query.Filter?.FilingCountValue` directly (depends on T002)

- [ ] T005 [P] Update `tests/Rentier.IntegrationTests/Repositories/ReportRepositoryTests.cs` — remove all operator-based date filter tests (`ApplyImportDateFilter` / `ApplyEmailDateFilter` variants); add tests: (1) `ImportDateContains = "2024-03"` returns only March 2024 reports, (2) `EmailDateContains` with a partial month string matches correctly, (3) `StatusFilters = { Init, Processed }` returns only those statuses, (4) `StatusFilters` containing all values returns all reports (same as no filter) (depends on T002, parallel with T003 and T004)

**Checkpoint**: All four projects compile cleanly with the new filter shape. Integration tests updated and green.

---

## Phase 3: User Story 1 — Filter Reports by Column via Header Flyout (Priority: P1) 🎯 MVP

**Goal**: Users can click the funnel icon in the Name, Importer, or Status column header; a flyout opens; they enter text or check statuses; they click "Primijeni" (Apply); the table filters server-side; and the funnel icon highlights in the accent color. Clicking outside the flyout without clicking Apply discards changes.

**Independent Test**: Open the reports page → click the funnel icon on the Name column → type "Godišnji" → click Apply → only matching reports appear and the Name funnel icon turns accent. Click the Status funnel → uncheck "Draft" → click Apply → no Draft reports appear and the Status funnel icon turns accent.

### Supporting Infrastructure for User Story 1

- [ ] T006 [P] [US1] Create `src/Rentier.Desktop/ViewModels/StatusCheckboxItem.cs` — sealed class extending `ReactiveObject` with: `ReportStatus Status { get; }`, `string DisplayName { get; }`, `bool IsChecked` (private `_isChecked = true`, `RaiseAndSetIfChanged`), and constructor `(ReportStatus status, string displayName)`

- [ ] T007 [P] [US1] Create `src/Rentier.Desktop/Converters/BoolToFilterBrushConverter.cs` — `IValueConverter` that returns `Application.Current.FindResource("RentierAccentBrush")` (or `SystemAccentColor` brush) when value is `true` and `Brushes.Transparent` (or the default foreground brush token) when `false`; handle null safely

- [ ] T008 [P] [US1] Add `FilterIcon` path geometry resource to `src/Rentier.Desktop/App.axaml` (funnel SVG `<StreamGeometry>` or `<PathGeometry>` keyed as `FilterIcon`) and register `BoolToFilterBrushConverter` in the app-level `ResourceDictionary` as `BoolToFilterBrushConverter`

- [ ] T009 [P] [US1] Add new resource string keys to `src/Rentier.Desktop/Resources/Strings.resx`: `Reports_Filter_Apply` = "Primijeni", `Reports_Filter_SelectAll` = "Odaberi sve", `Reports_Filter_ClearSelection` = "Očisti"; verify existing keys `Reports_Filter_Name_Watermark` and `Reports_Filter_Importer_Watermark` are present (add if missing with placeholder "Pretraži...")

### ViewModel for User Story 1

- [ ] T010 [US1] Modify `src/Rentier.Desktop/ViewModels/ReportsViewModel.cs` with the following changes (depends on T006):
  - **Remove**: `ComparisonOperator ImportDateOperator`, `ComparisonOperator EmailDateOperator`, `ComparisonOperator FilingCountOperator` properties and their backing fields; remove all `WhenAnyValue` subscriptions that watched these operator properties; remove `IReadOnlyList<ReportStatus?> StatusFilterOptions`; remove `ReportStatus? StatusFilter`; remove `DateOnly? ImportDateFilter` and `DateOnly? EmailDateFilter`
  - **Change type**: `int? FilingCountFilter` → `string? FilingCountFilterText`; `DateOnly? ImportDateFilter` → `string? ImportDateFilter`; `DateOnly? EmailDateFilter` → `string? EmailDateFilter`
  - **Add**: `ObservableCollection<StatusCheckboxItem> StatusCheckboxItems` initialized in constructor with all `ReportStatus` enum values (each mapped to its display name), all `IsChecked = true`
  - **Add**: `ReactiveCommand<Unit, Unit> ApplyFilterCommand` — on execute: call `BuildFilter()`, update internal filter state, then invoke `LoadPageCommand.Execute(Unit.Default)`; apply 300 ms `Throttle` on the observable pipeline to prevent double-click bursts
  - **Add**: `ReactiveCommand<Unit, Unit> SelectAllStatusesCommand` — sets all `StatusCheckboxItems[i].IsChecked = true`
  - **Add**: `ReactiveCommand<Unit, Unit> ClearAllStatusesCommand` — sets all `StatusCheckboxItems[i].IsChecked = false`
  - **Add**: computed `bool HasNameFilter` = `!string.IsNullOrWhiteSpace(NameFilter)`; same pattern for `HasImporterFilter`, `HasImportDateFilter`, `HasEmailDateFilter`, `HasFilingCountFilter` (non-empty and `int.TryParse` succeeds), `HasStatusFilter` (at least one item unchecked)
  - **Update `BuildFilter()`**: use `ImportDateContains`, `EmailDateContains`, `int.TryParse(FilingCountFilterText)`, and `StatusCheckboxItems.Where(s=>s.IsChecked).Select(s=>s.Status).ToHashSet()` with the `hasStatusFilter = checkedCount < totalCount` guard
  - **Update `ClearFiltersCommand`**: reset `NameFilter`, `ImporterFilter`, `ImportDateFilter`, `EmailDateFilter`, `FilingCountFilterText` to `null`/empty; set all `StatusCheckboxItems[i].IsChecked = true`; then execute `LoadPageCommand`
  - **Update `HasActiveFilters`**: recompute from any non-whitespace text filter OR any unchecked status item
  - **Update `WhenActivated`**: remove all per-property filter `WhenAnyValue` subscriptions that triggered `LoadPageCommand`; the sole trigger for filter-driven loads is now `ApplyFilterCommand`

### View for User Story 1

- [ ] T011 [US1] Modify `src/Rentier.Desktop/Views/ReportsView.axaml` (depends on T007, T008, T009, T010):
  - **Remove** the entire filter row `<Border>` block (the horizontal row of `TextBox`, `ComboBox`, `DatePicker`, and operator selector controls)
  - **Convert Name column** from `DataGridTextColumn` to `DataGridTemplateColumn`: header is a `StackPanel Orientation="Horizontal"` containing a `TextBlock` (column label) and a `Button` (transparent background, no border) whose content is a `PathIcon` with `Data="{StaticResource FilterIcon}"` and `Foreground` bound to `HasNameFilter` via `BoolToFilterBrushConverter` (using `RelativeSource AncestorType=DataGrid DataContext`); the `Button.Flyout` is a `<Flyout Placement="BottomEdgeAlignedLeft" ShowMode="Standard">` containing a `StackPanel` with a `TextBox` (watermark from `Reports_Filter_Name_Watermark`, `Text` TwoWay-bound to `NameFilter`) and a right-aligned `Button` (`Content` from `Reports_Filter_Apply`, `Command` bound to `ApplyFilterCommand`); cell template is a `DataTemplate` wrapping a `TextBlock` bound to `Name`
  - **Convert Importer column** from `DataGridTextColumn` to `DataGridTemplateColumn` using the same pattern with `ImporterFilter` and `HasImporterFilter` and watermark `Reports_Filter_Importer_Watermark`
  - **Convert Status column** from `DataGridTextColumn` to `DataGridTemplateColumn`: header flyout is a `StackPanel Width="200"` containing a row of `[Odaberi sve]` / `[Očisti]` link buttons bound to `SelectAllStatusesCommand` / `ClearAllStatusesCommand`, then an `ItemsControl` bound to `StatusCheckboxItems` with a `DataTemplate` containing a `CheckBox` (`Content="{Binding DisplayName}"`, `IsChecked="{Binding IsChecked, Mode=TwoWay}"`), then a right-aligned Apply button; funnel icon bound to `HasStatusFilter`

### Tests for User Story 1

- [ ] T012 [P] [US1] Update `tests/Rentier.UnitTests/Desktop/ReportsViewModelTests.cs` (depends on T010) — remove all operator-related test cases; add tests:
  1. `ApplyFilterCommand.Execute()` → `LoadPageCommand` is invoked
  2. `StatusCheckboxItems` contains one entry per `ReportStatus` value, all `IsChecked = true` on construction
  3. `HasNameFilter = true` when `NameFilter = "foo"`, `false` when `null` or whitespace
  4. `HasImporterFilter` mirrors `NameFilter` pattern
  5. `HasStatusFilter = true` when at least one `StatusCheckboxItem.IsChecked = false`
  6. `BuildFilter()` returns `null` when all text filters empty and all statuses checked
  7. `BuildFilter()` with `NameFilter = "Godišnji"` → `ReportColumnFilter.NameContains = "Godišnji"`
  8. `BuildFilter()` with some statuses unchecked → `ReportColumnFilter.StatusFilters` contains exactly the checked statuses
  9. `ClearFiltersCommand.Execute()` → all text filter properties become `null`/empty, all `StatusCheckboxItems.IsChecked = true`
  10. `HasActiveFilters = true` when `NameFilter` is non-empty; `= false` after `ClearFiltersCommand`

- [ ] T013 [P] [US1] Update `tests/Rentier.UnitTests/Desktop/ReportsViewHeadlessTests.cs` (depends on T011) — add assertions: (1) the filter row `Border` (previously containing inline `TextBox` controls) is no longer present in the rendered view tree; (2) the DataGrid has at least one `DataGridTemplateColumn` whose header visual tree contains a `Button` with a `Flyout`

**Checkpoint**: Name, Importer, and Status column flyouts work end-to-end. Active filter funnel icons highlight. Clear All Filters resets all. US1 independently testable.

---

## Phase 4: User Story 2 — Clear Individual and All Filters (Priority: P2)

**Goal**: A user with active filters on several columns can clear a single column's filter by opening its flyout, erasing the value, and clicking Apply — or clear all filters at once via the toolbar "Clear All Filters" button. The toolbar button is hidden/disabled when no filters are active.

**Independent Test**: Apply Name + Status filters → reopen Name flyout → clear text → Apply → Name filter gone but Status filter still active → click "Clear All Filters" → all filters cleared, all icons return to default color, toolbar button hides.

- [ ] T014 [US2] Verify and update `src/Rentier.Desktop/Views/ReportsView.axaml` — confirm the "Clear All Filters" toolbar `Button` has `IsVisible="{Binding HasActiveFilters}"` (or `IsEnabled`) correctly bound; if the binding is missing or stale after the filter row removal in T011, add/fix it; no other AXAML changes expected for this story

- [ ] T015 [P] [US2] Update `tests/Rentier.UnitTests/Desktop/ReportsViewModelTests.cs` — add tests:
  1. After `NameFilter = "test"` + `ApplyFilterCommand.Execute()`, set `NameFilter = null` + `ApplyFilterCommand.Execute()` → `HasNameFilter = false` and `LoadPageCommand` called with no name filter
  2. After unchecking one `StatusCheckboxItem`, `ClearFiltersCommand.Execute()` → all `IsChecked = true` and `HasStatusFilter = false`
  3. `HasActiveFilters = false` immediately after `ClearFiltersCommand.Execute()`
  4. `ClearFiltersCommand.Execute()` invokes `LoadPageCommand` (reload triggered after clear)

**Checkpoint**: Per-column filter clear and Clear All both work. Toolbar button visibility correct. US2 independently testable.

---

## Phase 5: User Story 3 — Date and Numeric Column Filtering (Priority: P3)

**Goal**: Users can filter the Import Date and Email Date columns using partial date text (e.g., "2024-03") matched as text-contains against the SQLite-stored "yyyy-MM-dd" format. Users can filter Filing Count by exact number; non-numeric input is silently ignored with no filter applied.

**Independent Test**: Enter "2024-03" in Import Date flyout → Apply → only March 2024 reports shown. Enter "5" in Filing Count flyout → Apply → only reports with 5 filings shown. Enter "abc" in Filing Count flyout → Apply → no filter applied and funnel icon stays default color.

- [ ] T016 [US3] Modify `src/Rentier.Desktop/Views/ReportsView.axaml` — convert ImportDate, EmailDate, and FilingCount columns from `DataGridTextColumn` to `DataGridTemplateColumn` (depends on T010, T011):
  - **Import Date flyout**: text `TextBox` with watermark from `Reports_Filter_Date_Watermark` ("Pretraži datum..."), `Text` TwoWay-bound to `ImportDateFilter`; funnel `PathIcon.Foreground` bound to `HasImportDateFilter` via `BoolToFilterBrushConverter`; Apply button bound to `ApplyFilterCommand`
  - **Email Date flyout**: same pattern using `EmailDateFilter` and `HasEmailDateFilter`
  - **Filing Count flyout**: text `TextBox` with watermark from `Reports_Filter_Count_Watermark` ("#"), `Text` TwoWay-bound to `FilingCountFilterText`; funnel `PathIcon.Foreground` bound to `HasFilingCountFilter` via `BoolToFilterBrushConverter`; Apply button bound to `ApplyFilterCommand`
  - Add resource keys `Reports_Filter_Date_Watermark` = "Pretraži datum..." and `Reports_Filter_Count_Watermark` = "#" to `src/Rentier.Desktop/Resources/Strings.resx` (if not already added in T009)

- [ ] T017 [P] [US3] Update `tests/Rentier.UnitTests/Desktop/ReportsViewModelTests.cs` — add tests (depends on T010):
  1. `ImportDateFilter = "2024-03"` → `BuildFilter()` returns `ReportColumnFilter` with `ImportDateContains = "2024-03"` and `HasImportDateFilter = true`
  2. `EmailDateFilter = "07"` → `BuildFilter()` returns `EmailDateContains = "07"` and `HasEmailDateFilter = true`
  3. `FilingCountFilterText = "5"` → `BuildFilter()` returns `FilingCountValue = 5` and `HasFilingCountFilter = true`
  4. `FilingCountFilterText = "abc"` → `BuildFilter()` returns `FilingCountValue = null` and `HasFilingCountFilter = false`
  5. `FilingCountFilterText = ""` → `HasFilingCountFilter = false` and `BuildFilter()` has no filing count filter
  6. `HasActiveFilters = true` when only `ImportDateFilter` is set; `= false` after clear

**Checkpoint**: Date and numeric column flyouts work. Numeric parse-or-ignore behaves correctly. US3 independently testable.

---

## Phase 6: User Story 4 — Debounced Apply (Priority: P3)

**Goal**: Rapid double-clicks on the Apply button in any flyout do not trigger multiple server-side filter requests. A 300 ms debounce/throttle is applied so that only one query fires per user action.

**Independent Test**: Rapidly click Apply 3 times in quick succession → only one `LoadPageCommand` execution is observed (via test scheduler or observable assertion).

- [ ] T018 [US4] Verify the 300 ms `Throttle` on `ApplyFilterCommand` pipeline is in place in `src/Rentier.Desktop/ViewModels/ReportsViewModel.cs` — if the command was wired without throttle in T010, add `Observable.FromAsync(() => ...).Throttle(TimeSpan.FromMilliseconds(300), RxApp.MainThreadScheduler)` or confirm `ReactiveCommand` `IsExecuting` guard already prevents concurrent execution; add an inline comment documenting the debounce strategy (D-006 from plan.md)

**Checkpoint**: Apply button debounce prevents duplicate server requests. US4 verified.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Final validation, cleanup, and confirmation of all cross-cutting requirements.

- [ ] T019 [P] Run full test suite: `dotnet test Rentier.slnx` — verify zero regressions; all new and updated tests green; Application coverage gate met (≥90% per CA-006)

- [ ] T020 Manual validation via quickstart.md — open the app, verify: (1) all 6 column headers (Name, Importer, Status, Import Date, Email Date, Filing Count) show funnel icons; (2) clicking each funnel opens the correct flyout type; (3) applying a filter highlights the icon in accent color; (4) clicking outside without Apply discards the change; (5) "Clear All Filters" toolbar button clears all, hides when no filters active; (6) pagination still works correctly with active filters

- [ ] T021 Code cleanup — search the codebase for any remaining references to removed properties: `ImportDateOperator`, `EmailDateOperator`, `FilingCountOperator`, `StatusFilter` (single), `DateOnly? ImportDateFilter`, `DateOnly? EmailDateFilter`; remove dead code, stale comments, and unused `using` directives in all modified files; verify no stale AXAML filter-row elements remain

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **Foundational (Phase 2)**: Depends on Setup — **BLOCKS all user story phases**; T003, T004, T005 are parallel with each other after T002
- **US1 (Phase 3)**: Depends on Foundational completion; T006–T010 can be parallel; T011 depends on T007, T008, T009, T010; T012–T013 depend on T010–T011
- **US2 (Phase 4)**: Depends on Phase 3 (ClearFiltersCommand and HasActiveFilters are in T010)
- **US3 (Phase 5)**: Depends on Phase 3 (ViewModel filter properties in T010); can proceed without US2
- **US4 (Phase 6)**: Depends on T010 (ApplyFilterCommand setup)
- **Polish (Phase 7)**: Depends on all desired user story phases complete

### User Story Dependencies

| Story | Blocks | Depends On |
|-------|--------|------------|
| US1 (P1) | US2, US3, US4 (shared ViewModel) | Foundational |
| US2 (P2) | Nothing | US1 (ClearFiltersCommand in T010) |
| US3 (P3) | Nothing | US1 (ViewModel filter props in T010) |
| US4 (P3) | Nothing | US1 (ApplyFilterCommand in T010) |

### Within Each Phase

- Parallel tasks (`[P]`) operate on different files with no in-flight dependencies
- T010 (ViewModel) is the single large-change task — complete it before T011 (View)
- Tests (`T012`, `T013`, `T015`, `T017`) can be written and executed in parallel with each other once their dependencies compile

---

## Parallel Execution Examples

### Foundational Phase (after T002 completes)

```
Parallel batch:
  Task: T003 — Update ReportRepository.cs (date LIKE, status IN)
  Task: T004 — Update GetReportsQueryHandler.cs (remove operator switch)
  Task: T005 — Update ReportRepositoryTests.cs (new integration tests)
```

### US1 Supporting Infrastructure (can start immediately after Foundational)

```
Parallel batch:
  Task: T006 — Create StatusCheckboxItem.cs
  Task: T007 — Create BoolToFilterBrushConverter.cs
  Task: T008 — Register FilterIcon + Converter in App.axaml
  Task: T009 — Add resource string keys to Strings.resx
Then:
  Task: T010 — Update ReportsViewModel.cs (depends on T006)
Then:
  Task: T011 — Update ReportsView.axaml (depends on T007, T008, T009, T010)
Parallel:
  Task: T012 — Update ReportsViewModelTests.cs
  Task: T013 — Update ReportsViewHeadlessTests.cs
```

### US3 + US4 (can run in parallel after US1)

```
Parallel batch:
  Task: T016 — Add date/numeric flyouts to ReportsView.axaml (US3)
  Task: T017 — Add date/numeric filter tests (US3)
  Task: T018 — Verify/add debounce in ReportsViewModel.cs (US4)
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (baseline green)
2. Complete Phase 2: Foundational — DTO + Infrastructure + Handler changes (**CRITICAL**)
3. Complete Phase 3: US1 — Name, Importer, Status flyouts + funnel icon visual state
4. **STOP and VALIDATE**: All 3 P1 flyouts work, funnel icons highlight, clear works
5. This alone eliminates the entire filter row and delivers full filtering for the most-used columns

### Incremental Delivery

1. Setup + Foundational → Application layer compiles with new filter shape
2. US1 (P1) → Core flyout interaction works for Name, Importer, Status ← **Demo point**
3. US2 (P2) → Clear per-column + Clear All verified ← **Demo point**
4. US3 (P3) → Date/numeric flyouts complete — all 6 columns filterable ← **Demo point**
5. US4 (P3) → Debounce hardened ← Polish
6. Polish → Full regression, cleanup, manual validation

---

## Notes

- `[P]` = different files, no dependency on incomplete tasks in the same batch
- `[US#]` maps each task to a specific user story for traceability
- The single biggest risk is T010 (ViewModel rewrite) — it touches many properties and subscriptions; complete and compile-verify it before starting T011 (View)
- The DTO change in T002 is a **compile-time breaking change** — all callers in the solution will fail to compile until T003 and T004 are also complete; complete T002–T004 as a unit before running the full build
- Flyout `DataContext` in column headers requires `RelativeSource AncestorType=DataGrid` binding pattern (see plan.md Layer 5 for full AXAML example)
- SQLite stores `DateOnly` as `"yyyy-MM-dd"` text — `EF.Functions.Like` text-contains on this format works correctly for partial date searches like `"2024-03"` (March 2024) or `"2024"` (full year)
- Commit after each phase checkpoint to enable easy rollback if a phase has issues
