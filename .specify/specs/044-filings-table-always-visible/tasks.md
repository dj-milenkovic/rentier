# Tasks: Filings Table Always Visible

**Feature**: `044-filings-table-always-visible`  
**Input**: `.specify/specs/044-filings-table-always-visible/` (spec.md, plan.md, research.md, data-model.md, quickstart.md, contracts/ui-contract.md)  
**Scope**: UI-only — `Rentier.Desktop` only (FR-007); no Domain, Application, or Infrastructure changes  
**Tech**: C# 12 / .NET 8, Avalonia UI 11+, ReactiveUI, FluentTheme  

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: Which user story this task belongs to (US1, US2, US3)
- Exact file paths are included in every task description

---

## Phase 1: Setup

**Purpose**: Confirm current view structure before making changes

- [X] T001 Inspect `src/Rentier.Desktop/Views/FilingsView.axaml` and note the exact line numbers for: (a) `IsVisible="{Binding HasItems}"` on the `<DataGrid>`, (b) the empty-state `<TextBlock IsVisible="{Binding IsEmpty}">`, and (c) their positions relative to each other inside the `DockPanel`

**Checkpoint**: Line numbers confirmed — implementation can begin

---

## Phase 3: User Story 1 — Empty Filings Page Shows Table Structure (Priority: P1) 🎯 MVP

**Goal**: DataGrid always renders with column headers visible regardless of row count. Removes the `IsVisible` binding that hides the table when no filings exist.

**Independent Test**: Navigate to the Filings page with zero filings → DataGrid with all column headers (selection checkbox, Status, Income Type, Paying Entity, Deadline, Tax Payable, Payment Reference, Actions) is visible on screen. No full-page "no data" placeholder replaces the table.

### Implementation for User Story 1

- [X] T002 [US1] Remove `IsVisible="{Binding HasItems}"` from the `<DataGrid x:Name="FilingsGrid">` element in `src/Rentier.Desktop/Views/FilingsView.axaml` — DataGrid defaults to `IsVisible="True"` and will always render

### Tests for User Story 1

- [X] T003 [US1] Add headless test `DataGrid_IsAlwaysVisible_WhenViewModelHasZeroRows` to `tests/Rentier.UnitTests/Desktop/Views/FilingsViewHeadlessTests.cs` — assert `FilingsGrid.IsVisible == true` and `FilingsGrid.IsEffectivelyVisible == true` when ViewModel's `Filings` collection is empty

**Checkpoint**: US1 complete — DataGrid visible with zero rows; all column headers render; existing layout unaffected

---

## Phase 4: User Story 2 — Polished Empty State Within Table (Priority: P2)

**Goal**: A subtle, muted empty-state message appears below the DataGrid (not replacing it) when no filings exist. Uses `RentierTextSecondaryBrush`, `FontSize="13"`, centered. The full-page placeholder that previously occupied the DataGrid's position is removed.

**Independent Test**: Navigate to Filings page with zero filings → column headers are visible AND a subtle "No filings yet." message appears below the grid area. Navigate with filings → message is absent, rows render normally.

**Dependency**: T002 must be complete before T004 (both modify `FilingsView.axaml` sequentially)

### Implementation for User Story 2

- [X] T004 [US2] Relocate the empty-state `<TextBlock>` in `src/Rentier.Desktop/Views/FilingsView.axaml` — move it from its current position **above** the `<DataGrid>` to **after** the `<DataGrid>` in the `DockPanel`, and restyle it:
  ```xml
  <TextBlock IsVisible="{Binding IsEmpty}"
             Text="{Binding [Filings_Empty], Source={StaticResource Localizer}}"
             HorizontalAlignment="Center"
             Foreground="{DynamicResource RentierTextSecondaryBrush}"
             FontSize="13"
             Margin="0,24" />
  ```
  Remove the old `DockPanel.Dock="Top"` and `VerticalAlignment="Center"` attributes that made it a full-page replacement.

- [X] T005 [P] [US2] Update `Filings_Empty` resource in `src/Rentier.Desktop/Resources/Strings.resx` — change value from `"No filings found."` to `"No filings yet."` to use softer, more welcoming phrasing per D-002

- [X] T006 [P] [US2] Update the sr-Latn translation for `Filings_Empty` in `src/Rentier.Desktop/Resources/SrLatnStrings.cs` to match the softened text from T005

### Tests for User Story 2

- [X] T007 [US2] Add headless test `EmptyStateMessage_IsVisibleBelowGrid_WhenNoFilings` to `tests/Rentier.UnitTests/Desktop/Views/FilingsViewHeadlessTests.cs` — assert the empty-state `TextBlock` is visible when `IsEmpty == true` and that the `FilingsGrid` is also still visible (both elements visible simultaneously)

- [X] T008 [US2] Add headless test `EmptyStateMessage_IsHidden_WhenFilingsExist` to `tests/Rentier.UnitTests/Desktop/Views/FilingsViewHeadlessTests.cs` — assert the empty-state `TextBlock` is not visible when the ViewModel's `Filings` collection contains at least one item

**Checkpoint**: US2 complete — table always visible, subtle empty hint below grid, full-page placeholder gone, localization updated

---

## Phase 5: User Story 3 — Consistent Table Behavior Across States (Priority: P3)

**Goal**: Table structure (headers, pagination) remains stable when transitioning between empty and populated states. No layout shift, no flicker. `IsLoading` does not interfere with DataGrid visibility.

**Independent Test**: Verify DataGrid visible during loading state (`IsLoading = true`, `Filings` empty) AND during populated state → empty transition. `HasItems` still correctly gates the select-all checkbox `IsEnabled`.

**Dependency**: T002 and T004 must be complete before T009

### Tests for User Story 3

