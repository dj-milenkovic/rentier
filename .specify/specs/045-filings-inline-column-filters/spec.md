# Feature Specification: Filings Inline Column Filters

**Feature Branch**: `046-filings-inline-column-filters`  
**Created**: 2025-07-15  
**Status**: Draft  
**Input**: User description: "Add inline column filters to the Filings page DataGrid with filter row below column headers, appropriate controls per column type, immediate filtering, AND logic, clear filters, and correct navigation from Reports page."

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Filter Filings by Status (Priority: P1)

A user opens the Filings page and wants to see only filings that still need to be filed. They select "Init" from the Status dropdown in the filter row. The table immediately updates to show only filings with status "Init". They can then switch to "Filed" or "Paid" to review those subsets, or select "All" to remove the filter.

**Why this priority**: Status filtering is the most common filtering need — users manage filings through a workflow (init → filed → paid) and need to focus on one stage at a time. This delivers the core filtering UX pattern that all other filters build upon.

**Independent Test**: Can be fully tested by opening the Filings page with mixed-status filings and selecting each status option from the dropdown. Delivers immediate value by letting users focus on actionable filings.

**Acceptance Scenarios**:

1. **Given** the Filings page is loaded with filings in various statuses, **When** the user selects "Init" from the Status filter dropdown, **Then** only filings with status "Init" are displayed.
2. **Given** the Status filter is set to "Init", **When** the user changes the selection to "All", **Then** all filings are displayed regardless of status.
3. **Given** the Status filter is set to "Filed", **When** the user selects "Paid", **Then** only filings with status "Paid" are displayed (the previous filter is replaced, not combined).

---

### User Story 2 — Filter Filings by Text Columns (Priority: P1)

A user wants to find all filings from a specific payer (Isplatilac). They type part of the payer name into the text filter input for the "Isplatilac" column. The table updates to show only filings where the payer name contains the entered text (case-insensitive). The same pattern applies to the "Referenca plaćanja" (payment reference) column.

**Why this priority**: Text search on payer name and payment reference are essential for daily use — users frequently need to find specific filings by payer or reference number. Equal priority with status filtering as these are complementary filtering dimensions.

**Independent Test**: Can be fully tested by typing a partial payer name and verifying only matching filings appear. Works independently of other filter types.

**Acceptance Scenarios**:

1. **Given** the Filings page has filings from multiple payers, **When** the user types "Microsoft" in the Isplatilac filter, **Then** only filings where the payer name contains "Microsoft" (case-insensitive) are displayed.
2. **Given** the user has typed a filter value in the Isplatilac column, **When** the user clears the text input, **Then** all filings are shown again (assuming no other filters active).
3. **Given** the Referenca plaćanja filter contains "PP-", **When** the user views the table, **Then** only filings whose payment reference contains "PP-" are shown.
4. **Given** the user types "xyz" in the Isplatilac filter and no payer matches, **When** the table updates, **Then** an empty state is shown (no filings displayed).

---

### User Story 3 — Combine Multiple Filters (Priority: P2)

A user wants to find all unpaid dividend filings from a specific payer. They set the Status filter to "Init", select "Dividend" from the Tip prihoda dropdown, and type the payer name. All three filters are applied simultaneously using AND logic, narrowing the results progressively.

**Why this priority**: Combining filters is critical for users with many filings but depends on individual filter controls working first. Delivers advanced power-user functionality.

**Independent Test**: Can be tested by setting two or more filters simultaneously and verifying the result set satisfies all active filter conditions.

**Acceptance Scenarios**:

1. **Given** the Filings page has diverse filings, **When** the user sets Status to "Init" AND Tip prihoda to "Dividend", **Then** only filings matching both conditions are displayed.
2. **Given** multiple filters are active, **When** the user clears one filter, **Then** the remaining filters continue to apply.
3. **Given** three filters are active and produce results, **When** the user adds a fourth filter that eliminates all matches, **Then** an empty state is displayed.

---

### User Story 4 — Clear All Filters (Priority: P2)

A user has set several column filters and wants to start fresh. They click a "Clear filters" control to reset all active filters at once, restoring the full unfiltered view.

**Why this priority**: Clearing all filters at once is a usability essential when multiple filters are active. Without it, users must manually reset each filter individually.

