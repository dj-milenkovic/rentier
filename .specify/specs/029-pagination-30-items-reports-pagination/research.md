# Research: Pagination — 30 Items per Page & Reports Pagination

**Feature**: 029-pagination-30-items-reports-pagination  
**Date**: 2025-07-16

## R-001: Page Size Constant Strategy

**Decision**: Change the hardcoded `20` in `GetFilingsQuery` default and `FilingsViewModel.LoadPageAsync` to `30`. No shared constant class is needed.

**Rationale**: The page size is declared as a default parameter value in the query record (`GetFilingsQuery(... int PageSize = 20)`). The FilingsViewModel passes `20` explicitly at the call site. Both values must change to `30`. Introducing a shared constant (e.g., `PaginationDefaults.PageSize`) was considered but adds indirection for a value referenced in exactly two locations. The query default alone serves as the source of truth for the Application layer, and the ViewModel call site can pass it explicitly for clarity.

**Alternatives considered**:
- Shared `const int DefaultPageSize = 30` in a `PaginationDefaults` static class — rejected because two call sites don't warrant the indirection, and the query record default already documents the value.
- Configuration-based page size — rejected because the spec treats page size as a fixed product decision, not a user preference.

## R-002: Reports Pagination — In-Memory Slicing vs Repository Method

**Decision**: Perform in-memory pagination in the `GetReportsQueryHandler` by loading all reports, building the full DTO list, then slicing with `Skip`/`Take`. No new repository method is required.

**Rationale**: The existing handler already loads all reports via `_reports.GetAllAsync()` and then performs N+1 filing count/date lookups per report. The spec explicitly assumes "the total number of reports is expected to remain small enough that loading all reports into memory and slicing in the handler is acceptable." Adding a `GetPagedAsync` to `IReportRepository` would not eliminate the N+1 calls (filing count and earliest date are fetched per-report from `IFilingRepository`) and would complicate the query without benefit. In-memory slicing after the full list is built mirrors the spec's intent and keeps the change scope minimal.

**Alternatives considered**:
- `IReportRepository.GetPagedAsync(skip, take)` — rejected because the handler still needs filing counts for *every* report to compute TotalCount and paginate. The N+1 problem is out of scope for this feature.
- Batch repository method returning counts and dates per report — rejected as over-engineering for the current dataset size and outside feature scope.

## R-003: Localisation Strategy for Reports Pagination Strings

**Decision**: Create new `Reports_Page_Previous`, `Reports_Page_Next`, and `Reports_Page_Indicator` entries in `Strings.resx`, using the same values as the existing Filings strings (`← Previous`, `Next →`, `Page {0} of {1}`).

**Rationale**: The spec requires all new user-facing strings to be externalised (FR-018) and follow the naming convention of Filings pagination strings. Using separate `Reports_Page_*` keys (rather than reusing `Filings_Page_*` directly) follows the page-specific naming convention already established in the resource file (e.g., `Reports_Col_Name`, `Reports_Button_Import`, etc.). This allows future divergence if the Reports page needs different labels.

**Alternatives considered**:
- Reuse `Filings_Page_*` strings directly — rejected because it breaks the `{Page}_` naming convention and would create an implicit cross-page coupling in the resource file.
- Generalise to `Common_Page_*` — rejected because no `Common_` prefix convention exists in the project, and introducing one for three strings is over-engineering.

## R-004: Handler Return Type for Paginated Reports

**Decision**: Create a new `ReportsPageResult` record mirroring `FilingsPageResult`, containing `Rows`, `TotalCount`, and `TotalPages`. Change the handler return type from `Result<IReadOnlyList<ReportRowDto>, Error>` to `Result<ReportsPageResult, Error>`.

**Rationale**: The spec explicitly calls for a `ReportsPageResult` that "mirrors the shape of the existing `FilingsPageResult`" (Assumptions section). Using the same structural pattern enables consistent pagination handling in ViewModels and simplifies the ViewModel's data-binding logic. The handler's return type must change since it now returns pagination metadata alongside the row data.

**Alternatives considered**:
- Generic `PagedResult<T>` shared by both Filings and Reports — considered viable long-term but rejected for this feature to minimize blast radius. The Filings handler would need refactoring to use the generic type, which is out of scope.
- Return a tuple `(IReadOnlyList<ReportRowDto>, int TotalCount, int TotalPages)` — rejected because records provide clear intent, named fields, and are the established pattern.

## R-005: Page Reset Wiring for Future Sort/Filter Controls

**Decision**: Add reactive `SortDirection` (and optionally `Filter`) properties to `ReportsViewModel` that, when changed, reset `_currentPage = 1` and trigger a reload — mirroring the `ShowAll` setter pattern in `FilingsViewModel`.

**Rationale**: The spec (FR-016, FR-017) requires page reset on sort/filter changes. The Reports page currently has no sort or filter controls, but the spec establishes the reset mechanism proactively. The `FilingsViewModel.ShowAll` setter already implements this pattern: set the backing field, reset page to 1, raise property changed, execute LoadPageCommand. Wiring this into the ViewModel now means future features adding sort/filter controls only need to bind the new UI controls — the reactive pipeline is ready.

**Alternatives considered**:
- Defer all wiring until sort/filter controls are added — rejected because the spec explicitly requires the mechanism to be in place now (FR-016, FR-017), and the FilingsViewModel pattern proves the approach works.
- Use a central `PaginationState` object — rejected as over-engineering for the current two-page application.

## R-006: Delete and Bulk Delete Page Adjustment

**Decision**: Mirror the `FilingsViewModel` delete/bulk-delete page-decrement logic in `ReportsViewModel`. When the last item(s) on a non-first page are deleted, decrement `_currentPage` before reloading.

**Rationale**: The spec edge cases require: "When the last item on the current page is deleted, the system navigates to the previous page." The `FilingsViewModel.DeleteCommand` already implements this with `if (Rows.Count == 1 && _currentPage > 1) _currentPage--`. The same pattern applies to `BulkDeleteCommand` which checks `if (selectedIds.Count == Rows.Count && _currentPage > 1) _currentPage--`. Replicating these guards in `ReportsViewModel` provides consistent behaviour.

**Alternatives considered**:
- Let the handler return empty page and let the ViewModel clamp via `Math.Min` — this would show an empty page briefly before clamping, creating a flicker. Explicit decrement avoids the extra reload.