- [X] T009 [US3] Add headless test `DataGrid_RemainsVisible_DuringLoadingState` to `tests/Rentier.UnitTests/Desktop/Views/FilingsViewHeadlessTests.cs` — set `IsLoading = true` and `Filings` empty on the ViewModel, assert `FilingsGrid.IsVisible == true` and empty-state `TextBlock` is not visible (loading + empty ≠ show empty message per ui-contract.md)

- [X] T010 [P] [US3] Verify all existing `IsEmpty` / `HasItems` property tests in `tests/Rentier.UnitTests/Desktop/FilingsViewModelTests.cs` still pass without modification — no ViewModel logic changes were made; this is a regression check (run `dotnet test --filter "FilingsViewModelTests"`)

- [X] T011 [P] [US3] Add headless test `SelectAllCheckbox_IsDisabled_WhenNoFilings` to `tests/Rentier.UnitTests/Desktop/Views/FilingsViewHeadlessTests.cs` — assert the select-all checkbox `IsEnabled` is still correctly bound to `HasItems` (unchanged by this feature) to guard against accidental removal of the `HasItems` binding from the wrong element

**Checkpoint**: US3 complete — all three page states (empty, loading, populated) verified stable; ViewModel properties unchanged

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Final validation across all user stories; confirm no regressions

- [X] T012 Run full test suite `dotnet test tests/Rentier.UnitTests` and confirm all tests pass (zero failures, zero skips introduced by this feature)

- [X] T013 Perform quickstart.md validation: (1) launch app with empty database → navigate to Filings page → confirm DataGrid with column headers visible and "No filings yet." message below, (2) add a filing → confirm table populates without layout shift, (3) delete all filings → confirm table remains visible with empty-state message re-appearing

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **US1 (Phase 3)**: Can start after Setup — **no foundational blocking phase needed** (UI-only, single file)
- **US2 (Phase 4)**: T004 depends on T002 (same file, sequential edits); T005/T006 are parallel and independent
- **US3 (Phase 5)**: T009 depends on T002 + T004; T010/T011 are parallel
- **Polish (Phase 6)**: Depends on all story phases complete

### User Story Dependencies

- **US1 (P1)**: Starts after Phase 1 — no story dependencies
- **US2 (P2)**: T004 sequentially after T002 (same AXAML file); T005/T006 parallel with T004
- **US3 (P3)**: After T002 and T004 complete

### Within Each User Story

- AXAML edits to the same file must be sequential (T002 → T004 → nothing else touches FilingsView.axaml)
- Localization files (T005, T006) are independent of AXAML edits — can proceed in parallel
- Tests can be written after implementation tasks complete

### Parallel Opportunities

```
T001 (inspect)
  └─► T002 [US1] Remove IsVisible from DataGrid
        ├─► T003 [US1] Headless test: DataGrid visible when empty
        └─► T004 [US2] Relocate + restyle TextBlock        ┐ T005 [P] [US2] Strings.resx update
                  ├─► T007 [US2] Headless: both visible   ├─ T006 [P] [US2] SrLatnStrings.cs
                  ├─► T008 [US2] Headless: message hidden  ┘
                  └─► T009 [US3] Headless: loading state
                        ├─► T010 [P] [US3] Confirm ViewModel tests pass
                        └─► T011 [P] [US3] SelectAll checkbox still bound
T012 (full test run) → T013 (quickstart validation)
```

---

## Implementation Strategy

### MVP First (User Story 1 Only) — ~15 minutes

1. Complete Phase 1: Inspect FilingsView.axaml (T001)
2. Complete Phase 3: Remove `IsVisible` binding + add headless test (T002, T003)
3. **STOP and VALIDATE**: Run app → Filings page empty → confirm headers visible
4. Deploy/demo US1 immediately — this is a one-line AXAML change with full value

### Incremental Delivery

1. T001 → T002 → T003: US1 done — table always visible ✅
2. T004 → T005/T006 → T007/T008: US2 done — polished empty state ✅
3. T009 → T010/T011: US3 done — all states verified ✅
4. T012 → T013: Full validation ✅

### Single-Developer Sequence (recommended)

```
T001 → T002 → T003 → T004 → T005+T006 (parallel) → T007 → T008 → T009 → T010+T011 (parallel) → T012 → T013
```

---

## Summary

| Phase | Story | Tasks | Files |
|-------|-------|-------|-------|
| Setup | — | T001 | FilingsView.axaml (read) |
| Phase 3 | US1 (P1) 🎯 | T002–T003 | FilingsView.axaml, FilingsViewHeadlessTests.cs |
| Phase 4 | US2 (P2) | T004–T008 | FilingsView.axaml, Strings.resx, SrLatnStrings.cs, FilingsViewHeadlessTests.cs |
| Phase 5 | US3 (P3) | T009–T011 | FilingsViewHeadlessTests.cs, FilingsViewModelTests.cs |
| Polish | — | T012–T013 | (validation only) |

**Total tasks**: 13  
**Parallel opportunities**: T005+T006, T010+T011  
**MVP scope**: T001 → T002 → T003 (3 tasks, one AXAML line removed)  
**Layers touched**: `Rentier.Desktop` only — Clean Architecture boundary preserved ✅

---

## Notes

- [P] tasks = different files, no incomplete task dependencies
- [Story] label maps each task to a specific user story for traceability
- US1 (T002) is a one-line AXAML change — highest value, lowest risk
- No ViewModel logic changes in any task — `HasItems` and `IsEmpty` properties are untouched
- The `FilingsViewHeadlessTests.cs` file may need to be created if it does not already exist at `tests/Rentier.UnitTests/Desktop/Views/`
- Commit after T002+T003 (US1 complete), again after T004–T008 (US2 complete), again after T009–T011 (US3 complete)
