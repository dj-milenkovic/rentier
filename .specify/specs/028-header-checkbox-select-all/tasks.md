# Tasks: Header Checkbox for Select All / Clear All

**Input**: Design documents from `.specify/specs/028-header-checkbox-select-all/`
**Branch**: `feat/027-031-ux-improvements`
**Prerequisites**: plan.md, spec.md, data-model.md, research.md, quickstart.md
**Tests**: Included per CA-006 (9 new `IsAllSelected_*` test methods per BulkDelete test file)
**Scope**: Desktop layer only -- 2 ViewModels, 2 Views, 2 UnitTest files. Zero Domain/Application/Infrastructure changes.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no shared in-flight state)
- **[Story]**: User story label (US1-US4) matching spec.md priorities
- Exact file paths are included in every task description

---

## Phase 1: Setup

**Purpose**: Establish a green baseline before modifying any file.

- [X] T001 Verify all existing tests pass before any changes by running `dotnet test tests/Rentier.UnitTests --filter FullyQualifiedName~BulkDelete` from repo root; confirm 17 pass in `tests/Rentier.UnitTests/Desktop/FilingsViewModelBulkDeleteTests.cs` and 12 pass in `tests/Rentier.UnitTests/Desktop/ReportsViewModelBulkDeleteTests.cs`; record the green baseline count

---

## Phase 2: Foundational -- `IsAllSelected` ViewModel Property

**Purpose**: Add the `bool?` tri-state property (getter + setter + re-entrancy guard + reactive notification) to both ViewModels. This property backs all four user stories; no story can be verified until it exists.

**Checkpoint**: After this phase `dotnet build Rentier.slnx` must succeed with zero errors.

**Note**: T002 and T003 touch different files -- can run simultaneously.

- [X] T002 [P] Add `private bool _isUpdatingSelection;` guard field and `public bool? IsAllSelected` property to `src/Rentier.Desktop/ViewModels/FilingsViewModel.cs`; getter returns `false` when `Rows.Count == 0` or `SelectedCount == 0`, returns `true` when `SelectedCount == Rows.Count`, returns `null` otherwise; setter calls `SelectAllCommand.Execute().Subscribe()` on `true`, `ClearSelectionCommand.Execute().Subscribe()` on `false`, ignores `null`; wrap setter body in `_isUpdatingSelection` guard with try/finally that always ends with `this.RaisePropertyChanged(nameof(IsAllSelected))`; also extend `RebuildRowSubscriptions()` existing per-row lambda to call `this.RaisePropertyChanged(nameof(IsAllSelected))` immediately after `SelectedCount = Rows.Count(r => r.IsSelected)` (see quickstart.md implementation pattern)
- [X] T003 [P] Add the identical `_isUpdatingSelection` guard, `IsAllSelected` property, and `RebuildRowSubscriptions()` notification to `src/Rentier.Desktop/ViewModels/ReportsViewModel.cs` following the exact same pattern as T002

---

## Phase 3: User Story 1 -- Select All Rows via Header Checkbox (Priority: P1) MVP

**Goal**: Clicking an unchecked or indeterminate header checkbox selects every row and transitions the header to a fully-checked state.

**Independent Test**: Load the Filings page with 5 rows and no rows selected; click the header checkbox; verify all 5 row checkboxes are checked and the header shows a checked state. Repeat on the Reports page with 10 rows.

**Note**: All four tasks in this phase touch different files and can run simultaneously after Phase 2.

### Tests for User Story 1

- [X] T004 [P] [US1] Add four test methods to `tests/Rentier.UnitTests/Desktop/FilingsViewModelBulkDeleteTests.cs`: (a) `IsAllSelected_WhenNoRowsSelected_ReturnsFalse` -- seed 5 rows with none selected, assert `IsAllSelected == false`; (b) `IsAllSelected_WhenAllRowsSelected_ReturnsTrue` -- seed 5 rows with all selected, assert `IsAllSelected == true`; (c) `IsAllSelected_SetTrue_SelectsAllRows` -- seed 5 rows with none selected, set `IsAllSelected = true`, assert `SelectedCount == 5` and `IsAllSelected == true`; (d) `IsAllSelected_SetTrue_FromIndeterminate_SelectsAllRows` -- seed 5 rows with 2 selected, set `IsAllSelected = true`, assert `SelectedCount == 5` and `IsAllSelected == true`
- [X] T005 [P] [US1] Add the same four `IsAllSelected_*` test methods to `tests/Rentier.UnitTests/Desktop/ReportsViewModelBulkDeleteTests.cs` using `ReportsViewModel` and `ReportRowViewModel` stubs

### Implementation for User Story 1

