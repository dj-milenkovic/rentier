# Tasks: Holidays Settings — Year-Range Filter & UX Improvements

**Input**: Design documents from `.specify/specs/024-holidays-settings-year-range-filter-ux/`
**Branch**: `feature/021-024-qa-fixes`
**Prerequisites**: plan.md ✅, spec.md ✅
**Scope**: Desktop layer only — `Rentier.Desktop` ViewModel + View + Resources + Desktop.Tests

**Tests**: Included — CA-006 explicitly requires Desktop-layer unit tests for ViewModel filtering logic.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no incomplete-task dependencies)
- **[Story]**: Which user story this task belongs to (US1–US4)

---

## Phase 1: Setup — Resource Strings (Foundational)

**Purpose**: Add the two new localised strings that US2 and US3 view tasks both depend on. Must complete before any View work for those stories begins.

- [ ] T001 Add `Holidays_YearRange_HelperText` ("Showing holidays for the selected year range. This range also determines which years are pre-seeded on first run.") and `Holidays_FilteredEmpty_Message` ("No holidays configured for the selected year range.") entries to `src/Rentier.Desktop/Resources/Strings.resx`

**Checkpoint**: Both string keys accessible via `Strings.*` — US2 and US3 view tasks can now proceed.

---

## Phase 2: User Story 1 — Filter Holidays by Year Range (Priority: P1) 🎯 MVP

**Goal**: The DataGrid immediately reflects only holidays whose `Date.Year` falls within `[StartYear, EndYear]` whenever either selector changes or the underlying collection changes.

**Independent Test**: Load the Holidays page with holidays spanning 2024–2026. Set `StartYear = 2025`, `EndYear = 2025` → DataGrid shows only 2025 entries. Change `EndYear = 2026` → 2025 and 2026 entries both appear. Add a 2023 entry with the filter active → it does not appear in the grid.

### Tests for User Story 1

- [ ] T002 [P] [US1] Add `FilteredEntries_ReturnsOnlyEntriesWithinRange`, `FilteredEntries_UpdatesWhenStartYearChanges`, `FilteredEntries_UpdatesWhenEndYearChanges` unit tests to `tests/Rentier.Desktop.Tests/HolidaySettingsViewModelTests.cs`
- [ ] T003 [P] [US1] Add `FilteredEntries_UpdatesWhenEntryAddedInRange`, `FilteredEntries_ExcludesEntryAddedOutsideRange`, `FilteredEntries_EmptyWhenStartYearExceedsEndYear` unit tests to `tests/Rentier.Desktop.Tests/HolidaySettingsViewModelTests.cs`

### Implementation for User Story 1

- [ ] T004 [US1] Add `FilteredEntries` property (`ObservableCollection<HolidayEntryViewModel>`), private `RebuildFilteredEntries()` method (LINQ Where `e.Date.Year >= StartYear && e.Date.Year <= EndYear`), and wire `Entries.CollectionChanged` to call `RebuildFilteredEntries()` in `src/Rentier.Desktop/ViewModels/HolidaySettingsViewModel.cs`
- [ ] T005 [US1] Add `WhenAnyValue(x => x.StartYear, x => x.EndYear).Subscribe(_ => RebuildFilteredEntries())` subscription in the constructor (after AddRow/Delete/Save/Import command setup) in `src/Rentier.Desktop/ViewModels/HolidaySettingsViewModel.cs`
- [ ] T006 [P] [US1] Rebind `DataGrid.ItemsSource` from `{Binding Entries}` to `{Binding FilteredEntries}` in `src/Rentier.Desktop/Views/HolidaySettingsView.axaml`

**Checkpoint**: User Story 1 fully functional — DataGrid updates on year-range change, add, and delete.

---

## Phase 3: User Story 2 — Empty State for No Matching Holidays (Priority: P1)

**Goal**: When the filter is active but no holidays match the selected range, a clear "no results for this range" placeholder replaces the empty grid. The existing generic "no holidays configured" message still shows when `Entries` is completely empty.

**Independent Test**: Set `StartYear = 2027`, `EndYear = 2028` with holidays only in 2024–2025 → range-specific placeholder appears. Switch range back to 2024–2025 → placeholder disappears and DataGrid shows entries. With an empty `Entries` collection → generic placeholder appears (not the range-specific one).

### Tests for User Story 2

