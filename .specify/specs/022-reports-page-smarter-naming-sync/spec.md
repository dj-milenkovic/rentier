# Feature Specification: Reports Page – Smarter Naming and Sync Clarification

**Feature Branch**: `003-reports-naming-sync`  
**Created**: 2025-07-09  
**Status**: Draft  
**Input**: User description: "Improve the Reports page naming and sync discoverability. Problems reported by QA: (1) Report names are raw file paths / long CSV file names — hard to read. (2) 'Sync mailboxes' button purpose is unclear; users do not understand why the same action exists on the dedicated Sync page."

## User Scenarios & Testing *(mandatory)*

### User Story 1 – Friendly Report Display Names (Priority: P1)

A user opens the Reports page and sees a list of imported reports. Currently the "Report Name" column shows raw file paths or long CSV file names (e.g., `C:\Downloads\IBKR_Activity_2024-03-15.csv`), which are hard to scan and interpret. After this change, each report row displays a human-friendly label following the pattern **"&lt;ImporterDisplayName&gt; – &lt;StatementDate&gt;"**, where StatementDate is the earliest income date among the report's filings (e.g., "IBKR CSV – 2024-03-15"). If the report has no filings yet, the import date is used as fallback (e.g., "IBKR CSV – 2024-07-09"). The original file name is still accessible via a tooltip when hovering over the display name, so users can reference the source file when needed.

**Why this priority**: This is the primary usability complaint from QA. Friendly names directly reduce cognitive load and make the Reports page functional for users managing multiple imports.

**Independent Test**: Can be fully tested by importing several reports with known importers and filing dates, then verifying the Reports page shows the expected friendly labels and tooltips.

**Acceptance Scenarios**:

1. **Given** a report imported via the "IBKR CSV" importer with filings whose earliest income date is 2024-03-15, **When** the user opens the Reports page, **Then** the display name column shows "IBKR CSV – 2024-03-15".
2. **Given** a report imported via the "IBKR CSV" importer with no filings (e.g., import in error state) and an import date of 2024-07-09, **When** the user opens the Reports page, **Then** the display name column shows "IBKR CSV – 2024-07-09".
3. **Given** a report whose importer cannot be resolved (e.g., the importer was deleted), **When** the user opens the Reports page, **Then** the display name shows "Unknown – &lt;date&gt;" using the same date fallback logic.
4. **Given** any report row on the Reports page, **When** the user hovers over the display name, **Then** a tooltip shows the original file name (e.g., `IBKR_Activity_2024-03-15.csv`).

---

### User Story 2 – Sync Button Clarification on Reports Page (Priority: P2)

A user on the Reports page sees a "Sync Mailboxes" button but is unsure what it does or why it also appears on the dedicated Sync page. After this change, a short descriptive subtitle or info banner appears near the "Sync Mailboxes" button explaining that syncing downloads new statements from configured mailboxes and processes them into reports. It also briefly differentiates this from the Sync page, which shows per-mailbox status and history.

**Why this priority**: This is a discoverability and comprehension issue. While less urgent than broken naming, it causes user confusion and support questions. It can be delivered independently as a UI-only text change.

**Independent Test**: Can be fully tested by navigating to the Reports page and verifying the explanatory text appears near the "Sync Mailboxes" button, and that the text is loaded from localized string resources.

**Acceptance Scenarios**:

1. **Given** the user is on the Reports page, **When** the page loads, **Then** a descriptive subtitle or info banner is visible near the "Sync Mailboxes" button.
2. **Given** the descriptive text is displayed, **When** the user reads it, **Then** it explains that sync downloads new statements from configured mailboxes and processes them into reports.
3. **Given** the descriptive text is displayed, **When** the user reads it, **Then** it differentiates the Reports-page sync action from the dedicated Sync page (which shows per-mailbox status and history).
4. **Given** the descriptive text content, **When** inspected, **Then** the text is sourced from localized string resources (not hardcoded in the view).

---

### User Story 3 – Unit Tests for Display Name Derivation (Priority: P3)

The display name derivation logic (combining importer name and earliest income date with fallback) is critical business logic in the Application layer. Comprehensive unit tests must verify the happy path, fallback behaviour, and edge cases to prevent regressions.

**Why this priority**: Testing supports the P1 story. It is listed separately because it can be developed and verified independently as pure logic tests without a running UI.

**Independent Test**: Can be fully tested by running the unit test suite and verifying all display-name-related test cases pass.

**Acceptance Scenarios**:

1. **Given** a report with filings that have income dates, **When** the display name is derived, **Then** it uses the earliest income date.
2. **Given** a report with no filings, **When** the display name is derived, **Then** it falls back to the report's import date.
3. **Given** a report whose importer is not found, **When** the display name is derived, **Then** it uses "Unknown" as the importer portion.
4. **Given** multiple reports with varying filing counts and importer associations, **When** the query is executed, **Then** each report has the correct display name.

---

### Edge Cases

