# Feature Specification: Reports Inline Column Filters

**Feature Branch**: `047-reports-inline-column-filters`  
**Created**: 2025-07-15  
**Status**: Draft  
**Input**: User description: "Add inline column filters to the Reports page DataGrid with operator-based filtering for numeric/date columns, text contains search, enum dropdowns, and a clear filters button."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Filter Reports by Text Column (Priority: P1)

A user wants to quickly find reports from a specific importer. They look at the filter row below the column headers, type part of the importer name into the "Name" or "Importer" column filter input, and immediately see only matching reports.

**Why this priority**: Text search is the most common filter operation — users frequently need to locate reports by name or importer. This provides immediate value for the most typical lookup workflow.

**Independent Test**: Can be fully tested by typing a partial name into the Name column filter and verifying only matching rows appear, delivering instant search value.

**Acceptance Scenarios**:

1. **Given** the Reports page is loaded with multiple reports, **When** the user types "IBKR" into the Name column filter, **Then** only reports whose display name contains "IBKR" (case-insensitive) are shown.
2. **Given** the Name column filter contains "IBKR", **When** the user clears the text, **Then** all reports reappear (subject to other active filters).
3. **Given** the user types a search term that matches no reports, **When** filtering is applied, **Then** the grid shows an empty state and the filter input retains the entered value.

---

### User Story 2 - Filter Reports by Date Column with Operators (Priority: P1)

A user wants to find reports imported after a specific date. They select the ">" operator in the Import Date filter, enter a date, and the grid immediately shows only reports imported after that date.

**Why this priority**: Date-based filtering is essential for time-sensitive tax reporting workflows — users need to find recent imports, reports from a specific period, or reports around a deadline.

**Independent Test**: Can be fully tested by selecting ">" operator, entering a date in the Import Date filter, and verifying only reports with import dates after the entered date are shown.

**Acceptance Scenarios**:

1. **Given** the Reports page shows reports with various import dates, **When** the user selects ">" and enters "2024-06-01" in the Import Date filter, **Then** only reports imported after June 1, 2024 are shown.
2. **Given** the Import Date filter has operator "<" and value "2024-01-01", **When** the user changes the operator to "=", **Then** only reports imported on exactly January 1, 2024 are shown.
3. **Given** the Email Date column filter is active with operator ">", **When** the user enters a date, **Then** reports with no email date (null) are excluded from results.

---

### User Story 3 - Filter Reports by Numeric Column with Operators (Priority: P2)

A user wants to find reports with more than a certain number of filings. They select the ">" operator in the Filing Count column filter, enter a number, and only matching reports appear.

**Why this priority**: Numeric filtering enables power users to find high-value or anomalous reports quickly, but is less frequently needed than text or date searches.

**Independent Test**: Can be fully tested by selecting ">" operator in Filing Count filter, entering "10", and verifying only reports with more than 10 filings appear.

**Acceptance Scenarios**:

1. **Given** the Reports page is loaded, **When** the user selects ">" and enters "5" in the Filing Count filter, **Then** only reports with more than 5 filings are shown.
2. **Given** the Filing Count filter has operator "=" and value "0", **When** filtering is applied, **Then** only reports with zero filings are shown.
3. **Given** the user enters a non-numeric value in the Filing Count filter, **When** the input loses focus, **Then** the invalid input is ignored and no filter is applied for that column.

---

### User Story 4 - Filter Reports by Status Dropdown (Priority: P2)

A user wants to see only reports in an error state. They open the Status column filter dropdown and select "Error" to narrow the list.

**Why this priority**: Status-based filtering lets users triage reports by processing state, important for workflow management but less frequent than name/date lookups.

**Independent Test**: Can be fully tested by selecting "Error" from the Status dropdown filter and verifying only error-status reports are shown.

**Acceptance Scenarios**:

