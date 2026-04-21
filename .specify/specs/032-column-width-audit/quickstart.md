# Quickstart: Column Width Audit — Filings & Reports Tables

**Feature**: 032-column-width-audit
**Date**: 2025-07-17

## What This Feature Does

Standardizes all column widths and cell padding across the Filings and Reports DataGrids so that content is never truncated, screen space is not wasted, and both tables present a visually consistent appearance.

## Files to Modify

Only **two files** require changes:

| File | Path | Change Summary |
|------|------|----------------|
| FilingsView.axaml | `src/Rentier.Desktop/Views/FilingsView.axaml` | Column widths + cell margins |
| ReportsView.axaml | `src/Rentier.Desktop/Views/ReportsView.axaml` | Column widths + cell margins |

## Implementation Steps

### Step 1: Update Filings DataGrid Column Widths

In `FilingsView.axaml`, update the `Width` attribute on each `DataGridColumn`:

| Column | Find | Replace With |
|--------|------|-------------|
| Selection | `Width="42"` | `Width="40"` |
| Status | `Width="84"` | `Width="90"` |
| Income Type | `Width="96"` | `Width="110"` |
| Paying Entity | `Width="*"` | (no change) |
| Filing Deadline | `Width="110"` | `Width="120"` |
| Tax Payable | `Width="120"` | `Width="130"` |
| Payment Reference | `Width="140"` | `Width="180"` |
| Actions | `Width="108"` | `Width="Auto"` |

### Step 2: Update Reports DataGrid Column Widths

In `ReportsView.axaml`, update the `Width` attribute on each `DataGridColumn`:

| Column | Find | Replace With |
|--------|------|-------------|
| Selection | `Width="44"` | `Width="40"` |
| Report Name | `Width="2*"` | `Width="*"` |
| Import Date | `Width="96"` | `Width="110"` |
| Email Date | `Width="96"` | `Width="110"` |
| Importer | `Width="120"` | `Width="160"` |
| Status | `Width="88"` | `Width="100"` |
| Filing Count | `Width="56"` | `Width="70"` |
| Actions | `Width="88"` | `Width="Auto"` |

### Step 3: Normalize Cell Padding

Apply `Margin="4,0"` to all cell content elements:

**For `DataGridTextColumn`** — add an `ElementStyle`:
```xml
<DataGridTextColumn.ElementStyle>
    <Style Selector="TextBlock">
        <Setter Property="Margin" Value="4,0" />
    </Style>
</DataGridTextColumn.ElementStyle>
```

**For `DataGridTemplateColumn`** — set `Margin="4,0"` on the primary content element inside `<CellTemplate>` (CheckBox, Border, TextBox, StackPanel).

Normalize existing inconsistent margins:
- Status badge Border: `Margin="4,2"` → `Margin="4,0"`
- Actions StackPanel (Filings): `Margin="4,2"` → `Margin="4,0"`
- Actions StackPanel (Reports): `Margin="4,2"` → `Margin="4,0"`

## Verification

1. **Build**: `dotnet build src/Rentier.Desktop/`
2. **Run**: Launch the app and navigate to Filings and Reports pages
3. **Check columns**: Verify each column matches the target width — no truncation, no excess padding
4. **Check padding**: Verify uniform 4px horizontal spacing in all cells
5. **Check behavior**: Confirm sorting, selection, editing, and commands still work
6. **Window resize**: Shrink the window — star columns should compress, fixed columns hold their width

## What NOT to Change

- **No ViewModel changes** — all bindings, commands, and properties remain unchanged
- **No Domain/Application/Infrastructure changes** — this is Desktop layer only
- **No new files** — only modify existing view AXAML files
- **No converter changes** — status/income-type converters are unaffected
- **No header changes** — column headers and localization keys stay the same
