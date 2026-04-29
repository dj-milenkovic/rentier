# Implementation Plan: Filings Visual Sorting

**Branch**: `046-filings-visual-sorting` | **Date**: 2025-07-15 | **Spec**: `.specify/specs/046-filings-visual-sorting/spec.md`
**Input**: Feature specification from `.specify/specs/046-filings-visual-sorting/spec.md`

## Summary

Add visual sort arrows (↑/↓) in DataGrid column headers on the Filings page, implementing a three-state sort cycle (unsorted → ascending → descending → unsorted). Remove the redundant Unpaid/All radio button filter toggles and the text-based sort indicator from the toolbar. This is a Desktop-layer-only change — the ViewModel already has `SortColumn`/`SortDescending` properties and `ApplySortCommand`; this feature adds the visual representation and modifies the sort cycle.

## Technical Context

**Language/Version**: C# 12 / .NET 8
**Primary Dependencies**: Avalonia UI 11+, ReactiveUI, FluentTheme
**Storage**: N/A (no persistence changes)
**Testing**: xUnit + FluentAssertions + NSubstitute + Avalonia.Headless.XUnit
**Target Platform**: Windows + macOS desktop (cross-platform Avalonia)
**Project Type**: Desktop application (Clean Architecture)
**Performance Goals**: Sort interactions must feel instant (<50ms) — all client-side in-memory operations
**Constraints**: Desktop layer only; no changes to Domain, Application, or Infrastructure layers
**Scale/Scope**: ~6 files modified, 1 new file (converter), estimated 2–3 hours implementation

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- [x] Clean Architecture boundary is preserved (`Desktop -> Application -> Domain`; Infrastructure implements Application contracts only). — **UI-only change. No new dependencies cross layer boundaries.**
- [x] All monetary/rate/percentage values are modeled as `decimal`. — **No monetary value changes. Sort operates on existing fields.**
- [x] All business dates are modeled as `DateOnly`; boundary conversions are identified. — **No date changes.**
- [x] Security/privacy constraints hold: local-first data, OS credential store for secrets, no telemetry. — **No new data access or storage.**
- [x] External network usage is limited to approved endpoints (IMAP and NBS) or explicitly justified. — **No network calls. Sorting is client-side.**
- [x] All I/O paths are async; UI work avoids blocking calls and uses reactive async command flow. — **Sort command already uses `ReactiveCommand.CreateFromTask`. No new I/O.**
- [x] Tests and coverage impact are defined for Domain (rule/state coverage) and Application (>=90%). — **Domain/Application unchanged. Desktop ViewModel tests updated for 3-state cycle. Headless UI tests added for arrow rendering.**
- [x] Feature work is mapped to an approved spec task under `.specify/tasks/`. — **Spec at `.specify/specs/046-filings-visual-sorting/spec.md`.**

## Project Structure

### Documentation (this feature)

```text
.specify/specs/046-filings-visual-sorting/
├── spec.md              # Feature specification
├── plan.md              # This file
├── research.md          # Phase 0: research decisions
├── data-model.md        # Phase 1: ViewModel state changes
├── quickstart.md        # Phase 1: quick reference
├── contracts/
│   └── ui-contracts.md  # Phase 1: visual state machine and toolbar layout
└── tasks.md             # Phase 2 output (created by /speckit.tasks)
```

### Source Code (repository root)

```text
src/Rentier.Desktop/
├── Views/
│   ├── FilingsView.axaml          # MODIFIED: remove radio buttons, add sort arrow headers
│   └── FilingsView.axaml.cs       # MODIFIED: update DataGrid_Sorting for 3-state cycle
├── ViewModels/
│   └── FilingsViewModel.cs        # MODIFIED: nullable SortColumn, ShowAll default, remove SortIndicatorDisplay
├── Converters/
│   └── SortArrowConverter.cs      # NEW: IMultiValueConverter for sort arrow geometry
└── Assets/
    └── Icons.axaml                # MODIFIED: add SortAscIcon/SortDescIcon StreamGeometry resources

tests/Rentier.UnitTests/Desktop/
├── FilingsViewModelTests.cs              # MODIFIED: 3-state sort cycle tests, default filter
└── Views/FilingsViewHeadlessTests.cs     # MODIFIED: verify arrows render, radio buttons absent
```

**Structure Decision**: All changes are within the existing `Rentier.Desktop` project and its test project. No new projects or layers needed.

## Complexity Tracking

No constitution violations. No complexity justifications needed.

---

## Design Decisions

### D-001: Three-State Sort Cycle via Nullable SortColumn

The `SortColumn` property changes from `FilingSortColumn` to `FilingSortColumn?`. When null, no column is actively sorted and the query uses default database ordering. The `ApplySortCommand` cycle becomes:

