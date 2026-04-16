# Data Model: Filings — Action Column Consolidation & Icon-Only Buttons

**Feature**: 030-filings-action-column-consolidation  
**Date**: 2025-07-17

## Impact Summary

This feature is a **UI-only change** in `Rentier.Desktop`. No Domain entities, Application commands/queries, or Infrastructure repositories are modified. The data model changes are confined to the presentation layer (ViewModels and View).

---

## Existing Entities (Unchanged)

### Filing (Domain Aggregate Root)
No changes. Status state machine remains:
```
Init (0) → Filed (1) → Paid (2)
```

### FilingStatus (Domain Enum)
No changes. Values: `Init = 0`, `Filed = 1`, `Paid = 2`.

### FilingRowDto (Application DTO)
No changes. Fields: `Id`, `Status`, `IncomeType`, `PayingEntity`, `FilingDeadline`, `TaxPayable`, `PaymentReference`.

---

## Modified Entities

### FilingRowViewModel (Desktop ViewModel — Modified)

**Current state**: Immutable row ViewModel created via `From(FilingRowDto dto)` factory method. Exposes computed display properties and `AvailableNextStatuses`.

**Changes**:

| Property | Change | Type | Description |
|---|---|---|---|
| `AdvanceStatusCommand` | **Added** | `ReactiveCommand<Unit, Unit>` | Per-row command that invokes the parent ViewModel's `AdvanceStatusCommand` with `(Id, FirstNextStatus)`. Disabled when `AvailableNextStatuses` is empty. |
| `ExportCommand` | **Added** | `ReactiveCommand<Unit, Unit>` | Per-row command that invokes the parent ViewModel's `ExportCommand` with `Id`. Always enabled. |
| `DeleteCommand` | **Added** | `ReactiveCommand<Unit, Unit>` | Per-row command that invokes the parent ViewModel's `DeleteCommand` with `Id`. Always enabled. |
| `AdvanceStatusTooltip` | **Added** | `string` | Computed tooltip: `"Mark as {NextStatusDisplayName}"` when a next status exists, or `"No further transitions"` when terminal. |
| `HasNextStatus` | **Added** | `bool` | Computed: `AvailableNextStatuses.Count > 0`. Used as `canExecute` for `AdvanceStatusCommand`. |

**Factory method change**:
```
Before: static FilingRowViewModel From(FilingRowDto dto)
After:  static FilingRowViewModel From(FilingRowDto dto, Action<(Guid, FilingStatus)> advanceStatus, Action<Guid> export, Action<Guid> delete)
```

The parent `FilingsViewModel` passes action delegates when constructing row VMs in `LoadPageAsync`.

### FilingsViewModel (Desktop ViewModel — Modified)

**Changes**:

| Area | Change | Description |
|---|---|---|
| `LoadPageAsync` | **Modified** | Passes action delegates for advance-status, export, and delete to `FilingRowViewModel.From()` |
| `AdvanceStatusCommand` | **Unchanged** | Remains `ReactiveCommand<(Guid, FilingStatus), Unit>` — row commands delegate to it |
| `ExportCommand` | **Unchanged** | Remains `ReactiveCommand<Guid, Unit>` — row commands delegate to it |
| `DeleteCommand` | **Unchanged** | Remains `ReactiveCommand<Guid, Unit>` — row commands delegate to it |

---

## New String Resources (Strings.resx)

| Key | Value | Usage |
|---|---|---|
| `Filings_Tooltip_AdvanceStatus` | `Mark as {0}` | Advance-status button tooltip (format string with next status name) |
| `Filings_Tooltip_AdvanceStatus_None` | `No further transitions` | Advance-status button tooltip when disabled |
| `Filings_Tooltip_Export` | `Export PP-OPO XML` | Export button tooltip |
| `Filings_Tooltip_Delete` | `Delete filing` | Delete button tooltip |
| `Filings_Col_Actions` | `Actions` | Actions column header |

---

## Removed Elements

| Element | Location | Reason |
|---|---|---|
| Status ComboBox column | `FilingsView.axaml` lines 114–130 | Replaced by advance-status icon button in Actions column (FR-001) |
| Export button column | `FilingsView.axaml` lines 161–171 | Merged into consolidated Actions column (FR-002) |
| Delete button column | `FilingsView.axaml` lines 173–183 | Merged into consolidated Actions column (FR-003) |
| `StatusComboBox_SelectionChanged` | `FilingsView.axaml.cs` lines 18–27 | Replaced by command binding (FR-016) |
| `ExportButton_Click` | `FilingsView.axaml.cs` lines 48–54 | Replaced by command binding |
| `DeleteButton_Click` | `FilingsView.axaml.cs` lines 40–46 | Replaced by command binding |

---

## Validation Rules

No new validation rules. All existing domain validation (status transitions, payment reference length) remains unchanged.

---

## State Transitions

No changes to the domain state machine. The UI interaction changes from:
- **Before**: Select new status from ComboBox → code-behind fires command
- **After**: Click icon button → command binding fires command with deterministic next status

The deterministic next status is always `AvailableNextStatuses[0]` (the only element — each status has at most one valid successor).

---

## Relationships

```
FilingsViewModel (1) ──creates──▶ FilingRowViewModel (many)
                                    │
                                    ├── AdvanceStatusCommand ──delegates──▶ FilingsViewModel.AdvanceStatusCommand
                                    ├── ExportCommand ──delegates──▶ FilingsViewModel.ExportCommand
                                    └── DeleteCommand ──delegates──▶ FilingsViewModel.DeleteCommand
```