1. **Given** the Reports page is loaded, **When** the user selects "Error" from the Status dropdown filter, **Then** only reports with Error status are shown.
2. **Given** the Status dropdown shows "Processed" selected, **When** the user changes selection to show all (empty/default option), **Then** the status filter is cleared and all reports appear (subject to other filters).
3. **Given** the Status dropdown is active, **When** the dropdown options are displayed, **Then** all report statuses (Init, Processed, Error, PartialError) plus an "All" or empty default option are available.

---

### User Story 5 - Combine Multiple Filters and Clear All (Priority: P1)

A user has set filters on multiple columns to narrow their search. They want to reset all filters at once using a "Clear filters" button.

**Why this priority**: Multi-filter combination and bulk clear are essential usability features that make the filtering system practical for real workflows.

**Independent Test**: Can be fully tested by setting filters on 2+ columns, verifying combined AND logic, then clicking "Clear filters" and verifying all filters reset.

**Acceptance Scenarios**:

1. **Given** the Name filter contains "IBKR" and the Status filter is set to "Processed", **When** both filters are active, **Then** only reports matching both conditions (name contains "IBKR" AND status is Processed) are shown.
2. **Given** multiple column filters are active, **When** the user clicks "Clear filters", **Then** all filter inputs are reset to their defaults and all reports are shown.
3. **Given** no filters are active, **When** the user views the toolbar area, **Then** the "Clear filters" button is hidden or disabled.

---

### Edge Cases

- What happens when the user enters a date in an invalid format? The filter is not applied for that column until a valid date is entered; no error message disrupts the workflow.
- What happens when all filters combined yield zero results? The grid shows the standard empty state; filters remain visible and editable so the user can adjust.
- How does filtering interact with pagination? Filters reset the page to 1 and are applied across the full dataset, with pagination operating on the filtered result set.
- What happens when a nullable date column (Email Date) is filtered? Reports with null values in that column are excluded from comparison-based filters (>, <, =).
- What happens when the user types very quickly in a text filter? Input is debounced to avoid excessive re-filtering; only the final value after a brief pause triggers filtering.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST display a filter row directly below the DataGrid column headers on the Reports page.
- **FR-002**: Text columns (Name, Importer) MUST provide a text input that filters rows where the column value contains the entered text (case-insensitive).
- **FR-003**: Date columns (Import Date, Email Date) MUST provide an operator selector (>, <, =) and a date value input. The default operator MUST be "=" (equals).
- **FR-004**: Numeric columns (Filing Count) MUST provide an operator selector (>, <, =) and a numeric value input. The default operator MUST be "=" (equals).
- **FR-005**: Enum columns (Status) MUST provide a dropdown selector listing all possible values plus a default "All" option that applies no filter.
- **FR-006**: Multiple active column filters MUST combine using AND logic — a row must satisfy all active filters to be displayed.
- **FR-007**: A "Clear filters" button MUST be available that resets all column filters to their default (empty) state in a single action.
- **FR-008**: The "Clear filters" button MUST only be visible or enabled when at least one filter is active.
- **FR-009**: Filtering MUST be applied immediately when the user changes a filter value, without requiring an explicit "Apply" action.
- **FR-010**: Text filter input MUST be debounced (short delay after typing stops) to prevent excessive re-filtering during fast typing.
- **FR-011**: When any filter changes, the current page MUST reset to page 1 and pagination MUST operate on the filtered result set.
- **FR-012**: Invalid input in numeric filters (non-numeric text) MUST be silently ignored — no filter is applied for that column until valid input is provided.
- **FR-013**: Date filters on nullable columns (Email Date) MUST exclude rows with null values when a filter value is specified.
- **FR-014**: The filter row MUST not interfere with existing DataGrid features (column selection checkbox, row selection, action buttons, pagination).
- **FR-015**: The filter row UX pattern MUST be consistent with the Filings page inline column filters (feature 045) to provide a unified experience across the application.

### Constitution Alignment *(mandatory)*

