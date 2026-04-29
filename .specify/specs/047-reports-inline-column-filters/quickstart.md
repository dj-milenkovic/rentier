# Quickstart: Reports Inline Column Filters

**Feature**: 047-reports-inline-column-filters  
**Date**: 2025-07-15

## Implementation Order

Work bottom-up through the architecture layers to ensure each layer is testable independently.

### Step 1: Application Layer — New Types

1. **Create `ComparisonOperator` enum** at `src/Rentier.Application/Enums/ComparisonOperator.cs`
   - Three values: `Equals = 0`, `GreaterThan = 1`, `LessThan = 2`

2. **Create `ReportColumnFilter` record** at `src/Rentier.Application/DTOs/ReportColumnFilter.cs`
   - All-optional fields with sensible defaults (see data-model.md)

3. **Modify `GetReportsQuery`** at `src/Rentier.Application/Queries/GetReportsQuery.cs`
   - Add `ReportColumnFilter? Filter = null` parameter

### Step 2: Repository Contract + Implementation

4. **Add `GetPagedAsync` to `IReportRepository`** at `src/Rentier.Application/Repositories/IReportRepository.cs`
   - Signature: `Task<(IReadOnlyList<Report> Items, int TotalCount)> GetPagedAsync(ReportColumnFilter? filter, int skip, int take, bool sortDescending, CancellationToken ct = default)`

5. **Implement `GetPagedAsync` in `ReportRepository`** at `src/Rentier.Infrastructure/Repositories/ReportRepository.cs`
   - Build `IQueryable<Report>` with conditional `.Where()` clauses
   - Handle nullable EmailDate (exclude nulls when filter active)
   - Apply existing sort order logic
   - Return `(items, totalCount)`

6. **Write integration tests** for `ReportRepository.GetPagedAsync`
   - Test each filter type independently
   - Test combined filters (AND logic)
   - Test pagination with filters
   - Test nullable EmailDate exclusion

### Step 3: Query Handler Refactoring

7. **Refactor `GetReportsQueryHandler`** at `src/Rentier.Application/Handlers/GetReportsQueryHandler.cs`
   - Replace `GetAllAsync` + in-memory pagination with `GetPagedAsync`
   - Pre-resolve `ImporterContains` to matching ImporterIds if filter is set
   - Post-filter by `FilingCountValue` if set (after resolving filing counts for paged results)
   - Maintain existing importer name / filing count resolution for display

8. **Write/update handler unit tests**
   - Verify filter is forwarded to repository
   - Verify ImporterContains pre-resolution flow
   - Verify FilingCount post-filtering
   - Verify backward compatibility (null filter = existing behavior)

### Step 4: ViewModel Filter State

9. **Add filter properties to `ReportsViewModel`**
   - `NameFilter`, `ImporterFilter` (string, debounced)
   - `ImportDateOperator`, `ImportDateFilter`, `EmailDateOperator`, `EmailDateFilter` (operator + DateOnly?)
   - `FilingCountOperator`, `FilingCountFilter` (operator + int?)
   - `StatusFilter` (ReportStatus?)
   - `HasActiveFilters` (OAPH, computed)
   - `ClearFiltersCommand`

10. **Wire reactive pipeline in `WhenActivated`**
    - Text filters: `WhenAnyValue → Throttle(300ms) → reset page → InvokeCommand(LoadPageCommand)`
    - Other filters: `WhenAnyValue → Skip(1) → reset page → InvokeCommand(LoadPageCommand)`

11. **Modify `LoadPageAsync`** to construct `ReportColumnFilter` from ViewModel state and pass to query

12. **Write ViewModel unit tests**
    - Filter property change triggers reload (with TestScheduler for debounce)
    - ClearFilters resets all properties
    - HasActiveFilters computation
    - Page resets to 1 on filter change
    - Invalid numeric input handling

### Step 5: UI Filter Row

13. **Add filter row to `ReportsView.axaml`**
    - Grid-based row between toolbar and DataGrid (or above DataGrid within the DockPanel)
    - Column widths aligned with DataGrid columns
    - TextBox for Name, Importer (with Watermark "Filter...")
    - ComboBox + TextBox pairs for Import Date, Email Date, Filing Count
    - ComboBox for Status
    - Empty cells for Checkbox and Actions columns

14. **Add "Clear filters" button** to the toolbar area
    - Bound to `ClearFiltersCommand`
    - Visible only when `HasActiveFilters` is true

15. **Add localized strings** to `Resources/Strings.resx`
    - Filter placeholders, operator labels, "Clear filters" button text, "All" dropdown option

## Key Files to Modify

| File | Change Type | Layer |
|------|------------|-------|
| `src/Rentier.Application/Enums/ComparisonOperator.cs` | NEW | Application |
| `src/Rentier.Application/DTOs/ReportColumnFilter.cs` | NEW | Application |
| `src/Rentier.Application/Queries/GetReportsQuery.cs` | MODIFY | Application |
| `src/Rentier.Application/Repositories/IReportRepository.cs` | MODIFY | Application |
| `src/Rentier.Application/Handlers/GetReportsQueryHandler.cs` | MODIFY | Application |
| `src/Rentier.Infrastructure/Repositories/ReportRepository.cs` | MODIFY | Infrastructure |
| `src/Rentier.Desktop/ViewModels/ReportsViewModel.cs` | MODIFY | Desktop |
| `src/Rentier.Desktop/Views/ReportsView.axaml` | MODIFY | Desktop |
| `src/Rentier.Desktop/Resources/Strings.resx` | MODIFY | Desktop |
| `tests/Rentier.UnitTests/Application/GetReportsQueryHandlerTests.cs` | MODIFY | Tests |
| `tests/Rentier.UnitTests/Desktop/ReportsViewModelTests.cs` | MODIFY | Tests |
| `tests/Rentier.Infrastructure.Tests/ReportRepositoryTests.cs` | MODIFY | Tests |

## Risks and Mitigations

| Risk | Mitigation |
|------|-----------|
| ImporterContains requires cross-table resolution | Pre-resolve matching ImporterIds in handler; pass as set to repository |
| FilingCount is computed, not a DB column | Post-filter in handler after pagination; over-fetch slightly if needed |
| Filter row column alignment with DataGrid | Use shared column width definitions or bind to DataGrid column ActualWidth |
| EF Core SQLite case-insensitive LIKE | SQLite `LIKE` is case-insensitive by default for ASCII; verify with non-ASCII test data |
| Debounce timing in tests | Use `TestScheduler.AdvanceBy(300ms)` in ViewModel tests |
