# Data Model: Pagination — 30 Items per Page & Reports Pagination

**Feature**: 029-pagination-30-items-reports-pagination  
**Date**: 2025-07-16

## Domain Entities

No domain entity changes. The `Report` and `Filing` entities are unchanged. Pagination is applied at the Application (query/handler) and Desktop (ViewModel/View) layers.

## Application Layer DTOs

### ReportsPageResult (NEW)

| Field | Type | Description |
|-------|------|-------------|
| `Rows` | `IReadOnlyList<ReportRowDto>` | Report DTOs for the current page |
| `TotalCount` | `int` | Total number of reports matching current criteria |
| `TotalPages` | `int` | Calculated total pages (`ceil(TotalCount / PageSize)`, minimum 1) |

**Location**: `src/Rentier.Application/DTOs/ReportsPageResult.cs`  
**Pattern**: Mirrors `FilingsPageResult` exactly.

```csharp
public sealed record ReportsPageResult(
    IReadOnlyList<ReportRowDto> Rows,
    int TotalCount,
    int TotalPages);
```

### GetFilingsQuery (MODIFIED)

| Field | Type | Change |
|-------|------|--------|
| `PageSize` | `int` | Default value changes from `20` → `30` |

All other fields unchanged.

### GetReportsQuery (MODIFIED)

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `Page` | `int` | `1` | Page number (1-based) |
| `PageSize` | `int` | `30` | Items per page |

**Before**: `public sealed record GetReportsQuery;` (no parameters)  
**After**: `public sealed record GetReportsQuery(int Page = 1, int PageSize = 30);`

## Handler Changes

### GetReportsQueryHandler (MODIFIED)

**Return type change**: `Result<IReadOnlyList<ReportRowDto>, Error>` → `Result<ReportsPageResult, Error>`

**New logic** (inserted after DTO list is built):
1. Validate `Page >= 1` and `1 <= PageSize <= 100`
2. Compute `totalCount = dtos.Count`
3. Compute `totalPages = Math.Max(1, (int)Math.Ceiling((double)totalCount / query.PageSize))`
4. Slice: `dtos.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)`
5. Return `ReportsPageResult(slicedRows, totalCount, totalPages)`

### GetFilingsQueryHandler (UNCHANGED)

No code changes. The handler already accepts `PageSize` as a parameter; the default is set in `GetFilingsQuery`.

## ViewModel State

### ReportsViewModel — New Properties

| Property | Type | Source | Description |
|----------|------|--------|-------------|
| `CurrentPage` | `int` | Backing field (`_currentPage = 1`) | Current page number |
| `TotalPages` | `int` | Backing field (`_totalPages = 1`) | Total pages from query result |
| `TotalCount` | `int` | Backing field (`_totalCount`) | Total item count |
| `HasPreviousPage` | `bool` | Computed: `_currentPage > 1 && !IsLoading` | Previous button enabled state |
| `HasNextPage` | `bool` | Computed: `_currentPage < _totalPages && !IsLoading` | Next button enabled state |
| `PageIndicator` | `string` | Computed: `string.Format(Strings.Reports_Page_Indicator, _currentPage, _totalPages)` | Display text |

### ReportsViewModel — New Commands

| Command | Type | Behaviour |
|---------|------|-----------|
| `PreviousPageCommand` | `ReactiveCommand<Unit, Unit>` | Decrement page, reload. Enabled when `HasPreviousPage`. |
| `NextPageCommand` | `ReactiveCommand<Unit, Unit>` | Increment page, reload. Enabled when `HasNextPage`. |

### ReportsViewModel — Modified Method

| Method | Change |
|--------|--------|
| `LoadReportsAsync` | Renamed to `LoadPageAsync`. Passes `_currentPage` and `30` to `GetReportsQuery`. Updates `TotalCount`, `TotalPages`. Clamps `_currentPage`. Raises pagination property changes. |

### FilingsViewModel — Modified Method

| Method | Change |
|--------|--------|
| `LoadPageAsync` | Page size parameter changes from `20` to `30` in `GetFilingsQuery` constructor call |

## Localisation Resources

### New Entries in `Strings.resx`

| Key | Value | Notes |
|-----|-------|-------|
| `Reports_Page_Previous` | `← Previous` | Same text as `Filings_Page_Previous` |
| `Reports_Page_Next` | `Next →` | Same text as `Filings_Page_Next` |
| `Reports_Page_Indicator` | `Page {0} of {1}` | Same format as `Filings_Page_Indicator` |

## State Transitions

### Page Navigation State Machine (ReportsViewModel)

```text
[Page N loaded]
    ─── NextPageCommand ──→ [Page N+1 loading] ──→ [Page N+1 loaded]
    ─── PreviousPageCommand ──→ [Page N-1 loading] ──→ [Page N-1 loaded]
    ─── Delete last item on page > 1 ──→ [Page N-1 loading] ──→ [Page N-1 loaded]
    ─── Sort/Filter change ──→ [Page 1 loading] ──→ [Page 1 loaded]

Invariants:
    CurrentPage ∈ [1, TotalPages]
    HasPreviousPage = (CurrentPage > 1) ∧ ¬IsLoading
    HasNextPage = (CurrentPage < TotalPages) ∧ ¬IsLoading
    TotalPages = max(1, ⌈TotalCount / PageSize⌉)
```

## Relationships

```text
GetReportsQuery(Page, PageSize)
    │
    ▼
GetReportsQueryHandler
    │ uses: IReportRepository.GetAllAsync()
    │ uses: IFilingRepository.GetFilingCountByReportIdAsync()
    │ uses: IFilingRepository.GetEarliestIncomeDateByReportIdAsync()
    │ uses: IImporterRepository.GetAllAsync()
    │ builds: List<ReportRowDto>
    │ slices: Skip/Take on full list
    │
    ▼
ReportsPageResult(Rows, TotalCount, TotalPages)
    │
    ▼
ReportsViewModel
    │ binds: CurrentPage, TotalPages, HasPreviousPage, HasNextPage, PageIndicator
    │ commands: PreviousPageCommand, NextPageCommand
    │
    ▼
ReportsView.axaml
    │ renders: Pagination bar (Previous | Page X of Y | Next)
```
