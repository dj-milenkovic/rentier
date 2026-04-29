# Research: Reports Inline Column Filters

**Feature**: 047-reports-inline-column-filters  
**Date**: 2025-07-15

## Research 1: Server-Side vs Client-Side Filtering for Reports

**Decision**: Server-side filtering in the repository layer (EF Core → SQLite).

**Rationale**: The current `GetReportsQueryHandler` loads ALL reports, then paginates in memory (line 33: `_reports.GetAllAsync()`). With filtering, we must push both filtering AND pagination to the database to avoid loading the entire dataset. The handler currently also resolves importer names and filing counts per-report in a loop — this is a performance concern that filtering at the DB level will mitigate (fewer rows = fewer lookups).

**Alternatives Considered**:
- **Client-side filtering (ViewModel only)**: Rejected — breaks pagination. If page 1 shows 30 items and the user filters, client-side filtering would only filter the current page's 30 items, not the full dataset. The spec explicitly requires "server-side filtering to work with pagination" (FR-011, FR-013).
- **Hybrid (load all, filter in handler)**: Rejected — while the handler already loads all reports, this doesn't scale. Adding a filtered repository method is the correct investment and aligns with how `FilingRepository.GetPagedAsync` already works.

## Research 2: Reusable Filter Infrastructure vs Page-Specific

**Decision**: Create reusable `ComparisonOperator` enum in Application, but keep filter DTOs feature-specific. Share the enum across Reports and Filings features.

**Rationale**: The `ComparisonOperator` (Equals, GreaterThan, LessThan) is genuinely reusable across any page with operator-based filtering. However, each page's filter DTO (columns, types) differs enough that a generic "ColumnFilter" base class adds complexity without value. The Filings page (feature 045/046) currently uses a simple `FilingFilterMode` enum — when it gets full inline filters, it can reference the same `ComparisonOperator`.

**Alternatives Considered**:
- **Fully generic filter framework**: Rejected — over-engineering for two pages. Each page has different columns, types, and operator combinations. A shared framework would need type erasure or complex generics.
- **Completely independent per page**: Rejected for the operator enum — duplicating `>`, `<`, `=` semantics per page is wasteful.

## Research 3: Filter Row UI Pattern in Avalonia DataGrid

**Decision**: Use a frozen `DataGrid` row below headers implemented as a `StackPanel`/`Grid` overlay positioned between the column headers and the data rows. Each filter cell aligns with its column using shared column widths.

**Rationale**: Avalonia's `DataGrid` does not natively support filter rows (unlike WPF DevExpress or Telerik). The Filings page (feature 045) currently uses a toolbar-based filter approach (RadioButtons, chips), not an inline filter row. For the Reports page, the spec explicitly requires "a filter row directly below the DataGrid column headers" (FR-001). The most reliable approach is:
1. Place a `Grid` or `ItemsControl` above the `DataGrid` (visually below the toolbar) that mirrors the column layout.
2. Bind column widths to keep filter inputs aligned.
3. Each cell contains the appropriate control (TextBox, ComboBox, or operator+input pair).

**Alternatives Considered**:
- **DataGrid.FrozenRowCount / custom row template**: Rejected — Avalonia DataGrid doesn't support frozen content rows separate from data rows. Attempting to hack the first data row as a filter row creates binding and selection conflicts.
- **Custom DataGridColumn header templates**: Rejected — headers already contain column labels. Adding filter controls inside headers makes them too tall and visually cluttered. A separate row is cleaner and matches the spec's "below column headers" requirement.

## Research 4: Debounce Pattern with ReactiveUI

**Decision**: Use `WhenAnyValue(...).Throttle(TimeSpan.FromMilliseconds(300)).InvokeCommand(LoadPageCommand)` for text filter properties. Operator and dropdown changes invoke immediately (no debounce).

**Rationale**: ReactiveUI's `Throttle` (Rx `Throttle` = debounce semantics) is the idiomatic way to debounce in this stack. The existing codebase already uses `WhenAnyValue(...).Skip(1).InvokeCommand(...)` for sort changes — adding `.Throttle()` for text inputs is a natural extension. The 300ms delay matches FR-010 and standard desktop UX.

**Alternatives Considered**:
- **Custom timer-based debounce**: Rejected — reinvents what Rx provides natively.
- **Debounce on all filter types**: Rejected — operator dropdown and status dropdown changes are discrete (single-click), not continuous like typing. Debouncing them adds unnecessary latency.

## Research 5: GetReportsQueryHandler Refactoring for Filtered Paged Query

**Decision**: Add a new `GetPagedAsync` method to `IReportRepository` that accepts filter parameters, skip, take, sort, and returns `(IReadOnlyList<Report>, int totalCount)`. Refactor the handler to use this instead of `GetAllAsync` + in-memory pagination.

**Rationale**: The current handler loads ALL reports (`GetAllAsync`), then resolves importer names and filing counts in a per-row loop, then paginates in memory. This is the main performance bottleneck. The new approach:
1. Repository applies filters + pagination at the SQL level.
2. Handler still resolves importer names and filing counts for the (max 30) page results.
3. Total count comes from the filtered query (for pagination indicator).

This mirrors the pattern already established in `FilingRepository.GetPagedAsync` which returns `(IReadOnlyList<Filing>, int totalCount)`.

**Alternatives Considered**:
- **Keep GetAllAsync and filter in handler**: Rejected — loads all rows for every filter change. Acceptable for <100 reports, but the spec targets 1,000+ reports (SC-002).
- **Include importer names and filing counts in the repository query (JOIN)**: Considered but deferred — would require a projection query/view. The current per-row lookup pattern works for ≤30 rows per page and avoids complex JOIN logic in the repository.

## Research 6: Handling Nullable Date Columns (EmailDate) in Filters

**Decision**: When a date filter is active on a nullable column (EmailDate), exclude rows where the column is null. When no filter is active, include all rows.

**Rationale**: The spec explicitly states "Reports with null values in that column are excluded from comparison-based filters" (FR-013, edge case). This is intuitive: if the user says "show me reports with Email Date > 2024-06-01", reports with no email date cannot satisfy that condition.

**Alternatives Considered**:
- **Treat null as minimum date**: Rejected — confusing UX. A report with no email date is not "before all dates."
- **Show null rows with a warning**: Rejected — overcomplicates the UI for minimal benefit.
