# UI Filter Contract: Filings DataGrid Inline Filters

**Feature**: 045-filings-inline-column-filters

## Filter Row Layout

The filter row is a horizontal panel visually aligned with DataGrid columns, positioned between the column headers and data rows. It is always visible (not toggleable).

```
┌──────────┬───────────┬──────────────┬──────────────┬──────────┬───────────────────┬──────────┐
│ ☐ Select │  Status   │  Tip prihoda │  Isplatilac  │ Rok za   │ Referenca plaćanja│ Actions  │
│          │           │              │              │ podnoš.  │                   │          │
├──────────┼───────────┼──────────────┼──────────────┼──────────┼───────────────────┼──────────┤
│          │ [▾ All  ] │ [▾ All     ] │ [Filter... ] │ [📅    ] │ [Filter...       ]│ [Clear ⌫]│  ← Filter Row
├──────────┼───────────┼──────────────┼──────────────┼──────────┼───────────────────┼──────────┤
│ ☐        │ Init      │ Dividend     │ MSFT Corp    │ 2025-02  │ PP-OPO-123       │ ⬆ 📄 🗑  │  ← Data Rows
│ ☐        │ Filed     │ Interest     │ AAPL Inc     │ 2025-03  │                  │ ⬆ 📄 🗑  │
└──────────┴───────────┴──────────────┴──────────────┴──────────┴───────────────────┴──────────┘
```

## Filter Controls

### Status Column (Dropdown)
- **Control**: `ComboBox`
- **Options**: "Svi" (All), "Inicijalan" (Init), "Podnet" (Filed), "Plaćen" (Paid)
- **Default**: "Svi" (null filter value)
- **Binding**: `SelectedItem` → `FilingsViewModel.FilterStatus`
- **Trigger**: Immediate on selection change

### Income Type Column (Dropdown)
- **Control**: `ComboBox`
- **Options**: "Svi" (All), "Dividenda" (Dividend), "Kamata" (Interest)
- **Default**: "Svi" (null filter value)
- **Binding**: `SelectedItem` → `FilingsViewModel.FilterIncomeType`
- **Trigger**: Immediate on selection change

### Payer Column (Text)
- **Control**: `TextBox` with `Watermark="Filter..."`
- **Binding**: `Text` → `FilingsViewModel.FilterPayingEntity`
- **Trigger**: Debounced 300ms after last keystroke
- **Match**: Case-insensitive `LIKE '%value%'`

### Filing Deadline Column (Date)
- **Control**: `CalendarDatePicker` with compact display
- **Binding**: `SelectedDate` → `FilingsViewModel.FilterDeadline`
- **Trigger**: Immediate on date selection
- **Match**: Exact `DateOnly` equality

### Payment Reference Column (Text)
- **Control**: `TextBox` with `Watermark="Filter..."`
- **Binding**: `Text` → `FilingsViewModel.FilterPaymentReference`
- **Trigger**: Debounced 300ms after last keystroke
- **Match**: Case-insensitive `LIKE '%value%'`

### Clear Filters Button
- **Control**: `Button` in Actions column area of filter row
- **Label**: Localizable, icon-based (clear/reset icon)
- **Visibility**: Only visible when `HasActiveFilters` is `true`
- **Command**: `ClearFiltersCommand` → resets all filter properties to defaults

## ViewModel Binding Contract

```csharp
// Filter properties
FilingStatus? FilterStatus { get; set; }
IncomeType? FilterIncomeType { get; set; }
string? FilterPayingEntity { get; set; }
DateTimeOffset? FilterDeadline { get; set; }
string? FilterPaymentReference { get; set; }

// Derived state
bool HasActiveFilters { get; }          // Any filter non-default
bool IsFilterRowEnabled { get; }        // false when ReportIdFilter is active

// Commands
ReactiveCommand<Unit, Unit> ClearFiltersCommand { get; }

// Dropdown item sources
IReadOnlyList<FilterOption<FilingStatus?>> StatusFilterOptions { get; }
IReadOnlyList<FilterOption<IncomeType?>> IncomeTypeFilterOptions { get; }
```

### FilterOption Helper

```csharp
public sealed record FilterOption<T>(string Label, T Value);
```

Used to populate ComboBox items with localized display labels mapped to nullable enum values. `null` represents "All" (no filter).

## Empty State Contract

When filters produce zero results:
- **Message**: "Nema prijava koje odgovaraju aktivnim filterima" / "No filings match the active filters"
- **Location**: Centered in the DataGrid content area
- **Condition**: `Rows.Count == 0 && HasActiveFilters`
- **Action hint**: "Clear filters" link/button alongside the message

## Interaction with Existing UI

### ShowAll Toggle
- The existing `ShowAll` toggle (Unpaid/All) continues to function as a top-level filter.
- Inline column filters work _within_ the ShowAll selection (AND logic).
- When `ShowAll` is false (Unpaid mode), Status dropdown still shows all options but results are intersected with Unpaid constraint.

### Report Filter Chip
- When `ReportIdFilter` is active, the filter row controls are disabled (greyed out).
- Clearing the report filter chip re-enables inline filter controls.
- Setting `ReportIdFilter` clears all inline filter values.

### Pagination
- Changing any filter resets `CurrentPage` to 1.
- `TotalPages` and `TotalCount` reflect filtered result counts.
- Page navigation operates within the filtered set.

### Sort
- Sorting applies within the filtered result set.
- Changing sort does not affect active filters.
