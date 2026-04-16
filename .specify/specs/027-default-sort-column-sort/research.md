# Research: Default Sort & Column Sort for Filings and Reports

**Feature**: 027-default-sort-column-sort  
**Date**: 2025-07-24

## R-001: Avalonia DataGrid Interactive Column Sorting

**Context**: The Filings DataGrid currently has `CanUserSortColumns="False"`. We need to enable interactive sorting and handle the `Sorting` event to perform server-side (query-layer) sorting rather than client-side sorting.

**Decision**: Use the Avalonia DataGrid's `Sorting` event with `e.Handled = true` to intercept column header clicks. Translate the column header into a sort parameter, and pass it through the query pipeline. Do NOT rely on DataGrid's built-in client-side sort — all ordering must happen in the EF Core query (repository layer) for correct multi-page behavior.

**Rationale**: Client-side sorting only reorders visible rows; for paginated data, this produces incorrect results. The spec (FR-005) mandates query-layer sorting. Avalonia DataGrid fires `DataGridColumnEventArgs` on the `Sorting` event, giving access to the `DataGridColumn.Header` and `SortDirection` which we can map to our `FilingSortColumn` enum.

**Alternatives Considered**:
- **Client-side sort via DataGrid built-in**: Rejected — only sorts the current page, violates FR-005.
- **Attached behavior**: Rejected as over-engineering — a code-behind event handler (pattern already used for `StatusComboBox_SelectionChanged` and `PaymentRef_LostFocus`) is consistent with the existing codebase.

## R-002: Sort Parameter Design for GetFilingsQuery

**Context**: `GetFilingsQuery` currently accepts `(Filter, Page, PageSize, ReportIdFilter)`. We need to add sort column and sort direction parameters.

**Decision**: Add two new parameters to the `GetFilingsQuery` record:
- `FilingSortColumn SortColumn = FilingSortColumn.FilingDeadline` — an enum in the Application layer (alongside `FilingFilterMode`)
- `bool SortDescending = true` — defaults to descending (latest first)

The `FilingSortColumn` enum values will be: `FilingDeadline`, `Status`, `IncomeType`, `PayingEntity`, `TaxPayable`, `PaymentReference`.

**Rationale**: Using a strongly-typed enum prevents arbitrary string-based column names from reaching the repository. Default values ensure backward compatibility — existing callers that don't pass sort parameters get the new "descending by deadline" default. The `bool SortDescending` is simpler than a `SortDirection` enum for a binary choice.

**Alternatives Considered**:
- **String-based column name**: Rejected — fragile, no compile-time safety, potential injection vector.
- **`SortDirection` enum instead of `bool`**: Considered but rejected for simplicity — a boolean is sufficient for two directions and matches the spec's `SortDescending` naming.
- **Separate `SortBy` query object**: Over-engineering for a single sort column + direction pair.

## R-003: Sort Parameter Design for GetReportsQuery

**Context**: `GetReportsQuery` is currently parameterless. Reports are always sorted by `ImportDate`. The spec (FR-008) requires a `SortDescending` parameter with a fixed sort key of `ImportDate`.

**Decision**: Add a single parameter to `GetReportsQuery`:
- `bool SortDescending = true` — defaults to descending (most recent first)

Sorting will be applied in the `GetReportsQueryHandler` after fetching all reports (since Reports are not paginated — the handler calls `_reports.GetAllAsync()` and enriches with importer names and filing counts). The repository's `GetAllAsync()` already sorts by `ImportDate DESC`; we will move the sort responsibility to the handler or add a parameter to `GetAllAsync` to keep the sort consistent regardless of how the repository returns data.

**Rationale**: Reports have no pagination, so sorting at the handler level after enrichment is acceptable. However, for consistency with the Filings approach and to keep the repository as the sorting authority, we will add a `bool sortDescending = true` parameter to `IReportRepository.GetAllAsync()`.

**Alternatives Considered**:
- **Sort only in ViewModel**: Rejected — violates FR-006 (sort must be consistent before results reach the view layer).
- **Sort in handler after enrichment with LINQ**: Viable but inconsistent — the handler currently preserves repository order. Adding a parameter to the repository keeps the single-responsibility principle.

## R-004: FilingRepository.GetPagedAsync Sort Implementation

**Context**: `GetPagedAsync` currently hardcodes `.OrderBy(f => f.FilingDeadline)`. It must accept dynamic sort column and direction.

