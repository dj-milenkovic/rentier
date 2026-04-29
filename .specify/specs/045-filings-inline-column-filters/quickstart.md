# Quickstart: Filings Inline Column Filters

**Feature**: 045-filings-inline-column-filters

## What This Feature Does

Adds a row of inline filter controls below the column headers on the Filings DataGrid page. Users can filter filings by Status (dropdown), Income Type (dropdown), Payer name (text search), Filing Deadline (date picker), and Payment Reference (text search). Filters combine with AND logic and apply immediately. A "Clear filters" button resets all filters at once.

## Key Design Decisions

1. **Server-side filtering** — Filters are passed as query parameters to the database, not applied client-side, because the Filings page is paginated.
2. **Mutual exclusivity with ReportIdFilter** — When navigating from Reports page, inline filters are cleared to guarantee target filings are visible.
3. **Debounced text inputs** — 300ms throttle on text filter changes to avoid excessive DB queries during typing.
4. **No domain changes** — This is a pure presentation + query layer feature.

## Files to Change

### Application Layer (`src/Rentier.Application/`)

| File | Change |
|------|--------|
| `Queries/FilingColumnFilter.cs` | **NEW** — Record carrying per-column filter values |
| `Queries/GetFilingsQuery.cs` | Add `FilingColumnFilter? ColumnFilter` parameter |
| `Handlers/GetFilingsQueryHandler.cs` | Pass `ColumnFilter` to repository call |
| `Repositories/IFilingRepository.cs` | Add `FilingColumnFilter? columnFilter` to `GetPagedAsync` |

### Infrastructure Layer (`src/Rentier.Infrastructure/`)

| File | Change |
|------|--------|
| `Repositories/FilingRepository.cs` | Apply WHERE clauses from `FilingColumnFilter` in `GetPagedAsync` |

### Desktop Layer (`src/Rentier.Desktop/`)

| File | Change |
|------|--------|
| `ViewModels/FilingsViewModel.cs` | Add filter properties, reactive pipeline, ClearFilters command, filter↔ReportId interaction |
| `Views/FilingsView.axaml` | Add filter row panel with ComboBoxes, TextBoxes, CalendarDatePicker below DataGrid headers |
| `Resources/Strings.resx` | Add localization keys for filter placeholders, clear button, empty state |

### Tests

| File | Change |
|------|--------|
| `tests/Rentier.Application.Tests/Handlers/GetFilingsQueryHandlerTests.cs` | Test ColumnFilter parameter forwarding |
| `tests/Rentier.Infrastructure.Tests/Repositories/FilingRepositoryTests.cs` | Test filtered queries with various column filter combinations |
| `tests/Rentier.Desktop.Tests/ViewModels/FilingsViewModelTests.cs` | Test filter state, debounce, clear, ReportIdFilter interaction |

## Build & Test

```bash
# Build
dotnet build Rentier.slnx

# Run all tests
dotnet test Rentier.slnx

# Run only affected test projects
dotnet test tests/Rentier.Application.Tests
dotnet test tests/Rentier.Infrastructure.Tests
dotnet test tests/Rentier.Desktop.Tests
```

## Architecture Compliance Notes

- **Clean Architecture**: Filter record lives in Application (query parameter). ViewModel owns filter state. Repository implements the query. No layer violations.
- **Decimal rule**: No monetary fields are filtered; `TaxPayableRsd` is display-only.
- **DateOnly rule**: `FilingDeadline` filter uses `DateOnly` in query/repository. ViewModel converts from `DateTimeOffset?` (Avalonia control) at boundary.
- **Async rule**: `LoadPageAsync` is already async; filter changes trigger it reactively.
