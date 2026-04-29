# Feature Specification: Reports Filter Header Flyouts

**Feature Branch**: `043-reports-filter-header-flyouts`  
**Created**: 2025-07-15  
**Status**: Draft  
**Input**: User description: "Rework the existing reports inline filter row (feature 047) — replace the confusing operator selector row with Excel-style column header filter popups/flyouts"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Filter Reports by Column via Header Flyout (Priority: P1)

A user viewing the reports list wants to filter by a specific column. They click the funnel icon in the column header, a small popup appears with a text input (or checkboxes for status), they type or select their filter criteria, press "Apply", and the table immediately filters to show only matching rows. The funnel icon turns to an accent color to indicate an active filter on that column.

**Why this priority**: This is the core interaction replacing the removed filter row — without it, users lose all filtering capability on the reports page.

**Independent Test**: Can be fully tested by opening the reports page, clicking a column header funnel icon, entering a filter value, and confirming filtered results appear correctly with server-side pagination intact.

**Acceptance Scenarios**:

1. **Given** the reports page is displayed with data, **When** the user clicks the funnel icon on the "Name" column header, **Then** a flyout popup appears anchored to that column header containing a text input with placeholder "Pretraži..." and an "Apply" button.
2. **Given** the Name column flyout is open and the user types "Godišnji", **When** the user clicks "Apply", **Then** the flyout closes, the table shows only reports whose name contains "Godišnji", and the funnel icon on the Name column is highlighted in the accent color.
3. **Given** the reports page is displayed, **When** the user clicks the funnel icon on the "Status" column header, **Then** a flyout appears with a checkbox for each report status value, plus "Select All" and "Clear" options.
4. **Given** the Status flyout is open with all statuses checked, **When** the user unchecks "Draft" and clicks "Apply", **Then** the table shows only reports that are NOT in Draft status.

---

### User Story 2 - Clear Individual and All Filters (Priority: P2)

A user who has applied filters to several columns wants to remove them — either one column at a time by reopening a flyout and clearing its value, or all at once using the existing "Clear All Filters" toolbar button.

**Why this priority**: Without the ability to remove filters, users can get stuck seeing a filtered subset with no way back to the full list.

**Independent Test**: Can be tested by applying filters to two columns, clearing one via its flyout, verifying partial clear works, then clearing all via the toolbar button.

**Acceptance Scenarios**:

1. **Given** a filter is active on the Name column (funnel icon highlighted), **When** the user opens the Name flyout, clears the text input, and clicks "Apply", **Then** the Name filter is removed, the funnel icon returns to its default color, and the table shows unfiltered results (or results filtered by remaining active filters).
2. **Given** filters are active on Name and Status columns, **When** the user clicks "Clear All Filters" in the toolbar, **Then** all filters are removed, all funnel icons return to default color, and the table shows the full unfiltered results.
3. **Given** no filters are active, **When** the user views the toolbar, **Then** the "Clear All Filters" button is hidden or disabled.

---

### User Story 3 - Filter by Date and Numeric Columns via Text Search (Priority: P3)

A user wants to find reports by date or filing count. They open the column header flyout for a date or numeric column and type a search value. For dates, they type partial date text (e.g., "2024-03" or "ožujak") and the system matches against the formatted date string. For numeric columns, they type a number for exact match.

**Why this priority**: Date and numeric filtering completes the full filtering story but is less frequently used than name/status filtering.

**Independent Test**: Can be tested by filtering on Import Date with a partial date string and on Filing Count with a number, confirming correct results for each.

**Acceptance Scenarios**:

1. **Given** the reports page is displayed, **When** the user opens the Import Date flyout, types "2024-03", and clicks "Apply", **Then** the table shows only reports whose formatted import date contains "2024-03".
2. **Given** the Filing Count flyout is open, **When** the user types "5" and clicks "Apply", **Then** the table shows only reports with exactly 5 filings.
3. **Given** the Filing Count flyout is open, **When** the user types "abc" (non-numeric) and clicks "Apply", **Then** the invalid input is silently ignored, no filter is applied for that column, and the funnel icon remains in default color.
4. **Given** the Import Date flyout is open, **When** the user types text that does not match any date, **Then** the filter is applied as a text-contains search on the formatted date string and returns zero results (no error shown).

---

### User Story 4 - Debounced Text Input in Flyouts (Priority: P3)

A user typing in a text filter flyout experiences responsive feedback. The filter value is debounced (300ms) so that rapid keystrokes do not trigger excessive server requests.

**Why this priority**: Performance optimization that prevents server overload during fast typing, but not critical for basic functionality.

**Independent Test**: Can be tested by rapidly typing in a filter flyout and confirming that the server request fires only after a 300ms pause.

**Acceptance Scenarios**:

1. **Given** the Name flyout is open, **When** the user types "Go" quickly followed by "d", **Then** only one filter request is sent to the server (after the 300ms debounce), not three separate requests.

---

### Edge Cases