**Independent Test**: Can be tested by setting multiple filters, clicking the clear control, and verifying all filters are reset and all filings are shown.

**Acceptance Scenarios**:

1. **Given** two or more column filters are active, **When** the user clicks "Clear filters", **Then** all filter controls are reset to their default state ("All" for dropdowns, empty for text inputs) and all filings are displayed.
2. **Given** no filters are active, **When** the user views the filter area, **Then** the "Clear filters" control is either hidden or visually disabled.

---

### User Story 5 — Navigate from Reports to a Specific Filing (Priority: P2)

A user is on the Reports page and clicks the "View filings" action for a specific report. The application navigates to the Filings page, and the filing(s) associated with that report are visible and highlighted/selected in the table, regardless of any previously active filters.

**Why this priority**: This cross-page navigation already exists but must work correctly with the new filtering feature. If filters hide the target filing, the navigation becomes broken — a critical usability regression.

**Independent Test**: Can be tested by setting filters on the Filings page, navigating away to Reports, then clicking "View filings" for a report and verifying the target filing is visible and selected.

**Acceptance Scenarios**:

1. **Given** the user is on the Reports page, **When** they click "View filings" for a report, **Then** the Filings page opens with the report's filings visible and the relevant filing(s) selected/highlighted.
2. **Given** the Filings page previously had active filters that would exclude the target filing, **When** the user navigates from Reports to that filing, **Then** the filters are cleared so the target filing is visible.
3. **Given** the user navigates from Reports to a filing, **When** the Filings page loads, **Then** the existing report filter behavior (ReportIdFilter) continues to work, and the inline column filters do not conflict with it.

---

### User Story 6 — Filter by Income Type (Priority: P3)

A user wants to see only dividend filings. They select "Dividend" from the Tip prihoda dropdown in the filter row. The table updates to show only filings of that income type.

**Why this priority**: Income type filtering is useful but less frequently needed than status or payer filtering. The implementation follows the same pattern as the Status dropdown filter.

**Independent Test**: Can be tested by selecting each income type from the dropdown and verifying only matching filings appear.

**Acceptance Scenarios**:

1. **Given** the Filings page has both Dividend and Interest filings, **When** the user selects "Dividend" from the Tip prihoda filter, **Then** only Dividend filings are displayed.
2. **Given** the Tip prihoda filter is set to "Interest", **When** the user selects "All", **Then** filings of all income types are displayed.

---

### User Story 7 — Filter by Filing Deadline (Priority: P3)

A user wants to see filings with a specific deadline date. They use the date filter for the "Rok za podnošenje" column to select or enter a date. Only filings matching that deadline are shown.

**Why this priority**: Date filtering is useful for deadline management but is a less common filter compared to status and payer. Users more often sort by deadline than filter to an exact date.

**Independent Test**: Can be tested by entering a date and verifying only filings with that deadline are displayed.

**Acceptance Scenarios**:

1. **Given** the Filings page has filings with various deadlines, **When** the user enters a specific date in the deadline filter, **Then** only filings with that exact deadline date are displayed.
2. **Given** the deadline filter has a date entered, **When** the user clears it, **Then** all filings are shown (assuming no other active filters).

---

### Edge Cases

