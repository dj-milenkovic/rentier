# Feature Specification: Default Sort & Column Sort for Filings and Reports

**Feature Branch**: `027-default-sort-column-sort`  
**Created**: 2025-07-24  
**Status**: Draft  
**Input**: User description: "Both tables default to descending order by the most relevant date column (filing deadline for Filings, import date for Reports) so the newest entries appear at the top without any interaction. The Filings DataGrid additionally gains interactive column sorting for all sortable columns (Reports already supports this). Sort direction is applied in the query layer so it is consistent across all pages."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Default Filings Sort Order (Priority: P1)

As a user opening the Filings page, I see the most urgent (latest) filing deadlines at the top of the table without clicking any column header. This lets me immediately focus on upcoming obligations.

**Why this priority**: This is the core value proposition — users should never need to manually sort to see their most relevant filings. Today the table sorts by FilingDeadline ascending, which buries the newest deadlines at the bottom.

**Independent Test**: Can be fully tested by opening the Filings page with multiple filings that have different deadlines and verifying the row order starts from the latest deadline downward.

**Acceptance Scenarios**:

1. **Given** the Filings page has not been visited yet, **When** the user navigates to Filings for the first time, **Then** the table rows are ordered by FilingDeadline descending (latest deadline first).
2. **Given** the Filings page has multiple filings spanning different months, **When** the page loads, **Then** the filing with the furthest-future deadline appears in the first row.
3. **Given** the user applies a filter (e.g., "Show All" vs "Unpaid"), **When** the filter changes, **Then** the default descending sort by FilingDeadline is preserved in the filtered results.

---

### User Story 2 - Default Reports Sort Order (Priority: P1)

As a user opening the Reports page, I see the most recently imported reports at the top. This lets me quickly find the report I just imported or synced.

**Why this priority**: Equal priority with Filings default sort — both tables must present a sensible default order for an intuitive first-load experience.

**Independent Test**: Can be fully tested by navigating to the Reports page with several reports imported on different dates and verifying the newest import appears first.

**Acceptance Scenarios**:

1. **Given** the Reports page has not been visited yet, **When** the user navigates to Reports, **Then** the table rows are ordered by ImportDate descending (most recently imported first).
2. **Given** a new report is imported and the user returns to the Reports page, **When** the page loads, **Then** the newly imported report appears at the top.

---

### User Story 3 - Interactive Column Sorting on Filings (Priority: P2)

As a user reviewing the Filings table, I can click any sortable column header to re-sort the table by that column. Clicking the same header again reverses the direction. This gives me flexible control over how I view my filings.

**Why this priority**: Adds interactive flexibility on top of the sensible default order. Users who want to sort by tax amount, paying entity, or status can do so without leaving the page.

**Independent Test**: Can be fully tested by clicking a column header (e.g., TaxPayable) and verifying the rows reorder accordingly, then clicking again to verify the direction reverses.

**Acceptance Scenarios**:

1. **Given** the Filings DataGrid is displaying rows sorted by FilingDeadline descending (default), **When** the user clicks the "Deadline" column header, **Then** the sort direction toggles to ascending (earliest deadline first).
2. **Given** the Filings DataGrid is sorted by Deadline ascending, **When** the user clicks the "Deadline" column header again, **Then** the sort direction toggles back to descending (latest deadline first).
3. **Given** the Filings DataGrid is sorted by Deadline, **When** the user clicks a different sortable column header (e.g., "Tax Payable"), **Then** the table re-sorts by the newly clicked column in ascending order.
4. **Given** the user has sorted by a column, **When** the user navigates to the next page, **Then** the same sort column and direction are applied to the next page's results.

---

### User Story 4 - Sort State Persists Across Pagination (Priority: P2)

As a user browsing a multi-page Filings table with a custom sort applied, the sort column and direction remain stable when I navigate between pages. The current page does not reset when only the sort direction changes.

**Why this priority**: Without sort persistence, changing pages would lose the user's chosen order, making multi-page workflows frustrating.

**Independent Test**: Can be fully tested by sorting by a non-default column, navigating to page 2, and verifying the same sort is applied. Then toggling sort direction on page 2 and verifying the page number does not reset to 1.

**Acceptance Scenarios**:

