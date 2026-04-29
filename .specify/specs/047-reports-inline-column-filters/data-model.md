# Data Model: Reports Inline Column Filters

**Feature**: 047-reports-inline-column-filters  
**Date**: 2025-07-15

## Entities

### Report (Existing — Domain Layer, Unchanged)

| Field | Type | Nullable | Notes |
|-------|------|----------|-------|
| Id | `Guid` | No | PK |
| ImportDate | `DateOnly` | No | Constitution Principle III |
| EmailDate | `DateOnly?` | Yes | Nullable — filter excludes nulls when active |
| ImporterId | `Guid` | No | FK to Importer |
| Status | `ReportStatus` | No | Enum: Init, Processed, Error, PartialError |
| ReportName | `string` | No | Max 500 chars |
| AttachmentContent | `byte[]?` | Yes | Not filterable |
| MailboxMessageId | `long?` | Yes | Not filterable |
| OriginalReportId | `Guid?` | Yes | Not filterable |

**No domain changes required.** All filter logic operates at the Application/Infrastructure boundary.

### ReportStatus (Existing — Domain Enum, Unchanged)

```csharp
public enum ReportStatus
{
    Init = 0,
    Processed = 1,
    Error = 2,
    PartialError = 3
}
```

## New Types

### ComparisonOperator (NEW — Application Enum)

**Location**: `src/Rentier.Application/Enums/ComparisonOperator.cs`

```csharp
namespace Rentier.Application.Enums;

/// <summary>
/// Comparison operator for numeric and date column filters.
/// Shared across all pages that support operator-based inline filters.
/// </summary>
public enum ComparisonOperator
{
    Equals = 0,
    GreaterThan = 1,
    LessThan = 2
}
```

**Rationale**: Reusable across Reports (this feature) and Filings (feature 045/046). Default is `Equals` per FR-003/FR-004.

### ReportColumnFilter (NEW — Application DTO)

**Location**: `src/Rentier.Application/DTOs/ReportColumnFilter.cs`

```csharp
namespace Rentier.Application.DTOs;

using Rentier.Application.Enums;
using Rentier.Domain.Enums;

/// <summary>
/// Immutable filter criteria for the Reports paged query.
/// All fields are optional — null/default means "no filter on this column."
/// </summary>
public sealed record ReportColumnFilter(
    string? NameContains = null,
    string? ImporterContains = null,
    ComparisonOperator ImportDateOperator = ComparisonOperator.Equals,
    DateOnly? ImportDateValue = null,
    ComparisonOperator EmailDateOperator = ComparisonOperator.Equals,
    DateOnly? EmailDateValue = null,
    ComparisonOperator FilingCountOperator = ComparisonOperator.Equals,
    int? FilingCountValue = null,
    ReportStatus? StatusFilter = null);
```

**Design decisions**:
- `record` type for immutability and value equality (easy to compare if filters changed).
- Text filters use `Contains` suffix to clarify they are substring matches.
- Operator fields have defaults (`Equals`) so a filter with only a value set uses "=" by default (FR-003, FR-004).
- `StatusFilter` is nullable — `null` means "All" (no filter), matching the dropdown's default option (FR-005).
- The `ReportColumnFilter.Default` pattern: `new ReportColumnFilter()` means "no filters active."

## Modified Types

### GetReportsQuery (MODIFIED — Application Query)

**Location**: `src/Rentier.Application/Queries/GetReportsQuery.cs`

```csharp
namespace Rentier.Application.Queries;

/// <summary>
/// Returns a paged, filtered list of Report records as display rows.
/// </summary>
public sealed record GetReportsQuery(
    int Page = 1,
    int PageSize = 30,
    bool SortDescending = true,
    ReportColumnFilter? Filter = null) : IPaginatedQuery;
```

**Change**: Added optional `Filter` parameter. Default `null` preserves backward compatibility — existing callers continue to work without filters.

### IReportRepository (MODIFIED — Application Interface)

Add new method:

```csharp
/// <summary>
/// Returns a paged, filtered list of reports with total count for pagination.
/// Filtering and pagination are applied at the database level.
/// </summary>
Task<(IReadOnlyList<Report> Items, int TotalCount)> GetPagedAsync(
    ReportColumnFilter? filter,
    int skip,
    int take,
    bool sortDescending,
    CancellationToken ct = default);
```

**Note**: The existing `GetAllAsync` method is preserved for other callers. The handler switches to `GetPagedAsync`.

### ReportRepository (MODIFIED — Infrastructure)

Implements `GetPagedAsync` with EF Core `IQueryable` composition:

