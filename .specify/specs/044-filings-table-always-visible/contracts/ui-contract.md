# UI Contract: Filings Page Empty State

## Visual Contract

### State: Empty (zero filings)

```
┌─────────────────────────────────────────────────────────────────┐
│ [○ Unpaid] [● All]                              [+ New Filing]  │
├────┬────────┬───────────┬──────────────┬──────────┬─────┬──────┤
│ ☐  │ Status │ Income    │ Paying       │ Deadline │ Tax │ Ref  │ Actions │
│    │        │ Type      │ Entity       │          │ Pay │      │         │
├────┴────────┴───────────┴──────────────┴──────────┴─────┴──────┤
│                                                                 │
│              No filings yet.  (muted, centered)                 │
│                                                                 │
├─────────────────────────────────────────────────────────────────┤
│                    [< Prev]  1 / 1  [Next >]                    │
└─────────────────────────────────────────────────────────────────┘
```

### State: Populated (one or more filings)

No change from current behavior. DataGrid renders rows as before. Empty-state message is hidden.

### State: Loading

DataGrid with column headers is visible. Progress bar appears above. Empty-state message is hidden during loading (`IsEmpty = Rows.Count == 0 && !IsLoading`).

### State: Error

DataGrid with column headers is visible. Error banner appears above the DataGrid. Table shows last-loaded data or empty.

## Behavioral Rules

| Rule | Description |
|------|-------------|
| DataGrid always visible | `IsVisible` is not bound; DataGrid renders in all states |
| Empty message condition | Shown when `IsEmpty == true` (no rows AND not loading) |
| Empty message style | `RentierTextSecondaryBrush`, `FontSize="13"`, centered horizontally |
| No layout shift | Table structure (headers, pagination) stable across all transitions |
| Select-all disabled when empty | Checkbox `IsEnabled="{Binding HasItems}"` unchanged |