- [ ] T007 [P] [US2] Add `IsFilteredEmpty_TrueWhenEntriesExistButNoneMatchRange`, `IsFilteredEmpty_FalseWhenEntriesMatchRange`, `IsFilteredEmpty_FalseWhenEntriesCollectionIsEmpty` (generic empty-state path), `IsFilteredEmpty_TrueWhenStartYearExceedsEndYear` unit tests to `tests/Rentier.Desktop.Tests/HolidaySettingsViewModelTests.cs`

### Implementation for User Story 2

- [ ] T008 [US2] Add `HasFilteredItems` (`FilteredEntries.Count > 0`) and `IsFilteredEmpty` (`HasItems && !HasFilteredItems`) computed bool properties, raise `PropertyChanged` for both from `RebuildFilteredEntries()` in `src/Rentier.Desktop/ViewModels/HolidaySettingsViewModel.cs`
- [ ] T009 [P] [US2] Add range-specific empty-state `TextBlock` (`Text="{x:Static res:Strings.Holidays_FilteredEmpty_Message}"`, `IsVisible="{Binding IsFilteredEmpty}"`) in the `DockPanel` above the `DataGrid`, and add `IsVisible="{Binding HasFilteredItems}"` to the existing `DataGrid` so it hides when the filtered list is empty in `src/Rentier.Desktop/Views/HolidaySettingsView.axaml`

**Checkpoint**: Both empty-state messages work independently — range-specific vs. generic.

---

## Phase 4: User Story 3 — Helper Text Explains Year-Range Purpose (Priority: P2)

**Goal**: A short informational `TextBlock` is permanently visible below the year-range selectors, explaining the dual purpose of the range (filter display + pre-seed years).

**Independent Test**: Open the Holidays settings page → helper text is visible below the Start/End Year controls at all times regardless of filter state.

### Implementation for User Story 3

- [ ] T010 [P] [US3] Add a helper `TextBlock` (`Text="{x:Static res:Strings.Holidays_YearRange_HelperText}"`, small `FontSize`, reduced `Opacity`, `Margin="8,0,8,4"`) immediately after the year-range `StackPanel` and before the separator in `src/Rentier.Desktop/Views/HolidaySettingsView.axaml` — depends on T001

**Checkpoint**: Helper text visible on page load; no binding or null-ref errors.

---

## Phase 5: User Story 4 — Improved Layout with Visual Separation (Priority: P2)

**Goal**: A visible separator cleanly divides the year-range controls from the data grid. Start Year / End Year labels are aligned and fully visible at narrow window sizes.

**Independent Test**: Open Holidays settings at minimum window width → both labels fully visible, no clipping. A horizontal line (or equivalent) is visible between the year controls block and the DataGrid.

### Implementation for User Story 4

- [ ] T011 [P] [US4] Add a `<Rectangle Height="1" Fill="{DynamicResource SystemControlForegroundBaseMediumLowBrush}" Margin="8,4" DockPanel.Dock="Top" />` (or `<Separator DockPanel.Dock="Top" Margin="8,2" />`) between the helper-text block and the DataGrid in `src/Rentier.Desktop/Views/HolidaySettingsView.axaml`
- [ ] T012 [P] [US4] Fix label alignment: replace the year-range `StackPanel` with a two-column `Grid` (column 0: label `MinWidth="80"`, column 1: `NumericUpDown`) so labels cannot be clipped at narrow widths, in `src/Rentier.Desktop/Views/HolidaySettingsView.axaml`

**Checkpoint**: Visual hierarchy matches spec (toolbar → controls + helper text → separator → grid). Labels readable at minimum width.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [ ] T013 [P] Add `SaveCommand_PersistsFullEntries_WhenFilterIsActive` unit test asserting that `SaveHolidayConfCommand.Holidays` count equals `Entries.Count` (not `FilteredEntries.Count`) when a year-range filter is in effect, in `tests/Rentier.Desktop.Tests/HolidaySettingsViewModelTests.cs`
- [ ] T014 Run `dotnet test tests/Rentier.Desktop.Tests --no-build` from repo root and confirm all new and existing tests pass (zero regressions)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: No dependencies — start immediately
- **Phase 2 (US1)**: Depends on Phase 1 only for the ViewModel work; T002–T003 (tests) and T004–T005 (VM) can start after Phase 1; T006 (View rebind) can be done in parallel with T004–T005 since it touches only the View file
- **Phase 3 (US2)**: Depends on Phase 2 (FilteredEntries must exist before `HasFilteredItems`/`IsFilteredEmpty` are derived from it); T007 test and T008 VM can start immediately after T004–T005; T009 View depends on T001 (string resource)
- **Phase 4 (US3)**: Depends on T001 (string resource) only — `TextBlock` addition is independent of US1/US2 VM work
- **Phase 5 (US4)**: No code dependencies — pure layout changes in the View; can be done after Phase 4 so the helper text and separator are added in one coherent pass
- **Phase 6 (Polish)**: Depends on all preceding phases

