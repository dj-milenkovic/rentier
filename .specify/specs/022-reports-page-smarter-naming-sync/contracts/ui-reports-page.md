# UI Contract: Reports Page Display Name and Sync Clarification

**Feature**: `003-reports-naming-sync` | **Date**: 2025-07-09

## Overview

This contract defines the interface between the Application layer (query handler + DTO) and the Desktop layer (ViewModel + View) for the Reports page display name feature and sync clarification text.

---

## Contract 1: ReportRowDto → ReportRowViewModel Mapping

### Data Flow

```text
GetReportsQueryHandler
  → ReportRowDto (Application)
    → ReportRowViewModel.From(dto) (Desktop)
      → ReportsView.axaml bindings
```

### ReportRowDto Shape (Application → Desktop)

| Field | Type | Nullable | Description |
|-------|------|----------|-------------|
| `Id` | `Guid` | No | Report identifier |
| `ReportName` | `string` | No | Original file name (for tooltip) |
| `DisplayName` | `string` | No | Friendly label: `"{Importer} – {Date}"` |
| `ImportDate` | `DateOnly` | No | Date report was imported |
| `ImporterName` | `string` | No | Resolved importer name or "Unknown" |
| `Status` | `ReportStatus` | No | Current report status |
| `FilingCount` | `int` | No | Number of linked filings |
| `EarliestIncomeDate` | `DateOnly?` | Yes | Earliest filing income date, null if no filings |

### ReportRowViewModel Properties (Desktop bindings)

| Property | Type | Binding Target | Description |
|----------|------|----------------|-------------|
| `DisplayName` | `string` | DataGrid column text | Primary label shown in "Report" column |
| `OriginalFileName` | `string` | `ToolTip.Tip` on display name cell | Original file name for hover tooltip |
| `ImportDateDisplay` | `string` | Import Date column | Formatted as `yyyy-MM-dd` |
| `ImporterName` | `string` | Importer column | Resolved importer name |
| `Status` | `ReportStatus` | Status column (via converter) | Report processing status |
| `FilingCount` | `int` | Filings column | Filing count |
| `Id` | `Guid` | Command parameters | Row identifier for actions |

---

## Contract 2: Reports DataGrid Column Layout

### Before (current)

| # | Header | Binding | Width | Type |
|---|--------|---------|-------|------|
| 1 | "Report Name" | `ReportName` | `*` | `DataGridTextColumn` |
| 2 | "Import Date" | `ImportDateDisplay` | `110` | `DataGridTextColumn` |
| 3 | "Importer" | `ImporterName` | `160` | `DataGridTextColumn` |
| 4 | "Status" | `Status` (converter) | `100` | `DataGridTextColumn` |
| 5 | "Filings" | `FilingCount` | `70` | `DataGridTextColumn` |
| 6 | (actions) | ViewFilings, Delete | `Auto` | `DataGridTemplateColumn` |

### After (new)

| # | Header | Binding | Width | Type | Tooltip |
|---|--------|---------|-------|------|---------|
| 1 | **"Report"** | **`DisplayName`** | `*` | **`DataGridTemplateColumn`** | **`OriginalFileName`** |
| 2 | "Import Date" | `ImportDateDisplay` | `110` | `DataGridTextColumn` | — |
| 3 | "Importer" | `ImporterName` | `160` | `DataGridTextColumn` | — |
| 4 | "Status" | `Status` (converter) | `100` | `DataGridTextColumn` | — |
| 5 | "Filings" | `FilingCount` | `70` | `DataGridTextColumn` | — |
| 6 | (actions) | ViewFilings, Delete | `Auto` | `DataGridTemplateColumn` | — |

**Changes**:
- Column 1 changes from `DataGridTextColumn` to `DataGridTemplateColumn` (to support tooltip binding).
- Column 1 header changes from "Report Name" (`Reports_Col_Name`) to "Report" (`Reports_Col_DisplayName`).
- Column 1 cell contains `<TextBlock Text="{Binding DisplayName}" ToolTip.Tip="{Binding OriginalFileName}" TextTrimming="CharacterEllipsis" />`.

---

## Contract 3: Sync Clarification Text

### Placement

```text
┌──────────────────────────────────────────────────────────┐
│ [Import...] [Sync Mailboxes] ████░░░░ Syncing file.csv   │  ← toolbar
│ Sync downloads new statements from your configured        │  ← NEW subtitle
│ mailboxes and creates reports. For per-mailbox status     │
│ and history, use the Sync page.                           │
├──────────────────────────────────────────────────────────┤
│ Report        │ Import Date │ Importer │ Status │ Filings │
│ IBKR – 2024.. │ 2024-07-09  │ IBKR CSV │ Proc.  │ 12      │
│ ...           │ ...         │ ...      │ ...    │ ...     │
└──────────────────────────────────────────────────────────┘
```

### AXAML Structure

```xml
<!-- Sync subtitle (NEW) — between toolbar and loading indicator -->
<TextBlock DockPanel.Dock="Top"
           Text="{x:Static res:Strings.Reports_Sync_Subtitle}"
           Foreground="{DynamicResource TextFillColorSecondaryBrush}"
           FontSize="12"
           Margin="8,0,8,4"
           TextWrapping="Wrap" />
```

### String Resource

| Key | Value |
|-----|-------|
| `Reports_Sync_Subtitle` | "Sync downloads new statements from your configured mailboxes and creates reports. For per-mailbox status and history, use the Sync page." |

---

## Contract 4: Display Name Format Specification

### Pattern

```
{ImporterDisplayName} – {EffectiveDate:yyyy-MM-dd}
```

- **Separator**: En dash (U+2013), surrounded by spaces.
- **ImporterDisplayName**: `Importer.DisplayName` or `"Unknown"` if importer not found.
- **EffectiveDate**: `MIN(Filing.IncomeDate)` for the report, or `Report.ImportDate` if no filings exist.

### Examples

| ImporterName | Has Filings | Earliest IncomeDate | ImportDate | Display Name |
|---|---|---|---|---|
| "IBKR CSV" | Yes | 2024-03-15 | 2024-07-09 | "IBKR CSV – 2024-03-15" |
| "IBKR CSV" | No | — | 2024-07-09 | "IBKR CSV – 2024-07-09" |
| (deleted) | Yes | 2024-03-15 | 2024-07-09 | "Unknown – 2024-03-15" |
| (deleted) | No | — | 2024-07-09 | "Unknown – 2024-07-09" |
| "Very Long Importer Name..." | Yes | 2024-01-01 | 2024-07-09 | "Very Long Importer Name... – 2024-01-01" |
