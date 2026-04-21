# Feature Specification: Column Width Audit — Filings & Reports Tables

**Feature Branch**: `035-column-width-audit`  
**Created**: 2025-07-17  
**Status**: Draft  
**Input**: User description: "Reviews and standardises all column widths across the Filings and Reports DataGrids so that content is neither truncated nor excessively padded. Establishes consistent padding on cell content and aligns the visual weight of both tables."

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Filings Table Columns Are Right-Sized (Priority: P1)

A user opens the Filings page and scans the table. Every column is wide enough to display its content without truncation — dates are fully visible, monetary amounts show the complete formatted value, and the paying-entity column fills all remaining horizontal space. At the same time, no column wastes screen real estate with excessive whitespace; the selection checkbox column is compact, the status badge fits snugly, and fixed-width columns match the natural width of their content type. The user can read and interact with the table comfortably without needing to manually resize columns.

**Why this priority**: The Filings table is the primary working surface for tax filing management. If column widths are too narrow, critical data (amounts, deadlines, references) is unreadable; if too wide, fewer columns fit on screen, forcing horizontal scrolling. Getting this right is the single highest-value change.

**Independent Test**: Can be fully tested by loading the Filings page with representative data (short and long paying-entity names, various amount formats) and confirming every cell's content is fully visible without truncation and without obvious excess whitespace.

**Acceptance Scenarios**:

1. **Given** the Filings page is loaded with filing records, **When** the user views the table, **Then** the Selection column displays only a checkbox at 40 pixels wide with no excess padding.
2. **Given** the Filings page displays filing records with status badges, **When** the user views the Status column, **Then** the status pill is fully visible within a 90-pixel-wide column without text clipping.
3. **Given** a filing has the income type "Dividend" or "Interest", **When** the user views the Income Type column, **Then** the full text is visible within a 110-pixel-wide column.
4. **Given** a filing has a long paying-entity name, **When** the user views the Paying Entity column, **Then** the column fills all remaining horizontal space and displays as much text as possible, truncating with ellipsis only when the window is very narrow.
5. **Given** a filing has a deadline of "2025-12-31", **When** the user views the Filing Deadline column, **Then** the full date is visible within a 120-pixel-wide column.
6. **Given** a filing has a tax payable amount of "1,234.56 RSD", **When** the user views the Tax Payable column, **Then** the full formatted amount is visible within a 130-pixel-wide column.
7. **Given** a filing has a payment reference, **When** the user views the Payment Reference column, **Then** the reference text is visible within a 180-pixel-wide column.
8. **Given** the Filings page displays action buttons, **When** the user views the Actions column, **Then** the column auto-sizes to fit three icon buttons without clipping or extra whitespace.

---

### User Story 2 — Reports Table Columns Are Right-Sized (Priority: P1)

A user opens the Reports page and scans the table. The column layout mirrors the disciplined sizing of the Filings table — fixed-width columns match their content type, the report-name column fills the remaining space, and the action column auto-sizes to its icon buttons. The user perceives both tables as belonging to the same application with a unified design language.

**Why this priority**: The Reports table is the second primary data surface. Aligning its column widths is equally important for consistency and usability. This story is P1 alongside Story 1 because both tables must be addressed together to achieve visual alignment.

**Independent Test**: Can be fully tested by loading the Reports page with representative data (varied report names, different importers) and confirming every cell's content is fully visible without truncation and without obvious excess whitespace.

**Acceptance Scenarios**:

1. **Given** the Reports page is loaded with report records, **When** the user views the table, **Then** the Selection column displays only a checkbox at 40 pixels wide.
2. **Given** a report has a long name, **When** the user views the Report Name column, **Then** the column fills all remaining horizontal space.
3. **Given** a report has an import date of "2025-07-15", **When** the user views the Import Date column, **Then** the full date is visible within a 110-pixel-wide column.
4. **Given** a report has an email date or an empty email date, **When** the user views the Email Date column, **Then** the content (date or blank) displays within a 110-pixel-wide column.
5. **Given** a report has an importer display name, **When** the user views the Importer column, **Then** the name is visible within a 160-pixel-wide column.
6. **Given** a report has a status of "Init", "Processed", or "Error", **When** the user views the Status column, **Then** the full status text is visible within a 100-pixel-wide column.
7. **Given** a report has a filing count, **When** the user views the Filing Count column, **Then** the number is visible within a 70-pixel-wide column.
8. **Given** the Reports page displays action buttons, **When** the user views the Actions column, **Then** the column auto-sizes to fit two icon buttons without clipping or extra whitespace.

---

### User Story 3 — Consistent Cell Padding Across Both Tables (Priority: P2)

A user looks at both the Filings and Reports tables and notices the text within cells has uniform horizontal breathing room. Content is neither jammed against cell edges nor floated in the middle of oversized padding. The consistent 4-pixel horizontal margin on all cell content elements creates a clean, aligned appearance that makes both tables feel like part of the same coherent design system.

**Why this priority**: Padding consistency is a polish item that elevates the overall quality of the UI. It is secondary to getting column widths correct (Stories 1 and 2) but important for the visual alignment goal stated in the feature description.

**Independent Test**: Can be tested by visually inspecting cells across both tables to confirm uniform horizontal spacing between cell edges and content, and by confirming that no cell content touches the cell boundary.

**Acceptance Scenarios**:

1. **Given** the Filings page is displayed with data, **When** the user inspects any text cell, **Then** the inner content element has a 4-pixel horizontal margin (left and right) creating consistent breathing room.
2. **Given** the Reports page is displayed with data, **When** the user inspects any text cell, **Then** the inner content element has the same 4-pixel horizontal margin as the Filings table cells.
3. **Given** both tables are visible (by switching between pages), **When** the user compares the horizontal padding of cell content, **Then** both tables exhibit identical spacing, giving a unified look.

