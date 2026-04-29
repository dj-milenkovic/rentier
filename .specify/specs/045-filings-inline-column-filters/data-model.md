# Data Model: Filings Inline Column Filters

**Feature**: 045-filings-inline-column-filters  
**Date**: 2025-07-15

## Entities Involved

### Filing (Existing — No Changes)

The `Filing` entity (`Rentier.Domain.Entities.Filing`) is the aggregate root displayed in the Filings DataGrid. **No domain model changes required** — filtering is applied at query/infrastructure level using existing entity properties.

| Property | Type | Filterable | Filter Control |
|----------|------|-----------|----------------|
| `Status` | `FilingStatus` (enum: Init, Filed, Paid) | ✅ | Dropdown |
| `IncomeType` | `IncomeType` (enum: Dividend, Interest) | ✅ | Dropdown |
| `PayingEntity` | `string` | ✅ | Text (contains, case-insensitive) |
| `FilingDeadline` | `DateOnly` | ✅ | Date picker (exact match) |
| `PaymentReference` | `string?` | ✅ | Text (contains, case-insensitive) |
| `TaxPayableRsd` | `decimal` | ❌ | N/A |

## New/Modified Types

### FilingColumnFilter (New — Application Layer)

A record that carries per-column filter parameters from the ViewModel through the query to the repository. Defined in `Rentier.Application.Queries` namespace.

```csharp
namespace Rentier.Application.Queries;

/// <summary>
/// Column-level filter criteria for the Filings DataGrid.
/// All fields are optional; null means "no filter on this column".
/// </summary>
public sealed record FilingColumnFilter(
    FilingStatus? Status = null,
    IncomeType? IncomeType = null,
    string? PayingEntity = null,
    DateOnly? FilingDeadline = null,
    string? PaymentReference = null);
```

### GetFilingsQuery (Modified — Application Layer)

Add `FilingColumnFilter? ColumnFilter` parameter to carry inline filter state.

```csharp
public sealed record GetFilingsQuery(
    FilingFilterMode Filter = FilingFilterMode.Unpaid,
    int Page = 1,
    int PageSize = 30,
    Guid? ReportIdFilter = null,
    FilingSortColumn SortColumn = FilingSortColumn.FilingDeadline,
    bool SortDescending = true,
    FilingColumnFilter? ColumnFilter = null) : IPaginatedQuery;
```

### IFilingRepository.GetPagedAsync (Modified — Application Layer)

Add `FilingColumnFilter? columnFilter` parameter.

```csharp
Task<(IReadOnlyList<Filing> Items, int TotalCount)> GetPagedAsync(
    FilingFilterMode filter,
    int skip,
    int take,
    FilingSortColumn sortColumn = FilingSortColumn.FilingDeadline,
    bool sortDescending = true,
    FilingColumnFilter? columnFilter = null,
    CancellationToken ct = default);
```

### FilingRepository.GetPagedAsync (Modified — Infrastructure Layer)

Extend the EF Core query chain to apply `WHERE` clauses for each non-null filter field:

```csharp
if (columnFilter is not null)
{
    if (columnFilter.Status.HasValue)
        query = query.Where(f => f.Status == columnFilter.Status.Value);
    if (columnFilter.IncomeType.HasValue)
        query = query.Where(f => f.IncomeType == columnFilter.IncomeType.Value);
    if (!string.IsNullOrEmpty(columnFilter.PayingEntity))
        query = query.Where(f => EF.Functions.Like(f.PayingEntity, $"%{columnFilter.PayingEntity}%"));
    if (columnFilter.FilingDeadline.HasValue)
        query = query.Where(f => f.FilingDeadline == columnFilter.FilingDeadline.Value);
    if (!string.IsNullOrEmpty(columnFilter.PaymentReference))
        query = query.Where(f => f.PaymentReference != null &&
            EF.Functions.Like(f.PaymentReference, $"%{columnFilter.PaymentReference}%"));
}
```

## State Model (ViewModel — Desktop Layer)

### FilingsViewModel Filter Properties (New)

| Property | Type | Default | Trigger |
|----------|------|---------|---------|
| `FilterStatus` | `FilingStatus?` | `null` | Immediate reload |
| `FilterIncomeType` | `IncomeType?` | `null` | Immediate reload |
| `FilterPayingEntity` | `string?` | `null` | Debounced (300ms) reload |
| `FilterDeadline` | `DateTimeOffset?` | `null` | Immediate reload |
| `FilterPaymentReference` | `string?` | `null` | Debounced (300ms) reload |
| `HasActiveFilters` (derived) | `bool` | `false` | Computed from above |

### Reactive Pipeline

```
WhenAnyValue(FilterStatus, FilterIncomeType, FilterDeadline)  ──┐
                                                                 ├─ Merge ─> ResetPage(1) ─> LoadPageCommand
WhenAnyValue(FilterPayingEntity, FilterPaymentReference)        │
    .Throttle(300ms)  ──────────────────────────────────────────┘

ClearFiltersCommand  ─> Reset all filter properties ─> LoadPageCommand
```

### Filter ↔ ReportIdFilter Interaction

| ReportIdFilter | Inline Filters | Behavior |
|---------------|---------------|----------|
| `null` | Active | Normal paginated + filtered query |
| `Guid` (set) | Cleared | Inline filters cleared; shows all filings for report |
| `Guid` → `null` | Available | User clears report filter, inline filters become usable |

## Validation Rules

- No new domain validation — filter values are optional query parameters.
- Text filter inputs: no length validation needed (UI naturally limits input).
- Date filter: `DateOnly` conversion from `DateTimeOffset` handles timezone by extracting date component only.
- `EF.Functions.Like` patterns must be sanitized: `%` and `_` characters in user input should be treated as literals. Use `EscapeLikePattern` helper if EF Core SQLite supports it, otherwise accept as-is for initial implementation (very unlikely in payer names or references).