```
Same column click:
  null (unsorted)     → set column ascending
  ascending           → set column descending  
  descending          → set null (unsorted)

Different column click:
  any state           → set new column ascending
```

This requires updating `ApplySortCommand` logic and the `LoadPageAsync` method to handle `null` sort column (pass default to query or adjust query construction).

### D-002: Custom Header Templates for Sortable Columns

Sortable `DataGridTextColumn` entries are replaced with equivalent `DataGridTemplateColumn` entries that use a custom `HeaderTemplate` containing:
- The localized column label text
- A `PathIcon` bound (via `MultiBinding` + `SortArrowConverter`) to the ViewModel's `SortColumn` and `SortDescending`

The `SortArrowConverter` receives `(SortColumn, SortDescending, columnTag)` and returns:
- `SortAscIcon` geometry when the column matches and ascending
- `SortDescIcon` geometry when the column matches and descending  
- `null` (hidden) when column doesn't match or sort is unsorted

The `DataGridTextColumn.Binding` for cell content is moved to `DataGridTemplateColumn.CellTemplate`.

### D-003: Code-Behind Sort Handler Update

The `DataGrid_Sorting` handler in `FilingsView.axaml.cs` already calls `ApplySortCommand`. The three-state logic is in the ViewModel command, not the code-behind. No significant code-behind change needed beyond ensuring the command parameter passes the column tag.

### D-004: Toolbar Simplification

Remove from `FilingsView.axaml`:
1. Both `RadioButton` elements (Unpaid/All filter toggles) — lines 20-25
2. The `SortIndicatorDisplay` `TextBlock` — line 45

Change in `FilingsViewModel.cs`:
1. Default `_showAll = true` (was `false`) — ensures FR-008 (show all filings by default)
2. Remove `SortIndicatorDisplay` computed property — FR-009

The `ShowAll` property setter and its reactive subscription remain for now (harmless; cleanup deferred to feature 045).

### D-005: Sort Arrow StreamGeometry Resources

Add to `Icons.axaml` (or `FilingsView.axaml` resources):

```xml
<StreamGeometry x:Key="SortAscIcon">M7 14 L12 9 L17 14</StreamGeometry>
<StreamGeometry x:Key="SortDescIcon">M7 10 L12 15 L17 10</StreamGeometry>
```

These are simple chevron-style arrows consistent with the existing Lucide-derived icons.

---

## Test Impact

### ViewModel Tests (FilingsViewModelTests.cs)

| Test | Change |
|---|---|
| `OnActivation_TriggersLoadPageWithDefaultUnpaidFilter` | Update: expect `FilingFilterMode.All` (was `Unpaid`) |
| `InitialSortState_IsFilingDeadlineDescending` | Update: `SortColumn` is now nullable `FilingSortColumn?` |
| `ApplySortCommand_SameColumn_TogglesDirectionKeepsPage` | Update: becomes 3-state cycle test |
| **NEW** `ApplySortCommand_SameColumn_ThirdClick_ClearsSortToUnsorted` | Verify descending → null |
| **NEW** `ApplySortCommand_UnsortedColumn_FirstClick_SetsAscending` | Verify null → ascending |
| **NEW** `SortIndicatorDisplay_IsRemoved` | Verify property no longer exists (compile-time check) |
| `FilterToggle_ShowAllChange_ResetsPageToOneAndReloads` | May be updated or removed depending on ShowAll property retention |

### Headless UI Tests (FilingsViewHeadlessTests.cs)

| Test | Description |
|---|---|
| **NEW** `FilingsView_WhenRendered_RadioButtonsNotPresent` | Verify no RadioButton controls in visual tree |
| **NEW** `FilingsView_SortableColumnHeaders_ContainArrowPathIcon` | Verify sortable columns have PathIcon in header |
| **NEW** `FilingsView_NonSortableColumns_DoNotHaveArrowIcons` | Verify checkbox/status/actions headers have no PathIcon |

---

## Post-Design Constitution Re-Check

- [x] **Clean Architecture**: Confirmed — all changes in Desktop layer. `FilingSortColumn` enum unchanged. Nullable wrapper is ViewModel-only.
- [x] **Financial Correctness**: Confirmed — no `decimal`/`double` changes. Sort is comparison-only.
- [x] **Temporal Correctness**: Confirmed — no `DateOnly`/`DateTime` changes.
- [x] **Security/Privacy**: Confirmed — no data access changes.
- [x] **Network Scope**: Confirmed — no network calls.
- [x] **Async/UI**: Confirmed — `ApplySortCommand` remains `ReactiveCommand.CreateFromTask`. No blocking calls.
- [x] **Testing**: Confirmed — ViewModel tests updated for 3-state cycle and new default. Headless tests added for visual verification.
- [x] **Spec Traceability**: Confirmed — mapped to spec `.specify/specs/046-filings-visual-sorting/spec.md`.
