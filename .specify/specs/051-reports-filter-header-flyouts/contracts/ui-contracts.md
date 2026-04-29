# UI Contracts: Reports Filter Header Flyouts

**Feature**: 051-reports-filter-header-flyouts
**Date**: 2025-07-15

## Column Header Contract

Every filterable DataGrid column header MUST render as:

```
┌──────────────────────────────┐
│  Column Label Text    🔽     │   ← 🔽 = funnel PathIcon (default color or accent when active)
└──────────────────────────────┘
```

The funnel icon is a `Button` with `Flyout` attached. Clicking the button opens the flyout anchored to the header.

## Text Filter Flyout Contract (Name, Importer)

```
┌─────────────────────────┐
│  ┌───────────────────┐  │
│  │ Pretraži...       │  │   ← TextBox, pre-populated with current filter value
│  └───────────────────┘  │
│            [Primijeni]  │   ← Apply button, right-aligned
└─────────────────────────┘
```

**Bindings**:
- TextBox.Text → local staged value (NOT directly to ViewModel filter property)
- Apply button → copies staged value to ViewModel filter property, closes flyout

## Date/Numeric Filter Flyout Contract (ImportDate, EmailDate, FilingCount)

Same layout as Text Filter Flyout. For dates, placeholder is "Pretraži datum..." (search date). For filing count, placeholder is "#".

**Behavior differences**:
- Date: value passed as-is (string contains search on formatted date)
- Numeric: value parsed to `int`; if parse fails, filter is cleared (silently ignored)

## Enum Filter Flyout Contract (Status/ReportStatus)

```
┌─────────────────────────┐
│  [Odaberi sve] [Očisti]  │   ← Select All / Clear links or buttons
│  ☑ Init                 │
│  ☑ Processed            │
│  ☑ Error                │
│  ☑ PartialError         │
│            [Primijeni]  │   ← Apply button
└─────────────────────────┘
```

**Bindings**:
- Each checkbox → local `StatusCheckboxItem.IsChecked` (staged)
- Select All → checks all items
- Clear → unchecks all items
- Apply → collects checked statuses → sets on ViewModel, closes flyout

## Funnel Icon States

| State | Appearance |
|-------|-----------|
| No active filter | Default foreground color (`SystemControlForegroundBaseMediumBrush`) |
| Active filter | Accent color (`RentierAccentBrush` or `SystemAccentColor`) |

## Resource String Keys (new)

| Key | Value | Usage |
|-----|-------|-------|
| `Reports_Filter_Apply` | "Primijeni" | Apply button in all flyouts |
| `Reports_Filter_SelectAll` | "Odaberi sve" | Status flyout Select All |
| `Reports_Filter_ClearSelection` | "Očisti" | Status flyout Clear |
| `Reports_Filter_Date_Watermark` | "Pretraži datum..." | Date flyout placeholder |
| `Reports_Filter_Count_Watermark` | "#" | Filing count flyout placeholder |

## Existing Resource Keys (reused)

| Key | Current Value | Reused In |
|-----|--------------|-----------|
| `Reports_Filter_Name_Watermark` | (existing) | Name flyout placeholder |
| `Reports_Filter_Importer_Watermark` | (existing) | Importer flyout placeholder |
| `Reports_Filter_Clear` | (existing) | Clear All Filters toolbar button |
