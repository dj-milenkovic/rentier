# UI Contracts: 050 — Filings Filter Header Flyouts

## Flyout Interaction Contract

### Column Header Layout

Each filterable column header renders three elements in a horizontal stack:

```
┌───────────────────────────────────────┐
│  Column Label   ↑  🔽                │
│                sort filter            │
└───────────────────────────────────────┘
```

- **Column Label**: `TextBlock` with localized text
- **Sort Arrow**: `PathIcon` (existing, unchanged)
- **Filter Icon**: `Button` with `PathIcon` (funnel) — clickable, toggles `Popup`

### Enum Filter Flyout (Status, Income Type)

```
┌──────────────────────┐
│ ☑ Init               │
│ ☑ Filed              │
│ ☑ Paid               │
│──────────────────────│
│ [Izaberi sve] [Obriši]│
│──────────────────────│
│         [Primeni]     │
└──────────────────────┘
```

- Checkboxes: one per enum value, all checked by default when no filter active
- Select All: checks all checkboxes (does not apply)
- Clear: unchecks all checkboxes (does not apply)
- Apply: commits selection, closes popup

### Text Filter Flyout (Paying Entity, Deadline, Payment Reference)

```
┌──────────────────────┐
│ ┌──────────────────┐ │
│ │ Pretraži...      │ │
│ └──────────────────┘ │
│──────────────────────│
│         [Primeni]     │
└──────────────────────┘
```

- TextBox: placeholder "Pretraži...", shows current filter value on open
- Apply: commits text, closes popup

### Filter Icon States

| State    | Foreground                         |
|----------|------------------------------------|
| Inactive | `RentierTextSecondaryBrush` (muted)|
| Active   | `RentierAccentBrush` (accent)      |
| Disabled | `RentierTextDisabledBrush` (dim)   |

"Disabled" state occurs when `ReportIdFilter` is set.

## ViewModel Binding Contract

### FilingsViewModel → View Bindings

| ViewModel Property              | AXAML Binding Target                       |
|---------------------------------|--------------------------------------------|
| `StatusFlyout.IsOpen`           | Status column `Popup.IsOpen`               |
| `StatusFlyout.Items`            | Status flyout `ItemsControl.ItemsSource`   |
| `StatusFlyout.IsActive`         | Status funnel `PathIcon.Foreground` (via converter) |
| `StatusFlyout.ApplyCommand`     | Status flyout Apply `Button.Command`       |
| `StatusFlyout.SelectAllCommand` | Status flyout Select All `Button.Command`  |
| `StatusFlyout.ClearCommand`     | Status flyout Clear `Button.Command`       |
| `IncomeTypeFlyout.*`            | (same pattern as Status)                   |
| `PayingEntityFlyout.IsOpen`     | PayingEntity column `Popup.IsOpen`         |
| `PayingEntityFlyout.SearchText` | PayingEntity flyout `TextBox.Text`         |
| `PayingEntityFlyout.IsActive`   | PayingEntity funnel `PathIcon.Foreground`  |
| `PayingEntityFlyout.ApplyCommand`| PayingEntity flyout Apply `Button.Command`|
| `DeadlineFlyout.*`              | (same pattern as PayingEntity)             |
| `PaymentReferenceFlyout.*`      | (same pattern as PayingEntity)             |
| `IsFilterRowEnabled`            | Funnel button `IsEnabled` (inverted: disabled when ReportIdFilter active) |

### Flyout ViewModel → Parent ViewModel Data Flow

```
EnumFilterFlyoutViewModel.Apply()
  → reads checked Items
  → calls parent callback: Action<IReadOnlySet<T>?>
  → parent sets FilterStatus/FilterIncomeType
  → reactive pipeline triggers LoadPageAsync

TextFilterFlyoutViewModel.Apply()
  → reads SearchText
  → calls parent callback: Action<string?>
  → parent sets FilterPayingEntity/FilterDeadline/FilterPaymentReference
  → reactive pipeline triggers LoadPageAsync
```
