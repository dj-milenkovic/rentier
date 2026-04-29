# UI Contracts: Filings Visual Sorting

## Column Header Sort Arrow Contract

Each sortable DataGrid column header displays a sort indicator arrow reflecting the current sort state.

### Visual States

| State | Arrow | Visibility | Header Appearance |
|---|---|---|---|
| Unsorted (column not active) | None | Hidden | Column label only |
| Ascending (this column active, `SortDescending = false`) | ↑ (chevron-up) | Visible | Label + ↑ arrow |
| Descending (this column active, `SortDescending = true`) | ↓ (chevron-down) | Visible | Label + ↓ arrow |

### Sort Cycle (click sequence on same column)

```
unsorted → ascending (↑) → descending (↓) → unsorted → ...
```

### Column Sort Interaction

```
Click sortable column header
  ├── Column is currently unsorted
  │   └── Set ascending sort on this column (↑ appears)
  ├── Column is currently ascending
  │   └── Set descending sort on this column (↓ appears)
  └── Column is currently descending
      └── Clear sort (arrow disappears, unsorted state)

Click DIFFERENT sortable column header
  └── Previous column loses arrow → New column gets ascending (↑)
```

### Sortable Columns

| Column | Tag (for ViewModel binding) | Sortable |
|---|---|---|
| Selection checkbox | — | No |
| Status badge | — | No |
| Income Type | `IncomeType` | Yes |
| Paying Entity | `PayingEntity` | Yes |
| Filing Deadline | `FilingDeadline` | Yes |
| Tax Payable | `TaxPayable` | Yes |
| Payment Reference | `PaymentReference` | No |
| Actions | — | No |

### Arrow Icon Specification

```
Ascending (chevron-up):   M7 14 L12 9 L17 14
Descending (chevron-down): M7 10 L12 15 L17 10
```

Size: 10×10, placed to the right of the column label text with 4px spacing.

## Toolbar Contract (After Removal)

### Before (current)
```
[○ Unpaid] [● All] [Filtered by report ✕] [↓ FilingDeadline]    [+ New Filing]
```

### After (this feature)
```
[Filtered by report ✕]                                           [+ New Filing]
```

Removed elements:
- "Unpaid" radio button
- "All" radio button
- Sort indicator text display (`SortIndicatorDisplay`)

Preserved elements:
- Report filter chip (visible only when `HasReportFilter` is true)
- "New Filing" button (right-aligned)
- Bulk selection toolbar (separate row below, unchanged)

## Hover Affordance

Sortable column headers show `Cursor="Hand"` on hover.
Non-sortable column headers retain default cursor.
