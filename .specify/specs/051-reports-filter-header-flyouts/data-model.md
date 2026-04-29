# Data Model: Reports Filter Header Flyouts

**Feature**: 051-reports-filter-header-flyouts
**Date**: 2025-07-15

## Entities

### ReportColumnFilter (Application DTO — MODIFIED)

**File**: `src/Rentier.Application/DTOs/ReportColumnFilter.cs`

```csharp
public sealed record ReportColumnFilter(
    string? NameContains = null,
    string? ImporterContains = null,
    IReadOnlyList<Guid>? ImporterIds = null,
    string? ImportDateContains = null,       // NEW — replaces ImportDateOperator + ImportDateValue
    string? EmailDateContains = null,        // NEW — replaces EmailDateOperator + EmailDateValue
    int? FilingCountValue = null,            // KEPT — operator removed, always equals
    IReadOnlySet<ReportStatus>? StatusFilters = null);  // NEW — replaces StatusFilter (multi-select)
```

**Removed fields**:
- `ComparisonOperator ImportDateOperator` — no longer needed (text-contains)
- `DateOnly? ImportDateValue` — replaced by `ImportDateContains`
- `ComparisonOperator EmailDateOperator` — no longer needed (text-contains)
- `DateOnly? EmailDateValue` — replaced by `EmailDateContains`
- `ComparisonOperator FilingCountOperator` — always equals
- `ReportStatus? StatusFilter` — replaced by multi-select `StatusFilters`

### StatusCheckboxItem (Desktop ViewModel — NEW)

**File**: `src/Rentier.Desktop/ViewModels/StatusCheckboxItem.cs`

```csharp
public sealed class StatusCheckboxItem : ReactiveObject
{
    public ReportStatus Status { get; }
    public string DisplayName { get; }
    
    private bool _isChecked = true;
    public bool IsChecked
    {
        get => _isChecked;
        set => this.RaiseAndSetIfChanged(ref _isChecked, value);
    }
}
```

**Validation rules**: None — simple UI selection model.

### ReportsViewModel Filter Properties (Desktop — MODIFIED)

**Removed properties**:
- `ComparisonOperator ImportDateOperator`
- `DateOnly? ImportDateFilter`
- `ComparisonOperator EmailDateOperator`
- `DateOnly? EmailDateFilter`
- `ComparisonOperator FilingCountOperator`
- `int? FilingCountFilter` (renamed)
- `ReportStatus? StatusFilter` (replaced)
- `IReadOnlyList<ReportStatus?> StatusFilterOptions` (replaced)

**New/modified filter properties** (staged in flyout, applied on "Apply"):
- `string? NameFilter` — KEPT, now applied via flyout Apply button
- `string? ImporterFilter` — KEPT, now applied via flyout Apply button
- `string? ImportDateFilter` — NEW type (was DateOnly?, now string for text search)
- `string? EmailDateFilter` — NEW type (was DateOnly?, now string for text search)
- `string? FilingCountFilterText` — NEW (string input, parsed to int? internally)
- `ObservableCollection<StatusCheckboxItem> StatusCheckboxItems` — NEW (checkbox list)
- `IReadOnlySet<ReportStatus>? ActiveStatusFilters` — computed from checked items

**HasActiveFilters** — recomputed from: any non-empty text filter OR any unchecked status item.

### ReportRepository.GetPagedAsync (Infrastructure — MODIFIED)

**Changes to filter application**:
- `ImportDateContains` → `WHERE strftime('%Y-%m-%d', ImportDate) LIKE '%value%'` or EF string conversion
- `EmailDateContains` → same pattern for EmailDate
- `StatusFilters` → `WHERE Status IN (...)` instead of single equality
- Remove `ApplyImportDateFilter` and `ApplyEmailDateFilter` helper methods
- `FilingCountValue` still post-filtered in handler (computed field)

## Relationships

```
ReportsViewModel 1──* StatusCheckboxItem (checkbox list for Status flyout)
ReportsViewModel ──> ReportColumnFilter (built in BuildFilter(), passed to query)
GetReportsQueryHandler ──> ReportRepository.GetPagedAsync (server-side filtering)
```

## State Transitions

No entity state transitions. The flyout open/close is purely UI state:

```
Flyout Closed → [click funnel icon] → Flyout Open (staged values = current filter values)
Flyout Open → [click Apply] → Flyout Closed (staged → committed, query triggered)
Flyout Open → [click outside / dismiss] → Flyout Closed (staged values discarded)
```
