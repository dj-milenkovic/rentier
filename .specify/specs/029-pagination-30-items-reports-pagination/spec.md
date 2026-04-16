# Feature Specification: Pagination — 30 Items per Page & Reports Pagination

**Feature Branch**: `029-pagination-30-items-reports-pagination`  
**Created**: 2025-07-16  
**Status**: Draft  
**Input**: User description: "Standardises the page size to 30 items on the Filings page (up from 20) and introduces server-side pagination on the Reports page (currently returns all rows in a single call). Navigation controls (Previous / page indicator / Next) are added to the bottom of the Reports view, mirroring the existing Filings pagination layout."

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Filings Page Size Increase to 30 (Priority: P1)

A user browsing the Filings page currently sees 20 items per page. After this change, the page displays 30 filings at a time, reducing the number of pages the user must navigate for the same dataset and making it easier to scan filings without excessive page changes.

**Why this priority**: This is the simplest, lowest-risk change — a single constant update — and immediately improves the browsing experience for every user on the most-used page of the application.

**Independent Test**: Can be fully tested by loading the Filings page with more than 30 filings and confirming exactly 30 appear per page, with correct page counts and navigation.

**Acceptance Scenarios**:

1. **Given** 60 filings exist in the system, **When** the user navigates to the Filings page, **Then** 30 filings are displayed on page 1, and the page indicator reads "Page 1 of 2".
2. **Given** 60 filings exist and the user is on page 1, **When** the user clicks "Next", **Then** the remaining 30 filings are displayed on page 2, and the page indicator reads "Page 2 of 2".
3. **Given** 25 filings exist in the system, **When** the user navigates to the Filings page, **Then** all 25 filings are displayed on page 1, and the page indicator reads "Page 1 of 1" with the "Next" button disabled.
4. **Given** 31 filings exist in the system, **When** the user navigates to the Filings page, **Then** 30 filings appear on page 1 and 1 filing appears on page 2.

---

### User Story 2 — Reports Page Pagination with Navigation Controls (Priority: P1)

A user viewing the Reports page currently sees all reports loaded at once with no pagination. After this change, reports are displayed 30 per page with Previous/Next navigation controls at the bottom of the page, matching the Filings page layout. The user can navigate between pages of reports using the same familiar controls.

**Why this priority**: This is the core deliverable of the feature — introducing pagination where none exists. It is equal priority to Story 1 because together they form the complete feature, and the Reports pagination provides the primary new capability.

**Independent Test**: Can be fully tested by having more than 30 reports in the system, navigating to the Reports page, verifying 30 appear per page, and using the Previous/Next buttons to page through results.

**Acceptance Scenarios**:

1. **Given** 75 reports exist in the system, **When** the user navigates to the Reports page, **Then** 30 reports are displayed on page 1, and the page indicator reads "Page 1 of 3".
2. **Given** the user is on page 1 of 3, **When** the user clicks "Next", **Then** 30 reports are displayed on page 2, the page indicator reads "Page 2 of 3", and the "Previous" button is enabled.
3. **Given** the user is on page 3 of 3, **When** the page loads, **Then** the remaining 15 reports are displayed, the page indicator reads "Page 3 of 3", and the "Next" button is disabled.
4. **Given** the user is on page 2 of 3, **When** the user clicks "Previous", **Then** the first 30 reports are displayed on page 1, and the "Previous" button is disabled.
5. **Given** 10 reports exist in the system, **When** the user navigates to the Reports page, **Then** all 10 reports are displayed on page 1, the page indicator reads "Page 1 of 1", and both "Previous" and "Next" are disabled.
6. **Given** no reports exist in the system, **When** the user navigates to the Reports page, **Then** the empty state is displayed and no pagination controls are shown.

---

### User Story 3 — Page Reset on Sort or Filter Changes (Priority: P2)

When the sort direction or any filter criteria changes on the Reports page, the current page resets to 1 so the user always sees the beginning of the newly ordered or filtered dataset. This prevents disorientation from landing on a page number that may be out of range or showing unexpected items after a sort/filter change.