---

### Edge Cases

- What happens when the application window is resized to a very narrow width? The fill-width columns (Paying Entity in Filings, Report Name in Reports) shrink to accommodate, potentially showing ellipsis-truncated text. Fixed-width columns retain their specified widths regardless of window size.
- What happens when a Paying Entity name or Report Name is extremely short (e.g. one word)? The fill-width column still occupies all remaining space; the short text is left-aligned with standard padding, leaving whitespace to the right — this is expected behaviour for a fill column.
- What happens when no data rows are present (empty table)? Column headers still display at the specified widths. The column width definitions apply to the header row even when no data rows exist.
- What happens when the user manually resizes a column (if column resizing is enabled)? The user's resize takes precedence at runtime. The specified widths define the initial default state only.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The Filings DataGrid Selection (checkbox) column MUST have a fixed width of 40 pixels.
- **FR-002**: The Filings DataGrid Status badge column MUST have a fixed width of 90 pixels.
- **FR-003**: The Filings DataGrid Income Type column MUST have a fixed width of 110 pixels.
- **FR-004**: The Filings DataGrid Paying Entity column MUST use fill (star) width to consume all remaining horizontal space.
- **FR-005**: The Filings DataGrid Filing Deadline column MUST have a fixed width of 120 pixels.
- **FR-006**: The Filings DataGrid Tax Payable column MUST have a fixed width of 130 pixels.
- **FR-007**: The Filings DataGrid Payment Reference column MUST have a fixed width of 180 pixels.
- **FR-008**: The Filings DataGrid Actions column MUST use auto width to size itself to its content (three icon buttons).
- **FR-009**: The Reports DataGrid Selection (checkbox) column MUST have a fixed width of 40 pixels.
- **FR-010**: The Reports DataGrid Report Name column MUST use fill (star) width to consume all remaining horizontal space.
- **FR-011**: The Reports DataGrid Import Date column MUST have a fixed width of 110 pixels.
- **FR-012**: The Reports DataGrid Email Date column MUST have a fixed width of 110 pixels.
- **FR-013**: The Reports DataGrid Importer column MUST have a fixed width of 160 pixels.
- **FR-014**: The Reports DataGrid Status column MUST have a fixed width of 100 pixels.
- **FR-015**: The Reports DataGrid Filing Count column MUST have a fixed width of 70 pixels.
- **FR-016**: The Reports DataGrid Actions column MUST use auto width to size itself to its content (two icon buttons).
- **FR-017**: All cell templates in both DataGrids MUST apply a horizontal margin of 4 pixels (left and right, zero vertical) to their inner TextBlock or primary content element.
- **FR-018**: All existing column behaviours (sorting, editing, command bindings) MUST remain unchanged — this feature modifies only column sizing and cell padding.

### Constitution Alignment *(mandatory)*

- **CA-001 (Architecture)**: Only the Desktop layer (view markup) is impacted. Column width values and cell template margins are purely presentational. No changes to Domain, Application, or Infrastructure layers. Clean Architecture boundaries remain intact.
- **CA-002 (Money and Dates)**: The Tax Payable column displays monetary values and the Filing Deadline, Import Date, and Email Date columns display dates. This feature changes only column widths — the underlying `decimal` and `DateOnly` data types and formatting logic are not modified.
- **CA-003 (Privacy and Security)**: No data storage or credential handling changes. Not applicable.
- **CA-004 (Network Scope)**: No outbound network calls introduced or modified. Not applicable.
- **CA-005 (Async and UI)**: No new I/O operations introduced. Column width and margin changes are static layout properties that do not affect async operations or UI-thread responsiveness.
- **CA-006 (Testing Impact)**: Desktop UI rendering tests (if present) for the Filings and Reports pages should be updated to verify the new column widths and cell margins. ViewModel tests are not impacted because no command bindings or data-binding logic changes.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of Filings DataGrid columns match the specified width values (40, 90, 110, fill, 120, 130, 180, auto) when the page loads at the default window size.
- **SC-002**: 100% of Reports DataGrid columns match the specified width values (40, fill, 110, 110, 160, 100, 70, auto) when the page loads at the default window size.
- **SC-003**: Zero content truncation is observed in any fixed-width column when populated with representative data (standard date formats, monetary amounts up to 9,999.99 RSD, status text, and typical display names).
- **SC-004**: All cell content elements in both tables have uniform 4-pixel horizontal margins, verified by visual inspection or layout inspection tooling.
- **SC-005**: Both tables exhibit the same padding and spacing conventions, creating a visually consistent appearance when switching between the Filings and Reports pages.
- **SC-006**: All pre-existing table functionality (sorting, selection, editing, commands) continues to work identically — zero behavioural regressions.

## Assumptions

- Features 027–031 (default sort, header checkbox, pagination, Filings action column consolidation, Reports icon-only action column) are completed before this feature begins, as they affect column definitions and the number of action buttons present.
- The Filings DataGrid Actions column contains three icon buttons (from feature 030) and the Reports DataGrid Actions column contains two icon buttons (from feature 031).
- The Filings Paying Entity column and Reports Report Name column are the only columns that use fill (star) width; all other columns use fixed pixel widths or auto width.
- "Auto" width for Actions columns means the column sizes itself to its content (the icon buttons), not a hardcoded pixel value — the actual rendered width may vary slightly based on icon size and button padding.
- The 4-pixel horizontal margin convention applies to the innermost content element of each cell template (the text label, badge, or primary control), not to the cell container itself.
- Column resizing by the user at runtime (if enabled) overrides the default widths; this feature sets the initial state only.
- This feature is a purely visual adjustment — no data model, ViewModel, or business logic changes are required.
