# Data Model: Filings Per-Report Filter Chip

**Feature**: 043-filings-filter-chip  
**Date**: 2025-07-22

## Overview

This feature is entirely within the Desktop (presentation) layer. No Domain entities, Application DTOs, or database schema changes are required. The data model describes ViewModel state additions only.

## ViewModel State Changes

### FilingsViewModel (modified)

| Property | Type | Kind | Description |
|---|---|---|---|
| `ReportIdFilter` | `Guid?` | **Existing** | When non-null, restricts displayed filings to a specific report. Already triggers `LoadPageCommand` via reactive pipeline. |
| `HasReportFilter` | `bool` | **New (read-only)** | Derived from `ReportIdFilter != null`. Drives chip visibility in the View. Implemented as `ObservableAsPropertyHelper<bool>`. |
| `ClearReportFilterCommand` | `ReactiveCommand<Unit, Unit>` | **New** | Sets `ReportIdFilter = null`. Enabled only when `HasReportFilter` is `true`. |

### State Diagram

```text
[No Report Filter]                    [Report Filter Active]
  HasReportFilter = false               HasReportFilter = true
  Chip: hidden                          Chip: visible
          │                                      │
          │  navigateToFilings(reportId)          │  ClearReportFilterCommand
          │  sets ReportIdFilter = guid           │  sets ReportIdFilter = null
          ▼                                      ▼
  [Report Filter Active]               [No Report Filter]
  LoadPageCommand fires                 LoadPageCommand fires
  (filtered results)                    (all results, page 1)
```

### Composition with Existing Filters

The report filter composes with the existing `ShowAll` (All/Unpaid) toggle:

| ShowAll | ReportIdFilter | Result |
|---------|----------------|--------|
| `false` (Unpaid) | `null` | All unpaid filings |
| `true` (All) | `null` | All filings |
| `false` (Unpaid) | `{guid}` | Unpaid filings for report `{guid}` |
| `true` (All) | `{guid}` | All filings for report `{guid}` |

## Localization Entries

### Strings.resx (new entries)

| Key | Value (en) | Usage |
|---|---|---|
| `Filings_FilterChip_Report` | `Filtered by report` | Chip display text |
| `Filings_FilterChip_Dismiss` | `Remove report filter` | Accessible name for ✕ button |

## No Database/Domain/Application Changes

- **Domain**: No entity or value object changes.
- **Application**: No new commands, queries, or DTOs. Existing `GetFilingsQuery` already accepts optional `reportId` parameter.
- **Infrastructure**: No repository or migration changes.
