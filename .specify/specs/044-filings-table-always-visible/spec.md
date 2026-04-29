# Feature Specification: Filings Table Always Visible

**Feature Branch**: `044-filings-table-always-visible`  
**Created**: 2025-07-15  
**Status**: Draft  
**Input**: User description: "On the Filings page, the data table (DataGrid) should ALWAYS be visible, even when there are no filings. Currently, the table is hidden or replaced with a message when empty. Instead, show the empty table with column headers visible so users understand the page structure and available columns."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Empty Filings Page Shows Table Structure (Priority: P1)

A user opens the Filings page for the first time (or after clearing all filings). Instead of seeing a plain text message that replaces the table, the user sees the full table with all column headers visible (Status, Income Type, Paying Entity, Deadline, Tax Payable, Payment Reference, Actions). The empty table communicates what data will appear and what columns are available, helping the user understand the page purpose immediately.

**Why this priority**: This is the core of the feature — the table must remain visible when empty. Without this, the feature has no value.

**Independent Test**: Can be fully tested by navigating to the Filings page with zero filings and verifying the table with column headers is rendered.

**Acceptance Scenarios**:

1. **Given** the user has no filings in the system, **When** they navigate to the Filings page, **Then** the data table is displayed with all column headers visible and zero data rows.
2. **Given** the user has no filings in the system, **When** they navigate to the Filings page, **Then** the previous full-page "no data" placeholder text is no longer shown in place of the table.
3. **Given** the user has no filings in the system, **When** they view the empty table, **Then** all column headers (selection checkbox, Status, Income Type, Paying Entity, Deadline, Tax Payable, Payment Reference, Actions) are visible and correctly labeled.

---

### User Story 2 - Polished Empty State Within Table (Priority: P2)

When viewing the Filings page with no data, the user sees a subtle, non-intrusive indication that no filings exist yet. This message appears inside or below the table area — it does not replace the table. The empty state feels intentional and polished rather than broken or missing.

**Why this priority**: This adds polish to the core change. The table being visible is functional; a helpful empty-state message improves user experience.

**Independent Test**: Can be tested by navigating to the Filings page with zero filings and verifying a subtle empty-state message is present alongside the visible table.

**Acceptance Scenarios**:

1. **Given** the user has no filings, **When** they view the Filings page, **Then** a subtle message indicating no filings are available is displayed inside or below the table area.
2. **Given** the user has no filings, **When** they view the empty-state message, **Then** the message does not obscure or replace the table column headers.
3. **Given** the user has filings, **When** they view the Filings page, **Then** no empty-state message is shown and the table displays data rows as before.

---

### User Story 3 - Consistent Table Behavior Across States (Priority: P3)

As a user who transitions between having filings and not having filings (e.g., after deleting all filings or after initial data load), the table remains stable on the page. The table does not appear/disappear — only the rows change. This provides a consistent, predictable layout.

**Why this priority**: Ensures the table doesn't flicker or jump during state transitions, which reinforces visual stability.

**Independent Test**: Can be tested by loading the page with filings, then removing all filings, and verifying the table structure persists throughout.

**Acceptance Scenarios**:

1. **Given** the user has filings displayed in the table, **When** all filings are removed (e.g., filtered out or deleted), **Then** the table remains visible with column headers and zero data rows.
2. **Given** the user sees the empty table, **When** filings data becomes available (e.g., after a sync), **Then** the table populates with rows without any layout shift or structural change.

---

### Edge Cases

- What happens when the page is loading (data fetch in progress)? The table should still be visible; a loading indicator may overlay or appear within the table area, but column headers remain visible.
- What happens if the data load fails with an error? The table with column headers should still be visible; error feedback should appear separately from the table structure.
- What happens if the user has a very narrow screen and the table columns overflow? The existing horizontal scroll behavior should apply identically whether the table has rows or not.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The filings data table MUST be rendered with all column headers visible regardless of whether any filing rows exist.
- **FR-002**: The full-page "no data" placeholder that currently replaces the table MUST be removed or hidden so it no longer takes the place of the table.
- **FR-003**: When zero filings exist, a subtle empty-state indicator MUST be displayed inside or below the table area to inform the user that no data is available yet.
- **FR-004**: The empty-state indicator MUST NOT obscure, replace, or hide the table column headers.
- **FR-005**: When filings exist, the table MUST display data rows as it currently does — existing behavior for populated tables MUST NOT change.
- **FR-006**: The table MUST remain visible during all page states: loading, empty, populated, and error.
- **FR-007**: This change MUST be limited to the Desktop UI layer only — no changes to Domain, Application, or Infrastructure layers.

### Constitution Alignment *(mandatory)*

- **CA-001 (Architecture)**: Only the Desktop (presentation) layer is impacted. The view and view-model for the Filings page will be modified. Clean Architecture boundaries remain valid — no business logic or data access changes are needed.
- **CA-002 (Money and Dates)**: No monetary or date fields are added or changed. Existing column bindings for Tax Payable (decimal) and Filing Deadline (DateOnly) remain unchanged.
- **CA-003 (Privacy and Security)**: No change to data storage or security. This is a purely visual change.
- **CA-004 (Network Scope)**: No outbound calls are added or changed.
- **CA-005 (Async and UI)**: No I/O changes. The visibility logic change is purely reactive property-based and does not introduce blocking operations.
- **CA-006 (Testing Impact)**: Desktop UI tests should be updated to verify the table renders in empty state. No Domain, Application, or Infrastructure test changes required.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of the time when a user navigates to the Filings page with zero filings, all column headers are visible on screen.
- **SC-002**: The full-page "no data" text placeholder no longer appears in place of the table under any circumstance.
- **SC-003**: Users can identify all available data columns (Status, Income Type, Paying Entity, Deadline, Tax Payable, Payment Reference, Actions) within 2 seconds of page load, even with no data.
- **SC-004**: The page layout remains stable (no visible layout shifts) when transitioning between empty and populated states.

## Assumptions

- The existing column definitions and their order remain unchanged; this feature only affects table visibility, not column structure.
- The loading state indicator (spinner or similar) already exists and will continue to function alongside the always-visible table.
- Localized text for the empty-state indicator will reuse or adapt the existing `Filings_Empty` localization key.
- No new pages, routes, or navigation changes are required — this is a modification to the existing Filings page only.
