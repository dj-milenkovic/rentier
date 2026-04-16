# Data Model: Default Sort & Column Sort for Filings and Reports

**Feature**: 027-default-sort-column-sort  
**Date**: 2025-07-24

## Overview

No new entities or database schema changes are introduced. This feature adds sort parameters to existing query records and modifies existing repository/handler signatures. All changes are additive with backward-compatible defaults.

## New Types

### FilingSortColumn (Enum — Application Layer)

**Location**: `src/Rentier.Application/Enums/FilingSortColumn.cs`  
**Purpose**: Strongly-typed identifier for sortable columns in the Filings DataGrid.

| Member           | Value | Maps To (Filing Property) | Type      |
|------------------|-------|---------------------------|-----------|
| `FilingDeadline` | 0     | `Filing.FilingDeadline`   | `DateOnly`|
| `Status`         | 1     | `Filing.Status`           | `FilingStatus` (enum/int) |
| `IncomeType`     | 2     | `Filing.IncomeType`       | `IncomeType` (enum/int) |
| `PayingEntity`   | 3     | `Filing.PayingEntity`     | `string`  |
| `TaxPayable`     | 4     | `Filing.TaxPayableRsd`    | `decimal` |
| `PaymentReference` | 5   | `Filing.PaymentReference` | `string?` |

**Validation Rules**:
- Defined as a C# enum; invalid values caught by the handler (return validation error).
- Default value: `FilingSortColumn.FilingDeadline` (used when no sort preference specified).

**Notes**:
- Status badge column and Status dropdown column share the same `FilingStatus` sort key.
- PaymentReference sorts nulls consistently (nulls sort first in ascending, last in descending — or per EF Core/SQLite default behavior).

## Modified Types

### GetFilingsQuery (Record — Application Layer)

**Location**: `src/Rentier.Application/Queries/GetFilingsQuery.cs`

| Parameter       | Type               | Default                          | Change   |
|-----------------|--------------------|----------------------------------|----------|
| `Filter`        | `FilingFilterMode`  | `FilingFilterMode.Unpaid`       | Existing |
| `Page`          | `int`              | `1`                              | Existing |
| `PageSize`      | `int`              | `20`                             | Existing |
| `ReportIdFilter`| `Guid?`            | `null`                           | Existing |
| `SortColumn`    | `FilingSortColumn`  | `FilingSortColumn.FilingDeadline`| **NEW**  |
| `SortDescending`| `bool`             | `true`                           | **NEW**  |

**Backward Compatibility**: All new parameters have defaults. Existing call sites (e.g., `new GetFilingsQuery(filter, page, 20, reportIdFilter)`) compile unchanged and get the new default sort behavior (descending by deadline).

### GetReportsQuery (Record — Application Layer)

**Location**: `src/Rentier.Application/Queries/GetReportsQuery.cs`

| Parameter       | Type   | Default | Change   |
|-----------------|--------|---------|----------|
| `SortDescending`| `bool` | `true`  | **NEW**  |

**Backward Compatibility**: Existing `new GetReportsQuery()` compiles unchanged and gets descending sort by default.

### IFilingRepository.GetPagedAsync (Interface — Application Layer)

**Location**: `src/Rentier.Application/Repositories/IFilingRepository.cs`

Current signature:
```csharp
Task<(IReadOnlyList<Filing> Items, int TotalCount)> GetPagedAsync(
    FilingFilterMode filter, int skip, int take, CancellationToken ct = default);
```

New signature:
```csharp
Task<(IReadOnlyList<Filing> Items, int TotalCount)> GetPagedAsync(
    FilingFilterMode filter, int skip, int take,
    FilingSortColumn sortColumn = FilingSortColumn.FilingDeadline,
    bool sortDescending = true,
    CancellationToken ct = default);
```

**Note**: Default parameter values ensure backward compatibility. The `CancellationToken` must remain the last parameter per C# convention.

### IReportRepository.GetAllAsync (Interface — Application Layer)

**Location**: `src/Rentier.Application/Repositories/IReportRepository.cs`

Current signature:
```csharp
Task<IReadOnlyList<Report>> GetAllAsync(CancellationToken ct = default);
```

New signature:
```csharp
Task<IReadOnlyList<Report>> GetAllAsync(bool sortDescending = true, CancellationToken ct = default);
```

## Sort Ordering Rules

### Filings (Paginated)

1. Primary sort: by `SortColumn` in `SortDescending` direction.
2. Secondary sort: by `Filing.Id` ascending (deterministic tie-breaker to prevent row flickering).
3. Applied at the EF Core query level (`ORDER BY` in SQL) before `Skip`/`Take`.

### Reports (Non-Paginated)

1. Sort: by `Report.ImportDate` in `SortDescending` direction.
2. Secondary sort: by `Report.Id` ascending (deterministic tie-breaker).
3. Applied at the EF Core query level in `ReportRepository.GetAllAsync`.

## State Transitions (ViewModel Sort State)

### FilingsViewModel Sort State Machine

```text
┌─────────────────────────────────────────────────────────────┐
│ Initial State: SortColumn=FilingDeadline, SortDescending=true│
└───────────────────────┬─────────────────────────────────────┘
                        │
        ┌───────────────┴───────────────┐
        ▼                               ▼
  Click SAME column              Click DIFFERENT column
        │                               │
        ▼                               ▼
  Toggle SortDescending          Set SortColumn = new
  Page stays the same            Set SortDescending = true (asc)
        │                        Reset Page to 1
        ▼                               │
  LoadPageAsync()                        ▼
                                 LoadPageAsync()
```

**Page-reset rules** (from spec FR-009, FR-010):
- Changing sort direction only → page NOT reset
- Changing sort column → page reset to 1
- Changing filter → page reset to 1 (existing behavior, preserved)

## Relationships Diagram

```text
  FilingsView.axaml                    FilingsViewModel
  ┌──────────────┐                   ┌──────────────────────┐
  │  DataGrid     │──Sorting event──▶│  ApplySortCommand    │
  │  CanUserSort  │                  │  _sortColumn         │
  │  = True       │                  │  _sortDescending     │
  └──────────────┘                   │  LoadPageAsync()     │
                                     └──────┬───────────────┘
                                            │
                                            ▼ constructs
                                     GetFilingsQuery(SortColumn, SortDescending)
                                            │
                                            ▼ handled by
                                     GetFilingsQueryHandler
                                            │
                                            ▼ calls
                                     IFilingRepository.GetPagedAsync(sortColumn, sortDescending)
                                            │
                                            ▼ implemented by
                                     FilingRepository (EF Core ORDER BY)
```