- **CA-001 (Architecture)**: Impacts the Desktop (UI) layer for filter row presentation and the Application layer for filtered query handling. Domain and Infrastructure layers may need filter parameter support in the query/repository. Clean Architecture boundaries remain valid — filter logic flows from UI → Application → Infrastructure.
- **CA-002 (Money and Dates)**: Date filter values use `DateOnly` for comparison. Filing Count uses `int`. No monetary fields are filtered in the current Reports columns, but the pattern must support `decimal` for future monetary column filters.
- **CA-003 (Privacy and Security)**: All filtering occurs on local data. No external data transmission. No sensitive data exposed by filter inputs.
- **CA-004 (Network Scope)**: No outbound network calls. All filtering operates on locally stored report data.
- **CA-005 (Async and UI)**: Filter changes trigger async query re-execution via the existing paged query handler. UI remains responsive during filtering with debounced inputs.
- **CA-006 (Testing Impact)**: Domain: filter predicate logic tests. Application: query handler tests with filter parameters. Desktop: ViewModel tests for filter state management, reactive command behavior, and debounce logic. Infrastructure: repository tests for filtered queries.

### Key Entities *(include if feature involves data)*

- **Column Filter**: Represents the active filter state for a single column — includes the column identifier, filter type (text/date/numeric/enum), the operator (for date/numeric: >, <, =), and the current filter value.
- **Filter Row State**: The aggregate of all active column filters for the Reports page, used to determine whether "Clear filters" should be visible and to compose the combined filter criteria for the query.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users can filter the reports list by any column within 2 seconds of entering a filter value, including query execution and grid update.
- **SC-002**: Users can narrow a list of 1,000+ reports to a specific subset using 1–3 column filters without scrolling or navigating away from the page.
- **SC-003**: Users can reset all active filters to the default state with a single click using the "Clear filters" button.
- **SC-004**: The filter row is immediately discoverable below column headers without requiring user instruction or documentation.
- **SC-005**: Users working across both the Reports and Filings pages experience a consistent filtering interaction pattern, reducing learning effort.
- **SC-006**: Combining two or more column filters always produces a result set matching all specified criteria (AND logic), with zero false positives.

## Assumptions

- The Reports page DataGrid columns remain as currently implemented: checkbox, Name (text), Import Date (date), Email Date (date), Importer (text), Status (enum), Filing Count (numeric), and Actions. If columns are added or removed, filter definitions will need updating.
- Filtering is performed server-side (in the query/repository layer) to work correctly with pagination. Client-side filtering of the current page only would produce incorrect results with paged data.
- The debounce delay for text input will follow a standard UX interval (approximately 300ms) consistent with common desktop application patterns.
- The operator selector for date and numeric columns will be a compact dropdown or toggle to minimize horizontal space within the filter row cells.
- Feature 045 (Filings page inline column filters) will be implemented first or concurrently, establishing shared UI patterns and potentially reusable filter components that this feature will adopt.: [FEATURE NAME]

**Feature Branch**: `[###-feature-name]`  
**Created**: [DATE]  
**Status**: Draft  
**Input**: User description: "$ARGUMENTS"

## User Scenarios & Testing *(mandatory)*

<!--
  IMPORTANT: User stories should be PRIORITIZED as user journeys ordered by importance.
  Each user story/journey must be INDEPENDENTLY TESTABLE - meaning if you implement just ONE of them,
  you should still have a viable MVP (Minimum Viable Product) that delivers value.
  
  Assign priorities (P1, P2, P3, etc.) to each story, where P1 is the most critical.
  Think of each story as a standalone slice of functionality that can be:
  - Developed independently
  - Tested independently
  - Deployed independently
  - Demonstrated to users independently
-->

### User Story 1 - [Brief Title] (Priority: P1)

[Describe this user journey in plain language]

**Why this priority**: [Explain the value and why it has this priority level]

**Independent Test**: [Describe how this can be tested independently - e.g., "Can be fully tested by [specific action] and delivers [specific value]"]

**Acceptance Scenarios**:

