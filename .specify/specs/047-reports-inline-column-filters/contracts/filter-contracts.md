# Filter Contracts: Reports Inline Column Filters

**Feature**: 047-reports-inline-column-filters  
**Date**: 2025-07-15

## Contract 1: ViewModel → Application Query

The `ReportsViewModel` constructs a `GetReportsQuery` with filter criteria and delegates to the query handler.

### Input: GetReportsQuery

```csharp
public sealed record GetReportsQuery(
    int Page = 1,
    int PageSize = 30,
    bool SortDescending = true,
    ReportColumnFilter? Filter = null) : IPaginatedQuery;
```

### Filter Criteria: ReportColumnFilter

```csharp
public sealed record ReportColumnFilter(
    string? NameContains = null,           // Case-insensitive substring match on DisplayName/ReportName
    string? ImporterContains = null,        // Case-insensitive substring match on resolved ImporterName
    ComparisonOperator ImportDateOperator = ComparisonOperator.Equals,
    DateOnly? ImportDateValue = null,       // Applied only when non-null
    ComparisonOperator EmailDateOperator = ComparisonOperator.Equals,
    DateOnly? EmailDateValue = null,        // Excludes null EmailDate rows when active
    ComparisonOperator FilingCountOperator = ComparisonOperator.Equals,
    int? FilingCountValue = null,           // Applied only when non-null
    ReportStatus? StatusFilter = null);     // null = "All" (no filter)
```

### Output: ReportsPageResult (Unchanged)

```csharp
public sealed record ReportsPageResult(
    IReadOnlyList<ReportRowDto> Rows,    // Max PageSize items (filtered + paginated)
    int TotalCount,                       // Total matching the filter (for pagination)
    int TotalPages);                      // Ceiling(TotalCount / PageSize)
```

### Behavioral Contract

| Condition | Expected Behavior |
|-----------|-------------------|
| `Filter` is null or all fields default | Return unfiltered paged results (backward compatible) |
| Multiple filter fields set | AND logic — row must satisfy ALL active filters |
| Text filter with empty/whitespace string | Treated as "no filter" for that column |
| Date filter value set, operator set | Apply comparison: `=` exact match, `>` after, `<` before |
| Date filter on nullable column (EmailDate) | Exclude rows where column is null |
| Numeric filter value set | Apply comparison operator against computed filing count |
| Status filter set to a ReportStatus value | Exact enum match |
| Any filter active | Page resets to 1; TotalCount reflects filtered count |

---

## Contract 2: Application Query Handler → Repository

The handler delegates database-level filtering to `IReportRepository.GetPagedAsync`.

### IReportRepository.GetPagedAsync

```csharp
Task<(IReadOnlyList<Report> Items, int TotalCount)> GetPagedAsync(
    ReportColumnFilter? filter,
    int skip,
    int take,
    bool sortDescending,
    CancellationToken ct = default);
```

### Behavioral Contract

| Condition | Expected Behavior |
|-----------|-------------------|
| `filter` null | Return all reports, paged |
| `NameContains` set | WHERE ReportName LIKE '%value%' (case-insensitive) |
| `ImportDateValue` set | Apply operator against Report.ImportDate |
| `EmailDateValue` set | Exclude nulls, then apply operator against Report.EmailDate |
| `StatusFilter` set | WHERE Status = value |
| `skip` + `take` | Applied AFTER filtering and sorting |
| Return value `.TotalCount` | Count of ALL rows matching the filter (not just the page) |

**Note**: `ImporterContains` and `FilingCountValue` filtering require data not on the `Report` entity. These are handled by the query handler:
- **ImporterContains**: Handler pre-resolves matching `ImporterId` values from `IImporterRepository` and passes them to the repository as an additional filter parameter (or the repository JOINs with the Importers table).
- **FilingCountValue**: Handler applies post-pagination filtering using `IFilingRepository.GetFilingCountByReportIdAsync`.

---

## Contract 3: ViewModel Reactive Pipeline

### Filter Property → LoadPage Flow

```
NameFilter (string)        ─┐
ImporterFilter (string)     ─┤─→ Throttle(300ms) ─→ ResetPageTo1() ─→ InvokeCommand(LoadPageCommand)
                             │
ImportDateOperator (enum)   ─┤
ImportDateFilter (DateOnly?) ─┤
EmailDateOperator (enum)    ─┤
EmailDateFilter (DateOnly?) ─┤─→ Skip(1) ─→ ResetPageTo1() ─→ InvokeCommand(LoadPageCommand)
FilingCountOperator (enum)  ─┤
FilingCountFilter (int?)    ─┤
StatusFilter (ReportStatus?)─┘
```

### HasActiveFilters (Computed)

```csharp
HasActiveFilters = WhenAnyValue(
    x => x.NameFilter,
    x => x.ImporterFilter,
    x => x.ImportDateFilter,
    x => x.EmailDateFilter,
    x => x.FilingCountFilter,
    x => x.StatusFilter)
    .Select(tuple => 
        !string.IsNullOrWhiteSpace(tuple.Item1) ||
        !string.IsNullOrWhiteSpace(tuple.Item2) ||
        tuple.Item3.HasValue ||
        tuple.Item4.HasValue ||
        tuple.Item5.HasValue ||
        tuple.Item6.HasValue);
```

### ClearFiltersCommand

```
Precondition: HasActiveFilters == true
Action: Set all filter properties to defaults (null/empty/Equals)
Postcondition: HasActiveFilters == false, page resets to 1, LoadPageCommand executes
```

---

## Contract 4: UI Filter Row ↔ ViewModel Binding

### Column-to-Control Mapping

| DataGrid Column | Filter Control | Binding Target | Binding Mode |
|-----------------|---------------|----------------|--------------|
| (Checkbox) | — (no filter) | — | — |
| Name | TextBox | `NameFilter` | TwoWay |
| Import Date | ComboBox + TextBox | `ImportDateOperator` + `ImportDateFilter` | TwoWay |
| Email Date | ComboBox + TextBox | `EmailDateOperator` + `EmailDateFilter` | TwoWay |
| Importer | TextBox | `ImporterFilter` | TwoWay |
| Status | ComboBox | `StatusFilter` | TwoWay |
| Filing Count | ComboBox + TextBox | `FilingCountOperator` + `FilingCountFilter` | TwoWay |
| Actions | — (no filter) | — | — |

### Operator ComboBox Items

```
Items: [ "=", ">", "<" ]
SelectedItem bound to: ComparisonOperator enum (via converter or Tag)
```

### Status ComboBox Items

```
Items: [ "All", "Init", "Processed", "Error", "Partial Error" ]
SelectedItem bound to: ReportStatus? (null for "All")
```

### Clear Filters Button

```
Location: Toolbar area (StackPanel), visible only when HasActiveFilters
Command: ClearFiltersCommand
```
