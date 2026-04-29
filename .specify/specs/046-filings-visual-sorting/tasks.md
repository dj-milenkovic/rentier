# Tasks: Filings Visual Sorting

**Input**: Design documents from `.specify/specs/046-filings-visual-sorting/`
**Branch**: `046-filings-visual-sorting`
**Prerequisites**: spec.md ✅ | plan.md ✅ | data-model.md ✅ | contracts/ui-contracts.md ✅ | research.md ✅ | quickstart.md ✅

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: Which user story this task belongs to (US1, US2, US3)
- All changes are **Desktop layer only** — no Domain, Application, or Infrastructure changes

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Add icon resources required by all sort arrow rendering tasks. Must complete before Phase 2 converter work.

- [X] T001 Add `SortAscIcon` (M7 14 L12 9 L17 14) and `SortDescIcon` (M7 10 L12 15 L17 10) chevron StreamGeometry resources to `src/Rentier.Desktop/Assets/Icons.axaml` following the existing Lucide icon pattern

**Checkpoint**: Icons are available as static resources — converter and header templates can now reference them.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core ViewModel and converter changes that both US1 and US2 depend on. Must complete before any user story phase begins.

**⚠️ CRITICAL**: No user story phase work can begin until this phase is complete.

- [X] T002 [P] Create `SortArrowConverter` implementing `IMultiValueConverter` in `src/Rentier.Desktop/Converters/SortArrowConverter.cs` — inputs: `(FilingSortColumn? SortColumn, bool SortDescending, string columnTag)`, output: `StreamGeometry?` (returns `SortAscIcon` when column matches and ascending, `SortDescIcon` when column matches and descending, `null` when not the active column or SortColumn is null)
- [X] T003 Update `FilingsViewModel` in `src/Rentier.Desktop/ViewModels/FilingsViewModel.cs`:
  - Change `_sortColumn` backing field type from `FilingSortColumn` to `FilingSortColumn?` (keep default value `FilingSortColumn.FilingDeadline`)
  - Change `SortColumn` property type to `FilingSortColumn?`
  - Update `ApplySortCommand` to implement 3-state cycle: same-column ascending → descending → null (unsorted); different-column → ascending
  - Change `_showAll` backing field default from `false` to `true` (FR-008: all filings shown by default)
  - Remove the `SortIndicatorDisplay` computed property entirely (FR-009)
  - Update `LoadPageAsync` to handle `null` SortColumn (pass default/no-sort to query)

**Checkpoint**: Converter and ViewModel are correct — user story phases can now proceed. Register converter in App.axaml (T006) before binding in view.

---

## Phase 3: User Story 1 — Sort Filings by Column with Visual Feedback (Priority: P1) 🎯 MVP

**Goal**: Sortable column headers (IncomeType, PayingEntity, FilingDeadline, TaxPayable) display ↑/↓ arrows reflecting ViewModel sort state; clicking cycles through unsorted → ascending → descending → unsorted.

**Independent Test**: Click column headers on the Filings page and verify arrows appear/change/disappear, and rows reorder. Run `dotnet test tests/Rentier.UnitTests --filter "Category=Desktop|Category=UI"`.

### Tests for User Story 1 ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation (T007)**

- [X] T004 [P] [US1] Update sort-cycle tests in `tests/Rentier.UnitTests/Desktop/FilingsViewModelTests.cs`:
  - Update `InitialSortState_IsFilingDeadlineDescending` — assert `SortColumn` is `FilingSortColumn?` (nullable)
  - Update `ApplySortCommand_SameColumn_TogglesDirectionKeepsPage` — becomes first leg of 3-state cycle (asc → desc)
  - Add `ApplySortCommand_SameColumn_ThirdClick_ClearsSortToUnsorted` — assert descending → null
  - Add `ApplySortCommand_UnsortedColumn_FirstClick_SetsAscending` — assert null → ascending
  - Add `ApplySortCommand_DifferentColumn_ResetsToAscendingOnNewColumn` — verify arrow moves between columns
- [X] T005 [P] [US1] Add headless UI tests in `tests/Rentier.UnitTests/Desktop/Views/FilingsViewHeadlessTests.cs`:
  - `FilingsView_SortableColumnHeaders_ContainArrowPathIcon` — verify PathIcon exists in header template for IncomeType, PayingEntity, FilingDeadline, TaxPayable columns

### Implementation for User Story 1