1. **Given** [initial state], **When** [action], **Then** [expected outcome]
2. **Given** [initial state], **When** [action], **Then** [expected outcome]

---

### User Story 2 - [Brief Title] (Priority: P2)

[Describe this user journey in plain language]

**Why this priority**: [Explain the value and why it has this priority level]

**Independent Test**: [Describe how this can be tested independently]

**Acceptance Scenarios**:

1. **Given** [initial state], **When** [action], **Then** [expected outcome]

---

### User Story 3 - [Brief Title] (Priority: P3)

[Describe this user journey in plain language]

**Why this priority**: [Explain the value and why it has this priority level]

**Independent Test**: [Describe how this can be tested independently]

**Acceptance Scenarios**:

1. **Given** [initial state], **When** [action], **Then** [expected outcome]

---

[Add more user stories as needed, each with an assigned priority]

### Edge Cases

<!--
  ACTION REQUIRED: The content in this section represents placeholders.
  Fill them out with the right edge cases.
-->

- What happens when [boundary condition]?
- How does system handle [error scenario]?

## Requirements *(mandatory)*

<!--
  ACTION REQUIRED: The content in this section represents placeholders.
  Fill them out with the right functional requirements.
-->

### Functional Requirements

- **FR-001**: System MUST [specific capability, e.g., "allow users to create accounts"]
- **FR-002**: System MUST [specific capability, e.g., "validate email addresses"]  
- **FR-003**: Users MUST be able to [key interaction, e.g., "reset their password"]
- **FR-004**: System MUST [data requirement, e.g., "persist user preferences"]
- **FR-005**: System MUST [behavior, e.g., "log all security events"]

*Example of marking unclear requirements:*

- **FR-006**: System MUST authenticate users via [NEEDS CLARIFICATION: auth method not specified - email/password, SSO, OAuth?]
- **FR-007**: System MUST retain user data for [NEEDS CLARIFICATION: retention period not specified]

### Constitution Alignment *(mandatory)*

- **CA-001 (Architecture)**: Specify impacted layers and confirm Clean Architecture boundaries remain valid.
- **CA-002 (Money and Dates)**: Identify all monetary/date fields and confirm `decimal` and `DateOnly` usage.
- **CA-003 (Privacy and Security)**: Confirm storage is local-first and any secrets use OS credential storage.
- **CA-004 (Network Scope)**: List outbound calls and confirm they are within allowed endpoints.
- **CA-005 (Async and UI)**: Confirm all I/O is async and UI flows avoid blocking operations.
- **CA-006 (Testing Impact)**: Define required Domain/Application/Infrastructure/Desktop test updates.

### Key Entities *(include if feature involves data)*

- **[Entity 1]**: [What it represents, key attributes without implementation]
- **[Entity 2]**: [What it represents, relationships to other entities]

## Success Criteria *(mandatory)*

<!--
  ACTION REQUIRED: Define measurable success criteria.
  These must be technology-agnostic and measurable.
-->

### Measurable Outcomes

- **SC-001**: [Measurable metric, e.g., "Users can complete account creation in under 2 minutes"]
- **SC-002**: [Measurable metric, e.g., "System handles 1000 concurrent users without degradation"]
- **SC-003**: [User satisfaction metric, e.g., "90% of users successfully complete primary task on first attempt"]
- **SC-004**: [Business metric, e.g., "Reduce support tickets related to [X] by 50%"]

## Assumptions

<!--
  ACTION REQUIRED: The content in this section represents placeholders.
  Fill them out with the right assumptions based on reasonable defaults
  chosen when the feature description did not specify certain details.
-->

- [Assumption about target users, e.g., "Users have stable internet connectivity"]
- [Assumption about scope boundaries, e.g., "Mobile support is out of scope for v1"]
- [Assumption about data/environment, e.g., "Existing authentication system will be reused"]
- [Dependency on existing system/service, e.g., "Requires access to the existing user profile API"]