- [X] T006 [P] [US1] Add a `DataGridTemplateColumn.Header` element containing a tri-state CheckBox to the selection column (Width=40) in `src/Rentier.Desktop/Views/FilingsView.axaml`; the CheckBox must have: `IsThreeState="True"`, `IsChecked` bound TwoWay to `DataContext.IsAllSelected` via `RelativeSource={RelativeSource AncestorType=DataGrid}`, `IsEnabled` bound to `DataContext.HasItems` via the same RelativeSource, `HorizontalAlignment="Center"`, `VerticalAlignment="Center"`; the existing `DataGridTemplateColumn.CellTemplate` for per-row checkboxes must remain unchanged (see quickstart.md XAML pattern)
- [X] T007 [P] [US1] Add the identical header CheckBox to the selection column in `src/Rentier.Desktop/Views/ReportsView.axaml` using the same `RelativeSource AncestorType=DataGrid` binding pattern already in use for command bindings on that file (~line 116)

**Checkpoint**: After T004-T007, open both pages; clicking the unchecked header selects all rows. Run `dotnet test tests/Rentier.UnitTests --filter FullyQualifiedName~BulkDelete` -- 8 new US1 tests pass (4 per ViewModel).

---

## Phase 4: User Story 2 -- Deselect All Rows via Header Checkbox (Priority: P1)

**Goal**: Clicking a fully-checked header checkbox clears the entire selection and the header transitions to an unchecked state.

**Independent Test**: Select all rows on the Filings page; click the checked header checkbox; verify all row checkboxes become unchecked and the header shows an unchecked state. Repeat on the Reports page.

**Note**: The `value == false` setter path is already implemented by T002/T003. This phase adds the verifying tests only.

### Tests for User Story 2

- [X] T008 [P] [US2] Add `IsAllSelected_SetFalse_DeselectsAllRows` test to `tests/Rentier.UnitTests/Desktop/FilingsViewModelBulkDeleteTests.cs`; seed 5 rows with all selected (`SelectedCount == 5`), set `IsAllSelected = false`, assert `SelectedCount == 0` and `IsAllSelected == false`
- [X] T009 [P] [US2] Add `IsAllSelected_SetFalse_DeselectsAllRows` test to `tests/Rentier.UnitTests/Desktop/ReportsViewModelBulkDeleteTests.cs` using the same arrange/act/assert pattern

**Checkpoint**: Run `dotnet test tests/Rentier.UnitTests --filter FullyQualifiedName~BulkDelete` -- 10 new tests pass (8 US1 + 2 US2). Clicking the checked header on either page deselects all rows.

---

## Phase 5: User Story 3 -- Visual Indication of Partial Selection (Priority: P2)

**Goal**: When the user manually toggles individual row checkboxes to create a partial selection, the header checkbox automatically updates to the indeterminate (dash) state.

**Independent Test**: On the Filings page with 5 rows, check 2 rows manually; verify the header shows an indeterminate indicator. Uncheck both; verify header returns to unchecked. Check all 5 one-by-one; verify header transitions to fully checked on the last row.

**Note**: The `null` getter path is already implemented by T002/T003 and `RebuildRowSubscriptions` already fires on every per-row toggle. This phase adds the verifying tests only.

### Tests for User Story 3

- [X] T010 [P] [US3] Add `IsAllSelected_WhenSomeRowsSelected_ReturnsNull` to `tests/Rentier.UnitTests/Desktop/FilingsViewModelBulkDeleteTests.cs`; seed 5 rows, select 2, assert `IsAllSelected == null`
- [X] T011 [P] [US3] Add `IsAllSelected_WhenSomeRowsSelected_ReturnsNull` to `tests/Rentier.UnitTests/Desktop/ReportsViewModelBulkDeleteTests.cs`
- [X] T012 [P] [US3] Add `IsAllSelected_UpdatesWhenRowSelectionChanges` to `tests/Rentier.UnitTests/Desktop/FilingsViewModelBulkDeleteTests.cs`; toggle rows programmatically and read `IsAllSelected` directly to verify all four state transitions: 0/5 selected -> `false`, then 2/5 -> `null`, then 5/5 -> `true`, then back to 0/5 -> `false`
- [X] T013 [P] [US3] Add `IsAllSelected_UpdatesWhenRowSelectionChanges` to `tests/Rentier.UnitTests/Desktop/ReportsViewModelBulkDeleteTests.cs` using the same transition sequence

**Checkpoint**: Run `dotnet test tests/Rentier.UnitTests --filter FullyQualifiedName~BulkDelete` -- 14 new tests pass. Manually checking individual rows causes the header checkbox to show the indeterminate indicator.

---

