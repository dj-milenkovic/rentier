# Implementation Plan: Header Checkbox for Select All / Clear All

**Branch**: `feat/027-031-ux-improvements` | **Date**: 2025-07-17 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `.specify/specs/028-header-checkbox-select-all/spec.md`

## Summary

Replace the standalone "Select All" and "Clear Selection" toolbar buttons on the Filings and Reports pages with a single tri-state checkbox placed in the DataGrid header cell of the selection column. The checkbox reflects the current selection state (unchecked / indeterminate / checked) and allows one-click select-all or deselect-all. This is a **Desktop-layer only** change — no Domain, Application, or Infrastructure modifications are required.

## Technical Context

**Language/Version**: C# 12 / .NET 8
**Primary Dependencies**: Avalonia UI 11+ (FluentTheme), ReactiveUI, CommunityToolkit.Mvvm
**Storage**: N/A (selection state is transient, in-memory only)
**Testing**: xUnit + FluentAssertions + NSubstitute
**Target Platform**: Windows + macOS (cross-platform desktop via Avalonia)
**Project Type**: Desktop application (Avalonia MVVM)
**Performance Goals**: Header checkbox state reflects within 200ms of any row selection change (SC-003)
**Constraints**: No new NuGet packages; no database migrations; UI thread safety required
**Scale/Scope**: 2 pages affected (Filings, Reports); 6 files modified

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- [x] Clean Architecture boundary is preserved (`Desktop -> Application -> Domain`; Infrastructure implements Application contracts only).
  - ✅ All changes are in Desktop layer only (ViewModels and Views). No Application, Domain, or Infrastructure changes.
- [x] All monetary/rate/percentage values are modeled as `decimal`.
  - ✅ N/A — no monetary or rate values introduced or modified.
- [x] All business dates are modeled as `DateOnly`; boundary conversions are identified.
  - ✅ N/A — no date fields introduced or modified.
- [x] Security/privacy constraints hold: local-first data, OS credential store for secrets, no telemetry.
  - ✅ N/A — selection state is transient in-memory. No data stored, no network, no telemetry.
- [x] External network usage is limited to approved endpoints (IMAP and NBS) or explicitly justified as a constitution amendment.
  - ✅ N/A — no network calls introduced. Purely local UI change.
- [x] All I/O paths are async; UI work avoids blocking calls and uses reactive async command flow.
  - ✅ No I/O introduced. `SelectAllCommand` and `ClearSelectionCommand` are synchronous `ReactiveCommand.Create()` operating on in-memory properties. Header checkbox setter uses `Execute().Subscribe()` which is non-blocking.
- [x] Tests and coverage impact are defined for Domain (rule/state coverage) and Application (>=90%).
  - ✅ No Domain or Application changes → no coverage impact. Desktop ViewModel tests will be added for `IsAllSelected` tri-state logic.
- [x] Feature work is mapped to an approved spec task under `.specify/tasks/`.
  - ✅ Spec exists at `.specify/specs/028-header-checkbox-select-all/spec.md` with approved checklist.

## Project Structure

### Documentation (this feature)

```text
.specify/specs/028-header-checkbox-select-all/
├── plan.md              # This file
├── research.md          # Phase 0 output — research decisions
├── data-model.md        # Phase 1 output — ViewModel property changes & state machine
├── quickstart.md        # Phase 1 output — implementation guide
├── checklists/
│   └── requirements.md  # Spec quality checklist (pre-existing)
└── tasks.md             # Phase 2 output (created by /speckit.tasks — NOT by /speckit.plan)
```

### Source Code (repository root)

```text
src/
└── Rentier.Desktop/
    ├── ViewModels/
    │   ├── FilingsViewModel.cs        # MODIFY: add IsAllSelected property
    │   └── ReportsViewModel.cs        # MODIFY: add IsAllSelected property
    └── Views/
        ├── FilingsView.axaml          # MODIFY: header checkbox + remove toolbar buttons
        └── ReportsView.axaml          # MODIFY: header checkbox + remove toolbar buttons

tests/
└── Rentier.UnitTests/
    └── Desktop/
        ├── FilingsViewModelBulkDeleteTests.cs   # MODIFY: add IsAllSelected tests
        └── ReportsViewModelBulkDeleteTests.cs   # MODIFY: add IsAllSelected tests
```

**Structure Decision**: Existing Desktop/MVVM structure is used. No new files are created — all changes are modifications to existing ViewModels, Views, and test files. The feature is entirely contained within the presentation layer.

## Complexity Tracking

> No Constitution Check violations. No complexity justifications needed.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| (none)    | —          | —                                   |

## Design Decisions

### D-001: Tri-State via `bool?` Property

Add `IsAllSelected` as a `bool?` property to both `FilingsViewModel` and `ReportsViewModel`:
- **Getter**: Computed from `SelectedCount` and `Rows.Count` (see state machine in data-model.md)
- **Setter**: Dispatches `SelectAllCommand` (on `true`) or `ClearSelectionCommand` (on `false`); ignores `null`
- **Re-entrancy guard**: `_isUpdatingSelection` flag prevents feedback loops

Rationale: `bool?` is the native Avalonia tri-state model; no converter needed. Reuses existing commands per FR-014.