1. **Given** the user is on page 2 of Filings sorted by TaxPayable ascending, **When** the user navigates to page 3, **Then** TaxPayable ascending sort is still applied.
2. **Given** the user is on page 2, **When** the user clicks a column header to change sort direction, **Then** the page remains on page 2 (does not reset to page 1) and the new direction is applied.
3. **Given** the user is on page 3 and clicks a different column to sort by, **When** the sort column changes, **Then** the page resets to page 1 (since the entire ordering has changed and the user's position is no longer meaningful).

---

### Edge Cases

- What happens when all filings have the same FilingDeadline? The order within the same deadline is deterministic (secondary sort by Id or insertion order) so the UI does not flicker between loads.
- What happens when the Reports list is empty? The default sort is still applied at the query layer so no error occurs; the user simply sees an empty table.
- What happens when a user sorts by a column that contains identical values across all visible rows (e.g., all "Unpaid" status)? The sort is applied but the visual order may not change; no error or unexpected behavior occurs.
- What happens when switching between the "Unpaid" and "Show All" filter while a custom sort is active? The sort column and direction persist; only the filter changes. The page resets to 1 since the data set has changed.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST sort Filings by FilingDeadline descending by default when no user sort preference has been applied.
- **FR-002**: System MUST sort Reports by ImportDate descending by default when no user sort preference has been applied.
- **FR-003**: The Filings DataGrid MUST allow interactive column sorting on all sortable columns (Status, IncomeType, PayingEntity, FilingDeadline, TaxPayable, PaymentReference).
- **FR-004**: Clicking a sortable column header in the Filings DataGrid MUST toggle the sort direction between ascending and descending.
- **FR-005**: The sort operation for Filings MUST be performed at the query/data layer (not client-side) so that sort order is consistent across all pages of a paginated result set.
- **FR-006**: The sort operation for Reports MUST be performed before results are returned to the view layer so the sort order is consistent regardless of the number of reports.
- **FR-007**: The Filings query MUST accept a sort column parameter and a sort direction parameter so that the caller can specify the desired ordering.
- **FR-008**: The Reports query MUST accept a sort direction parameter (with a fixed sort key of ImportDate) so that the caller can request ascending or descending order.
- **FR-009**: Changing only the sort direction on the Filings table MUST NOT reset the current page number.
- **FR-010**: Changing the sort column on the Filings table MUST reset the current page to page 1.
- **FR-011**: The Filings DataGrid MUST have user column sorting enabled (CanUserSortColumns="True").

### Constitution Alignment *(mandatory)*

- **CA-001 (Architecture)**: Impacted layers: Application (query/handler contracts gain sort parameters), Infrastructure (repository query applies ORDER BY), Desktop (ViewModel passes sort state, View enables column sorting). Clean Architecture boundaries remain valid — sort parameters flow inward through queries, and the repository implements the ordering.
- **CA-002 (Money and Dates)**: FilingDeadline (DateOnly) and ImportDate (DateOnly) are the sort keys. No new monetary or date fields are introduced. Existing `decimal` and `DateOnly` usage is unaffected.
- **CA-003 (Privacy and Security)**: No change. All data remains local-first. No new storage or credential requirements.
- **CA-004 (Network Scope)**: No outbound calls affected. Sorting is entirely local.
- **CA-005 (Async and UI)**: Query handlers remain async. Sort parameter changes trigger the same async load flow that pagination already uses. No blocking operations introduced.
- **CA-006 (Testing Impact)**: Domain — no changes needed. Application — unit tests for GetFilingsQueryHandler and GetReportsQueryHandler must verify sort parameters are respected. Infrastructure — integration tests for FilingRepository.GetPagedAsync must verify ORDER BY behavior with sort parameters. Desktop — ViewModel tests must verify sort state management and page-reset logic.

### Key Entities *(include if feature involves data)*

- **Filing**: Existing entity. Sort-relevant attributes: FilingDeadline (DateOnly), Status (FilingStatus), IncomeType (IncomeType), PayingEntity (string), TaxPayableRsd (decimal), PaymentReference (string?). No schema changes.
- **Report**: Existing entity. Sort-relevant attribute: ImportDate (DateOnly). No schema changes.
- **GetFilingsQuery**: Gains two new parameters — SortColumn (identifying which column to sort by) and SortDescending (boolean indicating direction). Default: SortColumn = FilingDeadline, SortDescending = true.
- **GetReportsQuery**: Gains one new parameter — SortDescending (boolean). Default: true. Fixed sort key = ImportDate.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: On first load, the Filings table displays the filing with the latest deadline in the first row, without any user interaction.
- **SC-002**: On first load, the Reports table displays the most recently imported report in the first row, without any user interaction.
- **SC-003**: Users can re-sort the Filings table by any sortable column with a single click, and reverse the direction with a second click.
- **SC-004**: When a user navigates between pages of the Filings table, the applied sort column and direction remain unchanged.
- **SC-005**: When a user changes only the sort direction (same column), the current page number is preserved.
- **SC-006**: Sorting is performed at the query/data layer, ensuring multi-page Filings results are globally ordered — not just sorted within the visible page.
- **SC-007**: All existing Filings and Reports functionality (filtering, pagination, import, export, delete) continues to work correctly with the new sort behavior.

## Assumptions

- The current Filings sort order (FilingDeadline ascending) is the only behavior being changed; all other query behavior (filtering by status, pagination size, report-linked filing retrieval) remains the same.
- Reports already return data sorted by ImportDate descending in the repository layer. The new SortDescending parameter formalizes this and allows the direction to be reversed if a user clicks the column header.
- The Filings DataGrid already has defined columns for Status, IncomeType, PayingEntity, FilingDeadline, TaxPayable, and PaymentReference. All of these are considered sortable. Checkbox and action-button columns are not sortable.
- A deterministic secondary sort (by a stable key such as entity Id) is applied when the primary sort column has duplicate values, to prevent row-order flickering across loads.
- No new database indexes are required for this feature, since FilingDeadline is already the primary ordering axis. If performance profiling later reveals a need, indexing is an infrastructure concern outside the scope of this specification.
- Changing the sort column (not just direction) resets the page to 1 because the user's current page position is no longer meaningful in a differently ordered data set.