## Phase 6: User Story 4 -- Toolbar Cleanup (Priority: P2)

**Goal**: Remove the now-redundant Select All and Clear Selection text buttons from both page toolbars; the Delete Selected (N) button and all remaining toolbar elements must stay intact.

**Independent Test**: Open the Filings page; confirm the toolbar contains no Select All or Clear Selection button; confirm Delete Selected (N) is present and enables/disables correctly based on selection. Repeat on the Reports page.

**Dependency**: Phase 3 (US1) must be complete -- the header checkbox must be in place before toolbar buttons are removed.

### Implementation for User Story 4

- [X] T014 [P] [US4] Remove the two `<Button>` elements bound to `SelectAllCommand` and `ClearSelectionCommand` from the toolbar StackPanel/DockPanel in `src/Rentier.Desktop/Views/FilingsView.axaml`; verify the Delete Selected button binding (`BulkDeleteCommand`) and its `IsEnabled`/visibility binding on `HasSelection` remain untouched; leave `BulkDelete_SelectAll_Button` and `BulkDelete_ClearSelection_Button` resource string keys in Strings.resx with a comment marking them as unused (retained for potential tooltip/accessibility use)
- [X] T015 [P] [US4] Remove the same two `<Button>` elements (`SelectAllCommand`, `ClearSelectionCommand`) from the toolbar in `src/Rentier.Desktop/Views/ReportsView.axaml`; keep all other toolbar elements including the Delete Selected button unchanged

**Checkpoint**: AXAML diff shows button removals only; no other markup changed. Both pages load without the removed buttons. All BulkDelete tests still pass.

---

## Phase 7: Polish and Edge Cases

**Purpose**: Cover the two spec edge-case requirements (FR-015 empty state, FR-016 post-reload recalculation) with tests, then do a full build and test run.

- [X] T016 [P] Add `IsAllSelected_WhenNoRows_ReturnsFalse` to `tests/Rentier.UnitTests/Desktop/FilingsViewModelBulkDeleteTests.cs`; construct a `FilingsViewModel` with zero rows, assert `IsAllSelected == false` and no exception is thrown (FR-015 empty-state guard)
- [X] T017 [P] Add `IsAllSelected_WhenNoRows_ReturnsFalse` to `tests/Rentier.UnitTests/Desktop/ReportsViewModelBulkDeleteTests.cs` using the same empty-state pattern
- [X] T018 [P] Add `IsAllSelected_RecalculatesAfterRowsReloaded` to `tests/Rentier.UnitTests/Desktop/FilingsViewModelBulkDeleteTests.cs`; seed 3 rows, select all (`IsAllSelected == true`), then simulate a reload by clearing Rows and re-populating with 3 new unselected rows the same way LoadPageAsync does, assert `SelectedCount == 0` and `IsAllSelected == false` (FR-016)
- [X] T019 [P] Add `IsAllSelected_RecalculatesAfterRowsReloaded` to `tests/Rentier.UnitTests/Desktop/ReportsViewModelBulkDeleteTests.cs`
- [X] T020 Run `dotnet build Rentier.slnx` from repo root; confirm zero compilation errors and zero new warnings introduced by this feature
- [X] T021 Run `dotnet test tests/Rentier.UnitTests --filter FullyQualifiedName~BulkDelete`; confirm total pass counts: 26 in FilingsViewModelBulkDeleteTests.cs (17 original + 9 new) and 21 in ReportsViewModelBulkDeleteTests.cs (12 original + 9 new)
- [ ] T022 Execute the 7 manual verification scenarios from quickstart.md: (1) empty page -- header checkbox visible but disabled; (2) click unchecked header -- all rows selected, header checked; (3) click checked header -- all rows deselected, header unchecked; (4) manually check 2 of 5 rows -- header shows indeterminate dash; (5) click indeterminate header -- all rows selected; (6) toolbar on both pages has no Select All or Clear Selection button; (7) Delete Selected (N) button still present and functional

---

## Dependencies and Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: No dependencies -- start immediately
- **Phase 2 (Foundational)**: Depends on Phase 1 -- **BLOCKS all user story phases**
- **Phase 3 (US1, P1)**: Depends on Phase 2 -- MVP increment; no dependency on US2, US3, or US4
- **Phase 4 (US2, P1)**: Depends on Phase 2 -- no dependency on Phase 3 (setter covers both paths)
- **Phase 5 (US3, P2)**: Depends on Phase 2 -- no dependency on Phase 3 or 4
- **Phase 6 (US4, P2)**: Depends on Phase 3 (US1) -- header checkbox must exist before toolbar buttons are removed
- **Phase 7 (Polish)**: Depends on all preceding phases