**Why this priority**: This is a defensive UX behaviour that prevents edge-case confusion. It depends on Stories 1 and 2 being implemented first, and while important for correctness, the application currently has no sort or filter controls on the Reports page — this story ensures the reset mechanism is wired in for when such controls are added.

**Independent Test**: Can be tested by navigating to a page beyond 1, then triggering a sort or filter change (if present), and verifying the page resets to 1 with updated data.

**Acceptance Scenarios**:

1. **Given** the user is on page 3 of the Reports page, **When** a sort direction change is triggered, **Then** the page resets to 1 and the reports are re-loaded from the beginning of the new sort order.
2. **Given** the user is on page 2 of the Reports page, **When** a filter criteria changes, **Then** the page resets to 1 and the reports are re-loaded matching the new filter.

---

### Edge Cases

- What happens when the last item on the current page is deleted (e.g., page 3 had one item and it's deleted)? The system navigates to the previous page (page 2) and updates the page indicator.
- What happens when bulk delete removes all items on the current page? The system navigates to page 1 (or shows the empty state if no items remain).
- What happens when new reports are imported while the user is on a page beyond 1? The page count may increase, but the user stays on their current page until they navigate or the list reloads.
- What happens when the total number of reports is exactly a multiple of 30 (e.g., 60, 90)? The page count is exact (2 pages for 60, 3 for 90) with no empty final page.
- What happens when the "Previous" button is clicked on page 1? The button is disabled; no action occurs.
- What happens when the "Next" button is clicked on the last page? The button is disabled; no action occurs.

## Requirements *(mandatory)*

### Functional Requirements

#### Filings Page Size

- **FR-001**: The Filings page MUST display 30 items per page, replacing the previous default of 20.
- **FR-002**: The default page size parameter in the filings query MUST be updated from 20 to 30.

#### Reports Pagination Query

- **FR-003**: The reports query MUST accept a page number parameter (default: 1) and a page size parameter (default: 30).
- **FR-004**: The reports query handler MUST validate that the page number is at least 1 and the page size is between 1 and 100.
- **FR-005**: The reports query handler MUST slice the sorted in-memory list using the page and page size parameters, returning only the items for the requested page.
- **FR-006**: The reports query result MUST include the page of report rows, the total count of all reports, and the total number of pages.

#### Reports ViewModel Pagination State

- **FR-007**: The Reports ViewModel MUST expose CurrentPage, TotalPages, and TotalCount properties reflecting the current pagination state.
- **FR-008**: The Reports ViewModel MUST expose HasPreviousPage (true when current page is greater than 1) and HasNextPage (true when current page is less than total pages) properties.
- **FR-009**: The Reports ViewModel MUST expose a PageIndicator property displaying text in the format "Page X of Y".
- **FR-010**: The Reports ViewModel MUST expose PreviousPageCommand and NextPageCommand for page navigation.
- **FR-011**: PreviousPageCommand MUST decrement the current page by 1 and reload the reports. It MUST be disabled when HasPreviousPage is false or when the page is loading.
- **FR-012**: NextPageCommand MUST increment the current page by 1 and reload the reports. It MUST be disabled when HasNextPage is false or when the page is loading.

#### Reports View Pagination Controls

- **FR-013**: The Reports view MUST display pagination controls (Previous button, page indicator label, Next button) at the bottom of the page, centred horizontally.
- **FR-014**: The pagination controls layout MUST be identical in structure to the existing Filings view pagination bar.
- **FR-015**: The pagination controls MUST be hidden when the Reports page is empty (no items loaded).

#### Page Reset Behaviour

- **FR-016**: The current page MUST reset to 1 whenever a sort direction changes on the Reports page.
- **FR-017**: The current page MUST reset to 1 whenever a filter criteria changes on the Reports page.

#### Localisation

- **FR-018**: All new user-facing strings for Reports pagination (button labels, page indicator format) MUST be defined in the localisation resource file, following the naming convention established by the Filings pagination strings.

### Constitution Alignment *(mandatory)*

- **CA-001 (Architecture)**: This feature impacts three layers: the Desktop layer (ReportsView, ReportsViewModel, Resources), the Application layer (GetReportsQuery, GetReportsQueryHandler, new ReportsPageResult DTO), and marginally the existing Filings query default. Domain entities are unchanged. Clean Architecture boundaries remain valid — the ViewModel dispatches queries with pagination parameters; the Application handler slices data; no new infrastructure changes are needed.
- **CA-002 (Money and Dates)**: No new monetary or date fields are introduced. Existing fields in Report and Filing entities are unaffected.
- **CA-003 (Privacy and Security)**: All data operations remain local-first. No new secrets, credentials, or external data sharing is introduced.
- **CA-004 (Network Scope)**: No new outbound network calls. Pagination operates entirely on locally-stored data already loaded into memory.
- **CA-005 (Async and UI)**: The reports query remains async. Page navigation commands trigger async data loads. The UI MUST remain responsive during page transitions — navigation buttons are disabled while loading to prevent double-submission.
- **CA-006 (Testing Impact)**: Application-layer tests are needed for the updated GetReportsQueryHandler (pagination slicing, boundary validation, total count/pages calculation). Desktop-layer tests are needed for ReportsViewModel pagination state (CurrentPage, HasPreviousPage, HasNextPage, PageIndicator, command enable/disable logic, page reset behaviour). The existing Filings tests should be updated to reflect the new page size of 30. No infrastructure or domain test changes are required.

### Key Entities *(include if feature involves data)*

- **Report**: An imported report record displayed on the Reports page. The existing entity is unchanged — pagination is applied at the query/presentation level by slicing the in-memory sorted list. Key attributes relevant to pagination: display name, import date, email date, importer, status, filing count.
- **ReportsPageResult**: A new result type returned by the reports query, containing the page of report rows, the total count of all reports matching the current criteria, and the total number of pages. Mirrors the shape of the existing FilingsPageResult.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: The Filings page displays exactly 30 items per page when more than 30 filings exist, reducing the total number of pages by approximately one-third compared to the previous page size of 20.
- **SC-002**: The Reports page displays a maximum of 30 items per page when more than 30 reports exist, with correct page counts.
- **SC-003**: Users can navigate the full set of reports using Previous/Next buttons without encountering disabled controls on intermediate pages (only disabled on first/last page boundaries).
- **SC-004**: The page indicator always displays the correct "Page X of Y" text reflecting the current position within the dataset.
- **SC-005**: After any sort direction or filter change on the Reports page, the user is returned to page 1 within the same interaction (no manual page-reset required).
- **SC-006**: 100% of new user-facing pagination strings are externalised in the resource file — zero hardcoded display strings.

## Assumptions

- The Reports page currently has no sort or filter controls. The page-reset requirement (FR-016, FR-017) establishes the reset mechanism proactively so that when sort or filter controls are added in a future feature, the pagination correctly resets. The current implementation needs only to wire the reset into the ViewModel's reactive pipeline so it triggers automatically when those properties change.
- The total number of reports is expected to remain small enough that loading all reports into memory and slicing in the handler is acceptable. No new database index or server-side query slicing is needed.
- The existing in-memory sorting of reports (by whatever order the handler currently returns them) is preserved. Pagination slices the already-sorted list.
- The FilingsPageResult pattern (rows, total count, total pages) is the established convention. The new ReportsPageResult will mirror this shape exactly.
- The existing Filings pagination string resources (Filings_Page_Previous, Filings_Page_Next, Filings_Page_Indicator) can be reused or generalised for the Reports page. If the naming convention requires page-specific keys (e.g., Reports_Page_Previous), new resource entries will follow the same pattern.
- Bulk delete and single-item delete on the Reports page will reload the current page after deletion. If the current page becomes empty (all items deleted), the ViewModel navigates to the previous page or page 1.
- The Filings page size change from 20 to 30 affects only the default constant; existing bookmarks or deep links (if any) that specify a page number remain valid since the page size is a default, not a user-configurable setting.