**Decision**: Add `FilingSortColumn sortColumn` and `bool sortDescending` parameters to `IFilingRepository.GetPagedAsync()`. In the implementation, use a switch expression to map the `FilingSortColumn` enum to the corresponding EF Core `OrderBy`/`OrderByDescending` expression. Apply a secondary `.ThenBy(f => f.Id)` to ensure deterministic ordering when the primary sort column has duplicate values (spec edge case requirement).

**Rationale**: Switch expression on enum is exhaustive at compile time (with a `_ => throw` default), preventing missing column mappings. The secondary `ThenBy(Id)` prevents row-order flickering across loads (spec edge case).

**Alternatives Considered**:
- **Dynamic LINQ / Expression trees**: Over-engineering for 6 known columns.
- **Dictionary of `Expression<Func<Filing, object>>`**: Loses type safety on the sort key and may cause EF Core translation issues.

## R-005: ViewModel Sort State Management

**Context**: `FilingsViewModel` needs to track the current sort column and direction, expose them for DataGrid header binding, and implement the page-reset logic from FR-009/FR-010.

**Decision**: Add to `FilingsViewModel`:
- `FilingSortColumn _sortColumn = FilingSortColumn.FilingDeadline` backing field
- `bool _sortDescending = true` backing field
- `SortColumn` and `SortDescending` reactive properties
- `ApplySortCommand` (`ReactiveCommand<(string ColumnTag, bool? CurrentDirection), Unit>`) that:
  - Maps the column tag string to `FilingSortColumn` enum
  - If same column: toggles direction, does NOT reset page (FR-009)
  - If different column: sets new column + ascending, resets page to 1 (FR-010)
  - Calls `LoadPageCommand`

The `LoadPageAsync` method will include `_sortColumn` and `_sortDescending` when constructing the `GetFilingsQuery`.

**Rationale**: Separating the sort command from the load command keeps concerns clean. The column tag string mapping happens once in the ViewModel, keeping the View thin. Page-reset logic is clearly traceable to FR-009 and FR-010.

**Alternatives Considered**:
- **Passing the DataGrid column object to ViewModel**: Rejected — creates a UI framework dependency in the ViewModel, violating MVVM separation.
- **Using DataGrid's built-in SortMemberPath**: Would require client-side sorting; incompatible with query-layer sorting.

## R-006: Reports DataGrid Column Sorting Behavior

**Context**: ReportsView.axaml does not set `CanUserSortColumns`, which defaults to `True` in Avalonia. However, no ViewModel sort handling exists.

**Decision**: Reports already sort by `ImportDate DESC` by default (FR-002 satisfied). Since the spec does not require interactive column sorting for Reports (only a `SortDescending` parameter), and the Reports DataGrid has no pagination, we will:
1. Explicitly set `CanUserSortColumns="False"` on the Reports DataGrid to prevent confusing non-functional header clicks.
2. The `SortDescending` parameter on `GetReportsQuery` supports future extensibility but is not exposed to the UI in this feature since no user story requires toggling Reports sort direction via column click.

**Rationale**: The spec scopes interactive column sorting to Filings only (User Story 3). Reports get the correct default sort order (User Story 2) and the query-layer parameter (FR-008) for future use.

**Alternatives Considered**:
- **Enable interactive sorting on Reports too**: Not in spec scope. Would add complexity without user value.
- **Leave `CanUserSortColumns` unset (implicit True)**: Rejected — clicking headers would confuse users since nothing would happen.

## R-007: Avalonia DataGrid Sorting Event Integration Pattern

**Context**: Need to wire the DataGrid `Sorting` event to the ViewModel's sort command.

**Decision**: In `FilingsView.axaml.cs`, add a `DataGrid_Sorting` event handler that:
1. Sets `e.Handled = true` to prevent default client-side sort.
2. Extracts the column header tag (we'll use `Tag` property or `SortMemberPath` on each column to identify the sort column).
3. Calls `ViewModel?.ApplySortCommand.Execute(...)`.

Each sortable `DataGridTextColumn` in the AXAML will have a `Tag` set to the matching `FilingSortColumn` enum name (e.g., `Tag="FilingDeadline"`). Template columns (checkbox, status badge, action buttons) will not have a `Tag` and will be skipped.

**Rationale**: This follows the existing code-behind event handler pattern used for `StatusComboBox_SelectionChanged` and `PaymentRef_LostFocus`. The `Tag` property is a lightweight way to associate metadata without creating custom attached properties.

**Alternatives Considered**:
- **Binding `SortMemberPath` and using Avalonia's built-in sort**: Incompatible with server-side sorting.
- **Custom `ICollectionView` sort descriptors**: Not applicable — Avalonia DataGrid doesn't use WPF's ICollectionView pattern.