- What happens when a report has filings but all have the same income date? The display name shows that single date (no ambiguity).
- What happens when the importer display name is very long (up to 200 characters)? The display name is still derived normally; the UI column handles overflow via truncation or ellipsis (existing grid behaviour).
- What happens when multiple reports share the same importer and earliest income date? Both show the same display name — this is acceptable because the original file name in the tooltip and the other columns (import date, status, filing count) still distinguish them.
- What happens when a report is a revision (has an OriginalReportId)? The display name follows the same derivation rules — revisions are treated as independent reports for naming purposes.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST derive a display name for each report using the pattern "&lt;ImporterDisplayName&gt; – &lt;EarliestIncomeDate&gt;" where EarliestIncomeDate is formatted as `yyyy-MM-dd`.
- **FR-002**: System MUST fall back to the report's import date (formatted as `yyyy-MM-dd`) when the report has no associated filings.
- **FR-003**: System MUST use "Unknown" as the importer portion of the display name when the importer cannot be resolved.
- **FR-004**: System MUST include the derived display name in the report data transferred to the UI layer.
- **FR-005**: System MUST preserve the original file name (ReportName) and make it available separately from the display name.
- **FR-006**: The Reports page MUST show the friendly display name as the primary label in the report list.
- **FR-007**: The Reports page MUST show the original file name in a tooltip when the user hovers over the display name.
- **FR-008**: The Reports page MUST display a descriptive subtitle or info banner near the "Sync Mailboxes" button explaining the sync action.
- **FR-009**: The sync explanatory text MUST differentiate the Reports-page sync from the dedicated Sync page (per-mailbox status and history).
- **FR-010**: All user-visible text introduced by this feature MUST be sourced from localized string resources.
- **FR-011**: System MUST provide a way to query the earliest income date of filings belonging to a specific report.
- **FR-012**: Unit tests MUST cover display name derivation for: reports with filings, reports without filings, and reports with unresolvable importers.

### Constitution Alignment *(mandatory)*

- **CA-001 (Architecture)**: This feature touches four layers: Domain (no changes — existing entities suffice), Application (new repository method signature, updated DTO, updated query handler), Infrastructure (new repository method implementation), and Desktop (updated view model and view, new string resources). Clean Architecture boundaries remain valid — the Application layer defines the interface, Infrastructure implements it, and the Desktop layer consumes the DTO.
- **CA-002 (Money and Dates)**: The EarliestIncomeDate is a `DateOnly` value (from Filing.IncomeDate). The ImportDate fallback is also `DateOnly` (from Report.ImportDate). No monetary fields are introduced or modified.
- **CA-003 (Privacy and Security)**: No new data is stored; the display name is derived at query time. All data remains local-first. No secrets or credentials are involved.
- **CA-004 (Network Scope)**: No new outbound network calls. The sync clarification text is purely informational UI — it does not change sync behaviour.
- **CA-005 (Async and UI)**: The new repository method to query earliest income date is async. The query handler already operates asynchronously. No blocking UI operations are introduced.
- **CA-006 (Testing Impact)**: New unit tests are required in the Application layer for the GetReportsQueryHandler display name logic. Existing handler tests should continue to pass. No Domain or Infrastructure tests are strictly required (the new repository method is a simple query), but integration tests may optionally verify the query.

### Key Entities

- **Report**: Represents an imported statement file. Key attributes: Id, ReportName (original file name), ImportDate, ImporterId, Status. The new display name is a derived value computed at query time, not a stored field.
- **Filing**: Represents an individual tax filing created from a report. Key attribute for this feature: IncomeDate (DateOnly) — used to derive the statement date portion of the display name. Linked to Report via ReportId.
- **Importer**: Represents an import source configuration. Key attribute: DisplayName — used as the importer portion of the report display name. Linked to Report via ImporterId.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users can identify the source and date of any report within 2 seconds of scanning the Reports page, without needing to hover or click.
- **SC-002**: 100% of report rows on the Reports page show a friendly display name instead of a raw file path or CSV file name.
- **SC-003**: Users can access the original file name for any report via tooltip without navigating away from the Reports page.
- **SC-004**: Users reading the sync clarification text can correctly explain what the "Sync Mailboxes" button does and how it differs from the Sync page (verifiable via usability walkthrough).
- **SC-005**: All unit tests for display name derivation pass, covering at minimum: reports with filings, reports without filings, and reports with missing importers.
- **SC-006**: No existing tests are broken by this change.

## Assumptions

- The existing importer name resolution in GetReportsQueryHandler (with "Unknown" fallback) will continue to be used for the importer portion of the display name.
- The `yyyy-MM-dd` date format is appropriate for the statement date in the display name, consistent with the existing ImportDateDisplay format in the UI.
- The sync explanatory text is static informational content and does not need to change dynamically based on mailbox configuration state.
- The display name is derived at query time and is not persisted to the database — this avoids schema changes and keeps the derivation logic centralized.
- The existing Strings.Designer.cs auto-generation workflow requires manual re-generation after updating Strings.resx (consistent with current project practices).
- The en dash character (–) in the display name pattern is the intended separator, providing clear visual distinction between the importer name and the date.