- What happens when the user opens a flyout, types a value, then clicks outside the flyout without clicking "Apply"? The flyout closes and the filter is NOT applied (no change to current filter state).
- What happens when the user opens a flyout for a column that already has an active filter? The flyout shows the current filter value pre-populated so the user can edit or clear it.
- What happens when filtering returns zero results? The table shows an empty state (existing empty-state behavior) and the active filter icons remain highlighted so the user knows filters are applied.
- What happens when the user opens a Status flyout and unchecks ALL statuses? No results are shown (empty table), equivalent to filtering to nothing.
- What happens with very long filter text input? The text input accepts up to a reasonable length; the server-side query handles it gracefully.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST remove the existing inline filter row (the horizontal row of TextBoxes, ComboBoxes, DatePickers, and operator selectors above the DataGrid).
- **FR-002**: Each filterable column header MUST display a small funnel/filter icon.
- **FR-003**: Clicking the funnel icon MUST open a flyout/popup anchored to that column header.
- **FR-004**: Text column flyouts (Name, Importer) MUST contain a text input with placeholder "Pretraži..." and an "Apply" button.
- **FR-005**: Enum column flyouts (Status/ReportStatus) MUST contain a checkbox list with one checkbox per enum value, plus "Select All" and "Clear" convenience options, and an "Apply" button.
- **FR-006**: Date column flyouts (Import Date, Email Date) MUST contain a text input for text-based search on the formatted date string, with an "Apply" button.
- **FR-007**: Numeric column flyouts (Filing Count) MUST contain a text input for number entry, with an "Apply" button.
- **FR-008**: When a filter is active on a column, the funnel icon for that column MUST be visually distinguished (highlighted in accent color).
- **FR-009**: When a filter is active on a column and the user reopens the flyout, the flyout MUST show the current filter value pre-populated.
- **FR-010**: Clicking "Apply" in a flyout MUST close the flyout and trigger a server-side filtered query.
- **FR-011**: Clicking outside an open flyout (dismissing it) MUST NOT apply any pending filter changes.
- **FR-012**: The existing "Clear All Filters" toolbar button MUST continue to clear all column filters at once and reset all funnel icons to default color.
- **FR-013**: Text filter inputs MUST be debounced at 300ms before triggering a server request (debounce fires on Apply, not on each keystroke — OR if live-filtering is desired, 300ms after last keystroke within the flyout).
- **FR-014**: For numeric columns, if the user input is not a valid number, the system MUST silently ignore the input and not apply a filter for that column.
- **FR-015**: For date columns, the system MUST perform a text-contains match on the server-formatted date string.
- **FR-016**: The system MUST only send "equals" (for numeric/enum) or "contains" (for text/date) matching to the server — no greater-than/less-than operators from the UI.
- **FR-017**: The system MUST retain server-side filtering to ensure correctness with paginated data (only 30 items loaded per page).
- **FR-018**: The existing ClearFiltersCommand, HasActiveFilters property, and ViewModel filter properties MUST be preserved (adapted to the new flyout interaction).

### Constitution Alignment *(mandatory)*

- **CA-001 (Architecture)**: Impacted layers: Desktop (view + ViewModel changes), Application (simplify filter usage to equals/contains only from UI). Domain and Infrastructure layers are unchanged. Clean Architecture boundaries remain valid — UI changes stay in Desktop, query simplification stays in Application.
- **CA-002 (Money and Dates)**: Date columns (Import Date, Email Date) use DateOnly. The feature changes how dates are filtered (text search on formatted string) but does not change date storage or representation.
- **CA-003 (Privacy and Security)**: No change — all data remains local, no new external data exposure.
- **CA-004 (Network Scope)**: No new outbound calls. Filtering queries go to the local database only.
- **CA-005 (Async and UI)**: Filter queries are async server-side operations. Debounce prevents UI blocking. Flyout open/close is synchronous UI interaction.
- **CA-006 (Testing Impact)**: Desktop tests: update ViewModel tests to verify new filter flow (flyout-driven apply, no operator selection). Application tests: verify simplified filter matching (contains/equals only). Infrastructure tests: verify repository handles text-contains date queries correctly.

### Key Entities

- **ReportColumnFilter**: Represents a filter applied to a single column — contains the column identifier, filter value (text string), and match type (contains or equals). Existing entity adapted to remove operator complexity.
- **Report**: The entity being filtered — has columns: Name, Importer, Status (enum), Import Date, Email Date, Filing Count. No changes to the entity itself.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users can apply a column filter in 2 clicks or fewer (click funnel icon → type/select → click Apply).
- **SC-002**: The filter row vertical space is reduced to zero — filters live entirely within column headers, reclaiming the vertical space previously used by the inline filter row.
- **SC-003**: 100% of previously available column filters (Name, Importer, Status, Import Date, Email Date, Filing Count) remain functional via the new flyout mechanism.
- **SC-004**: Users can identify which columns have active filters at a glance via visual highlighting of funnel icons.
- **SC-005**: Filter results appear within 1 second of clicking "Apply" under normal conditions (local database).
- **SC-006**: Pagination continues to work correctly with active filters — total count and page navigation reflect filtered results.
- **SC-007**: Users who are familiar with Excel/Google Sheets column filtering can use the reports filter flyouts without instructions.

## Assumptions

- The existing reports DataGrid columns are plain DataGridTextColumn (not template columns) and will need to be converted to DataGridTemplateColumn to embed funnel icons in headers.
- The ComparisonOperator enum in the Application layer will be retained for backward compatibility but the UI will only ever send "Equals" or "Contains" values.
- The flyout/popup will be implemented using the standard popup/flyout mechanism available in the UI framework — no custom windowing needed.
- Date formatting for text search uses the same locale-specific format already displayed in the DataGrid (e.g., "dd.MM.yyyy" or similar Croatian format).
- The 300ms debounce applies to the moment the user clicks "Apply" to prevent double-clicks, not to live-as-you-type filtering within the flyout (filtering only triggers on explicit "Apply" action).
- The "Clear All Filters" button in the toolbar already exists and its command binding will be preserved — only the filter application mechanism changes.