- What happens when the user types very quickly in a text filter? — Filtering should debounce input to avoid excessive re-filtering on every keystroke.
- What happens when filtering reduces results to zero? — An appropriate empty state message should be displayed (e.g., "No filings match the active filters").
- What happens when filters are active and the user changes the sort column? — Sorting should apply within the filtered result set.
- What happens when filters are active and the user paginates? — Pagination should operate on the filtered result set, with page counts reflecting filtered totals.
- What happens when a new filing is created while filters are active? — After returning to the Filings page, the new filing should be visible (filters may need to be cleared or the filing should appear if it matches active filters).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST display a filter row below the column header row in the Filings DataGrid.
- **FR-002**: The Status column MUST have a dropdown filter control with options: "All" (default), "Init", "Filed", "Paid".
- **FR-003**: The Tip prihoda (income type) column MUST have a dropdown filter control with options: "All" (default), "Dividend", "Interest".
- **FR-004**: The Isplatilac (payer) column MUST have a text input filter with placeholder text "Filter..." that performs case-insensitive contains matching.
- **FR-005**: The Referenca plaćanja (payment reference) column MUST have a text input filter with placeholder text "Filter..." that performs case-insensitive contains matching.
- **FR-006**: The Rok za podnošenje (filing deadline) column MUST have a date filter control that filters to an exact date match.
- **FR-007**: Filters MUST be applied immediately when the user changes a filter value (no submit button).
- **FR-008**: Text input filters MUST debounce user input to avoid excessive filtering (reasonable delay, e.g., 300ms).
- **FR-009**: When multiple filters are active, they MUST be combined using AND logic.
- **FR-010**: A "Clear filters" control MUST be available that resets all filters to their default state in a single action.
- **FR-011**: The "Clear filters" control MUST only be visible or enabled when at least one filter is active.
- **FR-012**: When navigating from the Reports page to a specific filing, the system MUST ensure the target filing is visible — clearing any active inline column filters that would hide it.
- **FR-013**: Filtering MUST work correctly with the existing pagination — the page count and navigation must reflect the filtered result set.
- **FR-014**: Filtering MUST work correctly with the existing sort functionality — sorting applies within the filtered results.
- **FR-015**: When no filings match the active filters, the system MUST display an empty state message indicating that filters are hiding results.
- **FR-016**: The existing ReportIdFilter navigation (from Reports page) MUST continue to function and not conflict with inline column filters.

### Constitution Alignment *(mandatory)*

- **CA-001 (Architecture)**: This feature impacts the Desktop (presentation) layer only. Filter state and logic reside in the ViewModel. No changes to Domain, Application, or Infrastructure layers. Clean Architecture boundaries remain valid — filtering is a UI concern applied to data already loaded by the existing query/pagination system.
- **CA-002 (Money and Dates)**: The filing deadline filter involves `DateOnly` values. No monetary fields are filtered (only displayed). Date filtering must compare using `DateOnly` equality, consistent with existing domain conventions.
- **CA-003 (Privacy and Security)**: No new data is stored or transmitted. Filter state is ephemeral (in-memory, per-session). No credential or sensitive data concerns.
- **CA-004 (Network Scope)**: No new outbound network calls. Filtering is applied to already-loaded data or modifies existing query parameters sent to the local database.
- **CA-005 (Async and UI)**: Filter changes trigger reactive updates via the existing ViewModel pattern. Text filter debouncing ensures the UI thread is not blocked by rapid input. If filtering requires re-querying the database, it must be done asynchronously.
- **CA-006 (Testing Impact)**: Desktop ViewModel tests needed for filter state management, filter combination logic, clear-filters behavior, and Reports-to-Filings navigation with active filters. No Domain or Infrastructure test changes expected.

### Key Entities *(include if feature involves data)*

- **Filing**: The primary entity displayed in the Filings DataGrid. Key filterable attributes: Status (enum: Init, Filed, Paid), IncomeType (enum: Dividend, Interest), PayingEntity (text), FilingDeadline (date), PaymentReference (text).
- **Filter State**: Ephemeral per-session state representing the currently active filter values for each column. Not persisted — resets when leaving the page or restarting the application.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users can filter filings by any single column and see results update within 1 second of setting the filter value.
- **SC-002**: Users can combine filters across all 5 filterable columns simultaneously and the displayed results satisfy all active filter conditions.
- **SC-003**: Users can clear all active filters with a single action and see the full unfiltered list restored within 1 second.
- **SC-004**: Navigation from the Reports page to a specific filing always results in the target filing being visible and selected, regardless of previously active filters.
- **SC-005**: Pagination correctly reflects the filtered result count — page totals update when filters change.
- **SC-006**: Users can identify the filter row controls and apply a filter without any instruction or training (discoverable UX).

## Assumptions

- Filter state is ephemeral and not persisted across page navigations or application restarts.
- Filtering is applied either client-side (on loaded page data) or as modified query parameters for server-side/database filtering — the implementation approach is left to the planning phase.
- The existing pagination mechanism (page size, current page) will be adapted to work with filtered result sets.
- The existing ReportIdFilter (used for Reports → Filings navigation) is a separate mechanism from inline column filters, but both must coexist without conflict.
- Dropdown filter options are derived from the domain enums (FilingStatus, IncomeType) and displayed with user-friendly labels.
- Date filtering uses exact date match (not date range) as the initial implementation; range filtering may be added in a future iteration.
- The filter row is always visible (not toggled on/off) to maximize discoverability.
