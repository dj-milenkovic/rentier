# Quickstart: Filings — Action Column Consolidation & Icon-Only Buttons

**Feature**: 030-filings-action-column-consolidation  
**Branch**: `feat/027-031-ux-improvements`

## What This Feature Does

Simplifies the Filings DataGrid by collapsing three separate action columns (Change Status ComboBox, Export button, Delete button) into a single "Actions" column containing three icon-only buttons. Eliminates code-behind event handlers in favour of direct command bindings.

## Scope

- **Layer**: `Rentier.Desktop` only (presentation layer)
- **No changes**: Domain, Application, Infrastructure layers are untouched
- **Files affected**: ~4 files modified, ~5 new string resources

## Key Files

| File | Change |
|---|---|
| `src/Rentier.Desktop/Views/FilingsView.axaml` | Remove 3 columns, add 1 Actions column with icon buttons |
| `src/Rentier.Desktop/Views/FilingsView.axaml.cs` | Remove `StatusComboBox_SelectionChanged`, `ExportButton_Click`, `DeleteButton_Click` event handlers |
| `src/Rentier.Desktop/ViewModels/FilingRowViewModel.cs` | Add per-row commands (`AdvanceStatusCommand`, `ExportCommand`, `DeleteCommand`), tooltip property, `HasNextStatus` |
| `src/Rentier.Desktop/ViewModels/FilingsViewModel.cs` | Pass command delegates to row VM factory |
| `src/Rentier.Desktop/Resources/Strings.resx` | Add 5 new tooltip/header string resources |
| `tests/Rentier.UnitTests/Desktop/FilingsViewModelTests.cs` | Update tests for new row VM factory signature |

## Architecture Decisions

1. **Icon approach**: Built-in `PathIcon` with inline SVG geometry data — no new NuGet packages
2. **Command binding**: Per-row commands on `FilingRowViewModel` that delegate to parent `FilingsViewModel` commands — eliminates all action-button code-behind
3. **Tooltip strategy**: `ToolTip.Tip` attached property — dynamic binding for advance-status, static resources for export/delete
4. **Destructive style**: `Foreground="Red"` on delete button — matches existing bulk-delete pattern

## Quick Verification

After implementation, verify:
1. Open Filings page → Actions column is rightmost, contains 3 icon buttons per row
2. No separate "Change Status", "Export", or "Delete" columns exist
3. Hover each icon → correct tooltip appears
4. Click advance-status on Init filing → status changes to Filed
5. Advance-status button is greyed out on Paid filing
6. Export and Delete buttons work identically to before
7. `FilingsView.axaml.cs` contains only `PaymentRef_LostFocus` handler (status/export/delete handlers removed)