### User Story Dependencies

- **US1 (P1)**: Independent — only needs Phase 1 (strings not required for US1)
- **US2 (P1)**: Depends on US1 VM work (needs `FilteredEntries` to derive `IsFilteredEmpty` from); View change needs T001 (strings)
- **US3 (P2)**: Depends on T001 (strings); otherwise independent of US1/US2
- **US4 (P2)**: Fully independent of US1–US3 logic; pure layout

### Within Each User Story

- Tests (T002, T003, T007, T013) must be written before implementation so failures can be confirmed
- ViewModel properties before View bindings within the same story
- `RebuildFilteredEntries()` in T004 is the shared engine — T005 (hook-up), T006 (View), T008 (empty-state), T009 (View) all depend on it

### Parallel Opportunities

Within Phase 2:
- T002 + T003 (test authoring) can run in parallel with each other (same file but non-overlapping test methods)
- T004 + T005 (VM implementation) are sequential in the same file
- T006 (View rebind) can be done in parallel with T004–T005 (different file)

Within Phase 3:
- T007 (tests) in parallel with T008 (VM) — different concerns in same file but non-overlapping
- T009 (View) in parallel with T007–T008

Phases 4 and 5 are both View-only and can be done in a single pass once Phases 2–3 are complete.

---

## Parallel Example: User Story 1

```text
# All can be launched together once Phase 1 (T001) is done:
Task A: Write FilteredEntries basic tests → T002
Task B: Write FilteredEntries edge-case tests → T003
Task C: Add FilteredEntries property + RebuildFilteredEntries() → T004 (then T005)

# After T004 completes, T006 can run in parallel with T005:
Task D: Rebind DataGrid to FilteredEntries in View → T006
```

---

## Implementation Strategy

### MVP First (US1 + US2 — core defect fix)

1. Complete Phase 1: Add resource strings (T001)
2. Complete Phase 2: FilteredEntries + tests + View rebind (T002–T006)
3. Complete Phase 3: Empty-state properties + tests + View placeholder (T007–T009)
4. **STOP and VALIDATE**: Test US1 and US2 independently per acceptance scenarios in spec.md
5. US3 and US4 can follow as a second pass (UX polish)

### Incremental Delivery

1. T001 → Foundation ready (strings in place)
2. T002–T006 (US1) → Filtering functional → DataGrid reactive to year selectors
3. T007–T009 (US2) → Empty-state distinguishes filter-empty from data-empty
4. T010 (US3) → Helper text explains controls purpose
5. T011–T012 (US4) → Separator + label alignment
6. T013–T014 (Polish) → Verification pass, no regressions

---

## Notes

- `SaveCommand` intentionally reads `Entries` (not `FilteredEntries`) — persists all holidays regardless of current filter. T013 verifies this invariant is not accidentally broken.
- `RebuildFilteredEntries()` is a synchronous LINQ `.Where()` on a small collection (~10–200 items). No `async`, no I/O, no scheduler needed — consistent with CA-005.
- The existing `HasItems` property (`Entries.Count > 0`) is unchanged and still drives the generic empty-state `TextBlock`.
- `IsFilteredEmpty = HasItems && !HasFilteredItems` ensures the range-specific message only appears when data exists but none matches — never shown on a truly empty list.
- `WhenAnyValue(StartYear, EndYear).Subscribe(...)` placed in the constructor (not `WhenActivated`) so filtering is live even before activation, consistent with how `Entries.CollectionChanged` is already wired in the constructor.
- All new resource strings must be added to `Strings.resx` (not hard-coded in AXAML) per FR-008.
- [P] tasks = different files or non-overlapping regions — safe to implement simultaneously
- [US*] labels map directly to user stories in spec.md for full traceability