- [X] T006 [US1] Register `SortArrowConverter` in application-level resources in `src/Rentier.Desktop/App.axaml` (or in `FilingsView.axaml` local resources if converter is view-scoped)
- [X] T007 [US1] Replace the 4 sortable `DataGridTextColumn` definitions (IncomeType, PayingEntity, FilingDeadline, TaxPayable) with `DataGridTemplateColumn` entries in `src/Rentier.Desktop/Views/FilingsView.axaml`:
  - Each column gets a custom `HeaderTemplate` with a `StackPanel` containing: `TextBlock` (column label) + `PathIcon` bound via `MultiBinding` to `(SortColumn, SortDescending, columnTag)` through `SortArrowConverter`
  - Move the existing cell `Binding` to a `CellTemplate` `TextBlock`
  - Keep `CanUserSort="True"` on sortable columns; keep `CanUserSort="False"` on non-sortable columns
- [X] T008 [US1] Review and update `DataGrid_Sorting` handler in `src/Rentier.Desktop/Views/FilingsView.axaml.cs` — verify the column tag is correctly passed to `ApplySortCommand` so the 3-state cycle receives the right column identifier

**Checkpoint**: User Story 1 is complete. Column headers show ↑/↓ arrows and cycle through 3 states. Run T004 and T005 tests to confirm.

---

## Phase 4: User Story 2 — Remove Redundant Filter Toggles (Priority: P2)

**Goal**: Unpaid/All radio buttons and `SortIndicatorDisplay` text label are removed from the toolbar. Filings page loads showing all filings by default. Remaining toolbar elements (report filter chip, New Filing button, bulk selection) work correctly.

**Independent Test**: Load the Filings page and confirm no radio buttons visible in toolbar, all filings displayed by default, and existing toolbar controls still function.

### Tests for User Story 2 ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation (T011)**

- [X] T009 [P] [US2] Update ViewModel tests in `tests/Rentier.UnitTests/Desktop/FilingsViewModelTests.cs`:
  - Update `OnActivation_TriggersLoadPageWithDefaultUnpaidFilter` — assert `FilingFilterMode.All` (was `Unpaid`)
  - Verify `SortIndicatorDisplay` property no longer exists (compile-time check via removing any reference)
- [X] T010 [P] [US2] Add headless UI test in `tests/Rentier.UnitTests/Desktop/Views/FilingsViewHeadlessTests.cs`:
  - `FilingsView_WhenRendered_RadioButtonsNotPresent` — assert no `RadioButton` controls exist in the visual tree of the toolbar area

### Implementation for User Story 2

- [X] T011 [US2] Remove Unpaid/All filter toggle controls and sort indicator text from `src/Rentier.Desktop/Views/FilingsView.axaml`:
  - Remove both `RadioButton` elements (Unpaid / All filter toggles) from the toolbar
  - Remove the `SortIndicatorDisplay` `TextBlock` binding from the toolbar
  - Confirm the report filter chip (`HasReportFilter`-gated control) and New Filing button remain and are correctly laid out

**Checkpoint**: User Story 2 is complete. Toolbar is clean — only report filter chip and New Filing button remain. Run T009 and T010 tests to confirm.

---

## Phase 5: User Story 3 — Distinguish Sortable from Non-Sortable Columns (Priority: P3)

**Goal**: Sortable column headers show `Cursor="Hand"` on hover, providing a visual affordance that differentiates them from non-sortable headers (checkbox, status badge, payment reference, actions).

**Independent Test**: Hover over sortable and non-sortable column headers and verify cursor changes only on sortable columns. Headless test confirms no PathIcon in non-sortable headers.

### Tests for User Story 3 ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation (T013)**

- [X] T012 [P] [US3] Add headless UI test in `tests/Rentier.UnitTests/Desktop/Views/FilingsViewHeadlessTests.cs`:
  - `FilingsView_NonSortableColumns_DoNotHaveArrowIcons` — assert checkbox, status badge, payment reference, and actions columns contain no `PathIcon` elements in their header templates

### Implementation for User Story 3

- [X] T013 [US3] Add `Cursor="Hand"` to the header `StackPanel` (or `Border`) of each sortable `DataGridTemplateColumn` in `src/Rentier.Desktop/Views/FilingsView.axaml` — apply to IncomeType, PayingEntity, FilingDeadline, and TaxPayable column headers only

**Checkpoint**: All three user stories are complete and independently testable.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Verification pass across all stories; catch regressions before merge.

- [X] T014 [P] Run the full Desktop/UI test suite and fix any regressions: `dotnet test tests/Rentier.UnitTests --filter "Category=Desktop|Category=UI"` in repo root
- [X] T015 [P] Build the Desktop project and verify no compiler errors: `dotnet build src/Rentier.Desktop` in repo root
- [X] T016 Validate acceptance scenarios manually per `quickstart.md`: launch app → Filings page → click sortable column headers → verify 3-state cycle (↑ → ↓ → none → ↑) → verify only one column shows arrow at a time → confirm no radio buttons in toolbar → confirm no sort text in toolbar

