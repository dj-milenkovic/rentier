# Quickstart: Reports Filter Header Flyouts

**Feature**: 051-reports-filter-header-flyouts

## What This Feature Does

Replaces the inline filter row above the Reports DataGrid with Excel-style flyout popups in column headers. Each filterable column gets a funnel icon; clicking it opens a small popup with filter controls. Text columns get a search box, the Status column gets a checkbox list, and date/numeric columns get text inputs.

## Key Files to Modify

### Desktop Layer (View + ViewModel)

| File | Change |
|------|--------|
| `src/Rentier.Desktop/Views/ReportsView.axaml` | Remove filter row (lines 86-164). Convert remaining `DataGridTextColumn` to `DataGridTemplateColumn` with custom headers containing funnel icon + flyout. |
| `src/Rentier.Desktop/ViewModels/ReportsViewModel.cs` | Remove operator properties. Change date filters from `DateOnly?` to `string?`. Replace single `StatusFilter` with multi-select. Add `ApplyFilterCommand`. Update `BuildFilter()`, `ClearFiltersCommand`, `HasActiveFilters`, and WhenActivated subscriptions. |
| `src/Rentier.Desktop/ViewModels/StatusCheckboxItem.cs` | **NEW** — Checkbox item for status multi-select flyout. |
| `src/Rentier.Desktop/Resources/Strings.resx` | Add new resource keys for flyout UI labels. |

### Application Layer

| File | Change |
|------|--------|
| `src/Rentier.Application/DTOs/ReportColumnFilter.cs` | Replace date operator+value pairs with `string? ImportDateContains` / `EmailDateContains`. Replace `StatusFilter` with `StatusFilters` (set). Remove `FilingCountOperator`. |

### Infrastructure Layer

| File | Change |
|------|--------|
| `src/Rentier.Infrastructure/Repositories/ReportRepository.cs` | Update `GetPagedAsync` to handle `ImportDateContains`, `EmailDateContains` (string LIKE), and `StatusFilters` (IN query). Remove `ApplyImportDateFilter`/`ApplyEmailDateFilter` helpers. |

### Application Handler

| File | Change |
|------|--------|
| `src/Rentier.Application/Handlers/GetReportsQueryHandler.cs` | Update filing count post-filter to use equals only (remove operator switch). |

### Tests

| File | Change |
|------|--------|
| `tests/Rentier.UnitTests/Desktop/ReportsViewModelTests.cs` | Update filter tests for new flyout-driven apply flow, multi-select status, text date filters. |
| `tests/Rentier.UnitTests/Desktop/ReportsViewHeadlessTests.cs` | Update headless view tests if filter row removal affects rendering. |

## Build & Run

```bash
# Build
dotnet build Rentier.slnx

# Run tests
dotnet test Rentier.slnx

# Run app
dotnet run --project src/Rentier.Desktop
```

## Implementation Order

1. **Application DTO** — Modify `ReportColumnFilter` (breaking change, update all callers)
2. **Infrastructure** — Update `ReportRepository.GetPagedAsync` for new filter shape
3. **Application Handler** — Simplify filing count filter in `GetReportsQueryHandler`
4. **Desktop ViewModel** — Rewrite filter properties, add `StatusCheckboxItem`, update `BuildFilter()`
5. **Desktop View** — Remove filter row, convert columns to template columns with flyout headers
6. **Resources** — Add new string keys
7. **Tests** — Update all affected test files
