# UI Contracts: Filings — Action Column Consolidation & Icon-Only Buttons

**Feature**: 030-filings-action-column-consolidation  
**Date**: 2025-07-17

## Contract Type: Avalonia MVVM ViewModel ↔ View Binding Contract

This feature is a Desktop-layer UI change. The "contracts" are the ViewModel property surface that the AXAML view binds to. These contracts define the public API between `FilingRowViewModel` and `FilingsView.axaml`.

---

## FilingRowViewModel — New Binding Surface

### Commands (AXAML binds to these via `Command="{Binding ...}"`)

| Property | Type | CanExecute | Description |
|---|---|---|---|
| `AdvanceStatusCommand` | `ReactiveCommand<Unit, Unit>` | `HasNextStatus` (true for Init, Filed; false for Paid) | Advances the filing to its next status. Delegates to parent ViewModel's `AdvanceStatusCommand`. |
| `ExportCommand` | `ReactiveCommand<Unit, Unit>` | Always `true` | Triggers PP-OPO XML export. Delegates to parent ViewModel's `ExportCommand`. |
| `DeleteCommand` | `ReactiveCommand<Unit, Unit>` | Always `true` | Triggers delete with confirmation. Delegates to parent ViewModel's `DeleteCommand`. |

### Display Properties (AXAML binds to these via `Text="{Binding ...}"` or `ToolTip.Tip="{Binding ...}"`)

| Property | Type | Example Values | Description |
|---|---|---|---|
| `AdvanceStatusTooltip` | `string` | `"Mark as Filed"`, `"Mark as Paid"`, `"No further transitions"` | Tooltip text for the advance-status button. Dynamic based on filing status. |
| `HasNextStatus` | `bool` | `true` (Init, Filed), `false` (Paid) | Whether a valid next status exists. Controls advance-status button enabled state. |

### Existing Properties (Unchanged, Still Bound)

| Property | Type | Bound To |
|---|---|---|
| `Id` | `Guid` | Row identity for command parameters |
| `Status` | `FilingStatus` | Status badge brush converter |
| `StatusDisplayText` | `string` | Status badge text |
| `IsSelected` | `bool` | Selection checkbox |
| `IncomeType` | `IncomeType` | Income type column |
| `PayingEntity` | `string` | Paying entity column |
| `DeadlineDisplay` | `string` | Filing deadline column |
| `TaxPayableDisplay` | `string` | Tax payable column |
| `PaymentReference` | `string?` | Payment reference TextBox |
| `IsPaymentReferenceEditable` | `bool` | Payment reference read-only state |
| `AvailableNextStatuses` | `IReadOnlyList<FilingStatus>` | No longer bound to ComboBox (removed); used internally by `HasNextStatus` |

---

## Static Tooltip Strings (from Strings.resx)

| Resource Key | Value | Bound To |
|---|---|---|
| `Filings_Tooltip_Export` | `Export PP-OPO XML` | Export button `ToolTip.Tip` |
| `Filings_Tooltip_Delete` | `Delete filing` | Delete button `ToolTip.Tip` |

---

## DataGrid Column Contract

### Before (10 columns)
```
[✓] | [Status Badge] | [Status ComboBox] | Income Type | Paying Entity | Deadline | Tax Payable | Payment Ref | [Export] | [Delete]
```

### After (8 columns)
```
[✓] | [Status Badge] | Income Type | Paying Entity | Deadline | Tax Payable | Payment Ref | [Actions: ▶ ⤓ 🗑]
```

**Removed columns**: Status ComboBox, Export, Delete (3 columns removed)  
**Added columns**: Actions (1 column added)  
**Net change**: −2 columns

---

## Actions Column Layout

```
┌──────────────────────────────────────┐
│  [▶ Advance]  [⤓ Export]  [🗑 Delete] │
│   PathIcon     PathIcon    PathIcon   │
│   Tooltip      Tooltip     Tooltip    │
│   canExec      always      always     │
└──────────────────────────────────────┘
```

- Horizontal `StackPanel` with `Spacing="4"`
- Each button: icon-only (no `Content` text), `ToolTip.Tip` for discoverability
- Delete button: `Foreground="Red"` for destructive visual distinction
- Advance-status button: visually disabled (greyed out) when `HasNextStatus == false`