---

## Dependencies & Execution Order

### Phase Dependencies

```
Phase 1: Setup         — No dependencies (start immediately)
Phase 2: Foundational  — Depends on Phase 1 (T001 must complete before T002)
Phase 3: US1           — Depends on Phase 2 (T002 + T003 must complete)
Phase 4: US2           — Depends on Phase 2 (T003 must complete); independent of Phase 3
Phase 5: US3           — Depends on Phase 3 (T007 must complete — adds hover to existing templates)
Phase 6: Polish        — Depends on all story phases completing
```

### User Story Dependencies

- **US1 (P1)**: Depends on Foundational (T002 SortArrowConverter + T003 ViewModel) — standalone after that
- **US2 (P2)**: Depends on Foundational (T003 ViewModel _showAll default) — can run in parallel with US1
- **US3 (P3)**: Depends on US1 (T007 DataGridTemplateColumn headers) — extends the header templates created in US1

### Within Each User Story

- Tests (T004/T005, T009/T010, T012) MUST be written and **failing** before implementation tasks (T007, T011, T013)
- T006 (converter registration) must precede T007 (header template binding)
- T007 must precede T013 (hover cursor goes into the same header templates)

### Parallel Opportunities

| Parallel Group | Tasks | Why parallel |
|---|---|---|
| Foundational | T002, T003 | Different files (Converter vs ViewModel) |
| US1 tests | T004, T005 | Different test files |
| US2 tests | T009, T010 | Different test files |
| US3 test | T012 | Different file from T013 |
| Polish | T014, T015 | Independent commands |

---

## Parallel Example: User Story 1

```bash
# Write tests in parallel (different files):
Task: "Update 3-state cycle tests in tests/Rentier.UnitTests/Desktop/FilingsViewModelTests.cs"           # T004
Task: "Add headless arrow-render tests in tests/Rentier.UnitTests/Desktop/Views/FilingsViewHeadlessTests.cs"  # T005

# Then implement (sequential — same file):
Task: "Register SortArrowConverter in src/Rentier.Desktop/App.axaml"                                    # T006
Task: "Replace 4 sortable columns with DataGridTemplateColumn in src/Rentier.Desktop/Views/FilingsView.axaml" # T007
Task: "Update DataGrid_Sorting handler in src/Rentier.Desktop/Views/FilingsView.axaml.cs"               # T008
```

## Parallel Example: User Story 2

```bash
# Write tests in parallel (different files):
Task: "Update ViewModel filter-mode tests in tests/Rentier.UnitTests/Desktop/FilingsViewModelTests.cs"     # T009
Task: "Add headless radio-button-absent test in tests/Rentier.UnitTests/Desktop/Views/FilingsViewHeadlessTests.cs" # T010

# Then implement (single file):
Task: "Remove radio buttons and SortIndicatorDisplay from src/Rentier.Desktop/Views/FilingsView.axaml"    # T011
```

---

## Implementation Strategy

### MVP First (User Story 1 Only — ~1.5 hrs)

1. Complete Phase 1: Add icon resources (T001)
2. Complete Phase 2: Foundational — Converter + ViewModel (T002, T003)
3. Complete Phase 3: US1 tests then implementation (T004–T008)
4. **STOP and VALIDATE**: Sort arrows cycle correctly, tests pass
5. Demo: sort column headers show ↑/↓ indicators with correct 3-state cycle

### Incremental Delivery

1. T001 → T002 + T003 → T004–T008 → **MVP: sort arrows working** ✅
2. T009–T011 → **Toolbar cleaned up** ✅
3. T012–T013 → **Hover affordance added** ✅
4. T014–T016 → **Ready for merge** ✅

---

## Notes

- [P] tasks = different files, no incomplete task dependencies — safe to parallelize
- [Story] label maps each task to its user story for traceability and independent delivery
- **US2 and US1 can start in parallel** after Foundational (Phase 2) since they modify different sections of FilingsView.axaml (toolbar vs column definitions)
- **US3 depends on US1** because it adds hover style to the `DataGridTemplateColumn` headers created in T007
- `ShowAll` property body is kept in FilingsViewModel (used in `LoadPageAsync`) — only the RadioButton UI controls are removed
- `FilingSortColumn` enum in the Application layer is unchanged — nullable wrapper is ViewModel (Desktop) only
- No Domain, Application, or Infrastructure files are touched in any task