```csharp
public async Task<(IReadOnlyList<Report> Items, int TotalCount)> GetPagedAsync(
    ReportColumnFilter? filter,
    int skip,
    int take,
    bool sortDescending,
    CancellationToken ct = default)
{
    var query = _db.Reports.AsNoTracking();

    // Apply filters
    if (filter is not null)
    {
        if (!string.IsNullOrWhiteSpace(filter.NameContains))
            query = query.Where(r => r.ReportName.Contains(filter.NameContains));

        if (!string.IsNullOrWhiteSpace(filter.ImporterContains))
            // ImporterName is not on Report entity — handled in handler post-query
            // OR resolved via JOIN. See note below.
            ;

        if (filter.ImportDateValue.HasValue)
            query = ApplyDateFilter(query, r => r.ImportDate, filter.ImportDateOperator, filter.ImportDateValue.Value);

        if (filter.EmailDateValue.HasValue)
            query = query.Where(r => r.EmailDate.HasValue); // Exclude nulls first
            query = ApplyNullableDateFilter(query, r => r.EmailDate, filter.EmailDateOperator, filter.EmailDateValue.Value);

        if (filter.StatusFilter.HasValue)
            query = query.Where(r => r.Status == filter.StatusFilter.Value);

        // FilingCount filter handled in handler (requires JOIN or subquery)
    }

    // Sort
    query = sortDescending
        ? query.OrderByDescending(r => r.EmailDate ?? r.ImportDate)
               .ThenByDescending(r => r.ImportDate)
               .ThenByDescending(r => r.Id)
        : query.OrderBy(r => r.EmailDate ?? r.ImportDate)
               .ThenBy(r => r.ImportDate)
               .ThenBy(r => r.Id);

    var totalCount = await query.CountAsync(ct);
    var items = await query.Skip(skip).Take(take).ToListAsync(ct);
    return (items.AsReadOnly(), totalCount);
}
```

**Important notes on non-Report-entity filters**:
- **ImporterName**: The `Report` entity stores `ImporterId` (Guid), not the importer name. The display name is resolved in the handler by joining with `IImporterRepository`. For `ImporterContains` filtering, the handler must pre-resolve matching importer IDs and pass them to the repository, OR the repository query must JOIN with the Importers table. The recommended approach is to resolve matching `ImporterId` values in the handler first, then pass them as a `IReadOnlySet<Guid>? importerIds` parameter to `GetPagedAsync`.
- **FilingCount**: Filing count is not a column on the `Report` table — it's computed by `_filings.GetFilingCountByReportIdAsync()`. For filtering by filing count, the handler must either: (a) use a subquery/JOIN in the repository, or (b) apply the filter post-pagination as a handler concern. Given the low page size (30), post-pagination filtering with over-fetch is acceptable for v1.

### GetReportsQueryHandler (MODIFIED — Application Handler)

Refactored flow:
1. Validate pagination.
2. If `ImporterContains` filter is set, pre-resolve matching importer IDs.
3. Call `_reports.GetPagedAsync(filter, skip, take, sortDescending, ct)`.
4. For each paged report row, resolve importer name + filing count (max 30 rows).
5. If `FilingCountValue` filter is set, post-filter the page results by filing count.
6. Return `ReportsPageResult`.

### ReportRowDto (UNCHANGED)

No changes. The DTO already contains all fields needed for filter display:
- `ReportName` (text filter target)
- `ImportDate` (date filter target)
- `EmailDate` (nullable date filter target)
- `ImporterName` (text filter target — resolved in handler)
- `Status` (enum filter target)
- `FilingCount` (numeric filter target — resolved in handler)

## ViewModel Filter State

### ReportsViewModel Filter Properties (Desktop Layer)

New reactive properties added to `ReportsViewModel`:

| Property | Type | Default | Debounced | Triggers Reload |
|----------|------|---------|-----------|-----------------|
| `NameFilter` | `string?` | `null` | Yes (300ms) | Yes |
| `ImporterFilter` | `string?` | `null` | Yes (300ms) | Yes |
| `ImportDateOperator` | `ComparisonOperator` | `Equals` | No | Yes (if value set) |
| `ImportDateFilter` | `DateOnly?` | `null` | No | Yes |
| `EmailDateOperator` | `ComparisonOperator` | `Equals` | No | Yes (if value set) |
| `EmailDateFilter` | `DateOnly?` | `null` | No | Yes |
| `FilingCountOperator` | `ComparisonOperator` | `Equals` | No | Yes (if value set) |
| `FilingCountFilter` | `int?` | `null` | No | Yes |
| `StatusFilter` | `ReportStatus?` | `null` | No | Yes |
| `HasActiveFilters` | `bool` (OAPH) | `false` | — | Controls "Clear filters" visibility |

**Reactive pipeline**:
```
Text filters → Throttle(300ms) → Reset page to 1 → InvokeCommand(LoadPageCommand)
Dropdown/operator filters → Skip(1) → Reset page to 1 → InvokeCommand(LoadPageCommand)
```

**ClearFiltersCommand**: Resets all filter properties to defaults. Only enabled when `HasActiveFilters` is true.

## State Transitions

```text
No Filter Active
    │
    ├── User types in text input ──→ Throttle 300ms ──→ Filters Active (page resets to 1)
    ├── User selects operator ──→ (no reload until value is set)
    ├── User enters date/number ──→ Filters Active (page resets to 1)
    ├── User selects status ──→ Filters Active (page resets to 1)
    │
Filters Active
    │
    ├── User modifies any filter ──→ Filters Active (page resets to 1)
    ├── User clicks "Clear filters" ──→ No Filter Active (page resets to 1)
    ├── User navigates pages ──→ Filters Active (page changes, filters preserved)
    └── User changes sort ──→ Filters Active (page resets to 1, filters preserved)
```

## Validation Rules

| Filter Type | Validation | On Invalid Input |
|-------------|-----------|------------------|
| Text (Name, Importer) | Any string, including empty (= no filter) | N/A — all input valid |
| Date (ImportDate, EmailDate) | Must parse to valid `DateOnly` | Silently ignored — no filter applied (FR-012 analog) |
| Numeric (FilingCount) | Must parse to valid `int` | Silently ignored — no filter applied (FR-012) |
| Enum (Status) | Must be valid `ReportStatus` or null | Dropdown constrains to valid values |
| Operator | Must be valid `ComparisonOperator` | Dropdown constrains to valid values |