### User Story Dependencies

- **US1 (P1)**: Requires Phase 2 completion; no dependency on other stories
- **US2 (P1)**: Requires Phase 2 completion; no dependency on US1
- **US3 (P2)**: Requires Phase 2 completion; no dependency on US1 or US2
- **US4 (P2)**: Requires US1 (Phase 3) completion -- header checkbox must be in place before toolbar removal is safe

### Parallel Opportunities Within Phases

| Tasks | Relationship |
|-------|-------------|
| T002 vs T003 | Parallel (different ViewModel files) |
| T004 vs T005 vs T006 vs T007 | All parallel within Phase 3 (all different files) |
| T008 vs T009 | Parallel (different test files) |
| T010 vs T011 vs T012 vs T013 | All parallel (different test files) |
| T014 vs T015 | Parallel (different View files) |
| T016 vs T017 vs T018 vs T019 | All parallel (different test files) |
| T020 -> T021 -> T022 | Sequential (build then test then manual verify) |

---

## Parallel Execution Examples

### Phase 2: Foundational (2 agents simultaneously)

```text
Agent A: T002 -- src/Rentier.Desktop/ViewModels/FilingsViewModel.cs  (add IsAllSelected)
Agent B: T003 -- src/Rentier.Desktop/ViewModels/ReportsViewModel.cs  (add IsAllSelected)
```

### Phase 3: US1 (4 agents simultaneously after Phase 2)

```text
Agent A: T004 -- tests/.../FilingsViewModelBulkDeleteTests.cs  (US1 tests)
Agent B: T005 -- tests/.../ReportsViewModelBulkDeleteTests.cs  (US1 tests)
Agent C: T006 -- src/.../Views/FilingsView.axaml               (header checkbox)
Agent D: T007 -- src/.../Views/ReportsView.axaml               (header checkbox)
```

### Phases 4 and 5: US2 and US3 (can run in parallel with Phase 3 after Phase 2)

```text
Agent A: T008, T009 -- US2 deselect tests       (depends only on Phase 2)
Agent B: T010-T013  -- US3 indeterminate tests  (depends only on Phase 2)
```

### Phase 6: US4 (2 agents simultaneously after Phase 3)

```text
Agent A: T014 -- src/.../Views/FilingsView.axaml  (remove toolbar buttons)
Agent B: T015 -- src/.../Views/ReportsView.axaml  (remove toolbar buttons)
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Baseline verification (T001)
2. Complete Phase 2: ViewModel property (T002-T003)
3. Complete Phase 3: Header checkbox + US1 tests (T004-T007)
4. **STOP and VALIDATE**: header checkbox selects all rows on both pages; 8 new tests pass alongside all 29 originals
5. Demo / merge MVP if ready

### Incremental Delivery

1. Phase 1 + 2 -> Foundation ready
2. Phase 3 (US1) -> Select All working -> Demo 1 (MVP)
3. Phase 4 (US2) -> Deselect All working -> Demo 2
4. Phase 5 (US3) -> Indeterminate state working -> Demo 3
5. Phase 6 (US4) -> Toolbar cleaned up -> Demo 4 (full feature complete)
6. Phase 7 -> Edge cases covered, build green, manual sign-off

### Parallel Team Strategy (4 developers after Phase 2)

```text
Dev A: Phase 3 -- header checkbox View binding (T006, T007)
Dev B: Phase 3 -- US1 ViewModel tests         (T004, T005)
Dev C: Phase 4 -- US2 deselect tests          (T008, T009)
Dev D: Phase 5 -- US3 indeterminate tests     (T010-T013)
```

Phase 6 (US4 toolbar cleanup) starts once Dev A merges T006/T007.

---

## Notes

- All 6 modified files are pre-existing; this feature creates no new files.
- `SelectAllCommand` and `ClearSelectionCommand` are retained on both ViewModels per FR-014; the `IsAllSelected` setter delegates to them rather than duplicating selection logic.
- The `RelativeSource AncestorType=DataGrid` header checkbox binding (research.md R-001, R-006) is the same proven pattern already in use for command bindings in `ReportsView.axaml` (~line 116). No new XAML pattern is introduced.
- Resource strings `BulkDelete_SelectAll_Button` and `BulkDelete_ClearSelection_Button` remain in `Strings.resx` (marked unused) in case they are needed for tooltip or accessibility text later (data-model.md decision).
- The `_isUpdatingSelection` guard (research.md R-004) prevents feedback loops between the setter invoking `SelectAllCommand` and the reactive pipeline re-notifying `IsAllSelected`.
- [P] tasks can be dispatched simultaneously to parallel agents; non-[P] tasks within a phase must complete in listed order.