### D-002: Header Checkbox via DataGridTemplateColumn.Header

Place a `CheckBox` with `IsThreeState="True"` in the `DataGridTemplateColumn.Header` of the selection column. Bind via `RelativeSource={RelativeSource AncestorType=DataGrid}` to reach the ViewModel.

Rationale: This pattern is already used in `ReportsView.axaml` for action button commands (line 116). Proven approach in the codebase.

### D-003: Empty State Disabling

Bind `IsEnabled` on the header `CheckBox` to `HasItems` via the same `RelativeSource` pattern. When `Rows.Count == 0`, the checkbox is disabled and unchecked.

Rationale: Satisfies FR-015 using an existing reactive property. Disabled (not hidden) communicates the column purpose.

### D-004: Toolbar Cleanup

Remove the "Select All" and "Clear Selection" `<Button>` elements from both toolbars. The "Delete Selected (N)" button is kept as-is.

Rationale: FR-009 through FR-012 require removal. FR-013 requires the delete button to remain. Resource strings are kept in `Strings.resx` in case they're needed for tooltip/accessibility later.

### D-005: Reactive Chain Update

In `RebuildRowSubscriptions()`, the existing lambda that updates `SelectedCount` is extended to also raise `PropertyChanged` for `IsAllSelected`. This ensures the header checkbox updates automatically when individual rows are toggled.

Rationale: Minimal change to existing reactive infrastructure. The computed getter on `IsAllSelected` does the state logic; the notification just triggers the UI binding refresh.

## External Contracts

This feature has no external interfaces. It is a purely internal UI interaction change. No APIs, CLI commands, file formats, or inter-system contracts are affected.

## Test Plan

### New Tests (added to existing test files)

| Test | File | Validates |
|------|------|-----------|
| `IsAllSelected_WhenNoRows_ReturnsFalse` | Both test files | FR-015, empty state |
| `IsAllSelected_WhenNoRowsSelected_ReturnsFalse` | Both test files | FR-003 |
| `IsAllSelected_WhenAllRowsSelected_ReturnsTrue` | Both test files | FR-002 |
| `IsAllSelected_WhenSomeRowsSelected_ReturnsNull` | Both test files | FR-004 |
| `IsAllSelected_SetTrue_SelectsAllRows` | Both test files | FR-005 |
| `IsAllSelected_SetFalse_DeselectsAllRows` | Both test files | FR-007 |
| `IsAllSelected_SetTrue_FromIndeterminate_SelectsAllRows` | Both test files | FR-006 |
| `IsAllSelected_UpdatesWhenRowSelectionChanges` | Both test files | FR-008 |
| `IsAllSelected_RecalculatesAfterRowsReloaded` | Both test files | FR-016 |

### Existing Tests (must continue passing)

All 17 tests in `FilingsViewModelBulkDeleteTests.cs` and all 12 tests in `ReportsViewModelBulkDeleteTests.cs` must pass without modification. These cover:
- `SelectedCount` reactivity
- `HasSelection` computation
- `SelectAllCommand` / `ClearSelectionCommand` execution
- `BulkDeleteCommand` flow (confirmation, dispatch, error handling)

## Requirement Traceability

| Requirement | Design Decision | Test Coverage |
|-------------|----------------|---------------|
| FR-001 | D-002 (header template) | Visual verification |
| FR-002 | D-001 (getter: `SelectedCount == Rows.Count` → `true`) | `IsAllSelected_WhenAllRowsSelected_ReturnsTrue` |
| FR-003 | D-001 (getter: `SelectedCount == 0` → `false`) | `IsAllSelected_WhenNoRowsSelected_ReturnsFalse` |
| FR-004 | D-001 (getter: partial → `null`) | `IsAllSelected_WhenSomeRowsSelected_ReturnsNull` |
| FR-005 | D-001 (setter: `true` → SelectAllCommand) | `IsAllSelected_SetTrue_SelectsAllRows` |
| FR-006 | D-001 (setter: `true` from indeterminate) | `IsAllSelected_SetTrue_FromIndeterminate_SelectsAllRows` |
| FR-007 | D-001 (setter: `false` → ClearSelectionCommand) | `IsAllSelected_SetFalse_DeselectsAllRows` |
| FR-008 | D-005 (reactive chain) | `IsAllSelected_UpdatesWhenRowSelectionChanges` |
| FR-009 | D-004 (remove Select All button from Filings) | Visual verification + absence in AXAML |
| FR-010 | D-004 (remove Clear Selection button from Filings) | Visual verification + absence in AXAML |
| FR-011 | D-004 (remove Select All button from Reports) | Visual verification + absence in AXAML |
| FR-012 | D-004 (remove Clear Selection button from Reports) | Visual verification + absence in AXAML |
| FR-013 | D-004 (keep Delete Selected button) | Existing tests + visual verification |
| FR-014 | D-001 (commands retained, invoked from setter) | Existing command tests + new setter tests |
| FR-015 | D-003 (disabled when empty) | `IsAllSelected_WhenNoRows_ReturnsFalse` |
| FR-016 | D-005 + existing reload flow | `IsAllSelected_RecalculatesAfterRowsReloaded` |
