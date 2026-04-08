# Feature Specification: Holidays Settings Repair

**Feature Branch**: `feature/020-holidays-settings-repair`  
**Created**: 2025-07-15  
**Status**: Draft  
**Input**: User description: "Fix Settings -> Holidays functionality and usability — DataGrid edit commit/cancel for DateOnly rows, import from timeanddate.com, year field layout clipping, and state handling."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Edit Holiday Date Reliably (Priority: P1)

A user opens Settings → Holidays and sees the list of configured holidays in a data grid. They click on a holiday date cell to change the date value. The cell enters edit mode, they type a new date, and press Enter or Tab to commit. The new value is accepted and reflected in the row. If the user presses Escape, the edit is cancelled and the original value remains. After editing, the Save button persists all changes.

**Why this priority**: The inability to edit existing holiday dates is the most disruptive bug — users cannot correct mistakes or adjust dates without deleting and re-adding rows, which is error-prone and frustrating.

**Independent Test**: Can be fully tested by opening the Holidays settings, double-clicking a date cell, typing a new date, pressing Enter to commit, and verifying the date updates. Pressing Escape should discard the edit. Delivers reliable inline editing of holiday data.

**Acceptance Scenarios**:

1. **Given** the Holidays grid has a row with date "2025-01-01", **When** the user double-clicks the date cell, types "2025-01-07", and presses Enter, **Then** the cell displays "2025-01-07" and HasUnsavedChanges becomes true.
2. **Given** the Holidays grid has a row with date "2025-01-01", **When** the user double-clicks the date cell, types "2025-01-07", and presses Escape, **Then** the cell reverts to "2025-01-01" and HasUnsavedChanges remains unchanged.
3. **Given** the user has edited a date and committed, **When** they click Save, **Then** the new date is persisted and a success confirmation message is displayed.
4. **Given** the user enters an invalid date string (e.g., "abc"), **When** they press Enter, **Then** the edit is rejected, the original value is restored, and the user sees feedback that the input was invalid.

---

### User Story 2 - Import Holidays from Web (Priority: P1)

A user wants to populate the holidays list for a specific year by importing public holidays from an online source. They enter the desired year and click Import. The system fetches the holiday data from a public web source, parses the results, and populates the grid with the imported national holidays. The user sees a loading indicator during the operation and clear feedback on success or failure.

**Why this priority**: Import is the primary way users populate holiday lists without manual entry. The current implementation does not correctly parse the source HTML, so no holidays are ever returned.

**Independent Test**: Can be fully tested by entering a year (e.g., 2025), clicking Import, and verifying that national holidays for Serbia appear in the grid. Delivers automated population of holiday data from a public source.

**Acceptance Scenarios**:

1. **Given** the user enters year 2025, **When** they click Import, **Then** the system fetches and parses Serbian national holidays for 2025 and populates the grid with holiday entries (date and name).
2. **Given** the import is in progress, **When** the fetch is running, **Then** IsLoading is true and a loading indicator is visible. Other action buttons are disabled while loading.
3. **Given** the import succeeds, **When** results are displayed, **Then** IsLoading becomes false and the grid shows only National Holiday type entries (not observances, seasons, or optional holidays).
4. **Given** the network is unavailable, **When** the user clicks Import, **Then** an error message is displayed with code HOLIDAY_IMPORT_FAILED, IsLoading becomes false, and the existing grid contents are preserved (not cleared).
5. **Given** the fetched HTML cannot be parsed or contains no matching holidays, **When** import completes, **Then** an error message is displayed with code HOLIDAY_PARSE_ERROR or HOLIDAY_NOT_FOUND, and the existing grid contents are preserved.
6. **Given** the user already has entries in the grid, **When** import succeeds, **Then** the imported holidays replace the current grid contents and HasUnsavedChanges becomes true.

---

### User Story 3 - Year Fields Fully Visible (Priority: P2)

A user views the Holidays settings screen and can fully see and interact with the Start Year and End Year fields. The numeric controls display complete 4-digit year values without clipping, truncation, or overflow, across all supported window widths.

**Why this priority**: While less critical than broken functionality, clipped input fields degrade usability and give an unfinished impression. Users may not realize the full value is hidden.

**Independent Test**: Can be tested by opening the Holidays settings at various supported window widths and verifying that both year fields show the full 4-digit value, the spinner arrows are accessible, and the labels are fully readable.

**Acceptance Scenarios**:

1. **Given** the Holidays settings panel is displayed at minimum supported width, **When** the user views the year range row, **Then** both Start Year and End Year fields show the full 4-digit value without clipping.
2. **Given** the window is resized between minimum and maximum supported widths, **When** the layout adjusts, **Then** the year fields and their labels remain fully visible and properly aligned.
3. **Given** a year value of 2099 (maximum), **When** displayed in the NumericUpDown control, **Then** all four digits and spinner arrows are visible.

---

### User Story 4 - Clear State Feedback (Priority: P2)

A user performs any action (load, save, import) and sees appropriate visual feedback. Loading state, error messages, and success messages are shown and cleared at the correct times. Empty-state messaging is shown when the grid has no entries.

**Why this priority**: Clear state transitions prevent user confusion and ensure they know the outcome of each action.

**Independent Test**: Can be tested by performing load, save, and import actions and verifying the correct feedback messages appear and disappear at the right moments.

**Acceptance Scenarios**:

1. **Given** any async operation starts, **When** IsLoading becomes true, **Then** a progress indicator is visible and action buttons are disabled.
2. **Given** an operation completes successfully, **When** IsLoading becomes false, **Then** a success message appears and any previous error message is cleared.
3. **Given** an operation fails, **When** IsLoading becomes false, **Then** an error message appears with a meaningful description and any previous success message is cleared.
4. **Given** the grid has no entries, **When** the view renders, **Then** an empty-state message is shown indicating no holidays are configured.

---

### Edge Cases

- What happens when the user imports holidays for a year outside the configured Start Year / End Year range? The import should still succeed (grid is populated), but the user must adjust the year range and save to persist.
- How does the system handle duplicate holidays on the same date after import? The save operation already validates for duplicate dates and returns an error if duplicates exist.
- What happens if the web source changes its HTML structure? The parser should return a HOLIDAY_PARSE_ERROR with a descriptive message rather than silently returning empty results.
- What happens when the user edits a date to a value outside the configured year range? The edit commits locally; validation occurs at save time.
- What happens if the user clicks Import while a previous import is still in progress? The Import button should be disabled while IsLoading is true, preventing concurrent operations.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST support reliable inline editing of DateOnly values in the holidays DataGrid, with commit on Enter/Tab and cancel on Escape.
- **FR-002**: System MUST parse date input strings during edit and reject invalid date formats, restoring the previous value on failure.
- **FR-003**: System MUST import Serbian national holidays from `https://www.timeanddate.com/holidays/serbia/{YEAR}?hol=1` by parsing the HTML table with id `holidays-table`.
- **FR-004**: System MUST filter imported holidays to include only rows classified as "National Holiday" type (rows with CSS class `showrow` and data-mask indicating national holiday status).
- **FR-005**: System MUST extract holiday dates from `<th>` elements (format: "d MMM", e.g., "1 Jan", "15 Feb") and holiday names from the anchor text within the third `<td>` element of each data row.
- **FR-006**: System MUST display loading state (IsLoading = true) during all async operations (load, save, import) and disable action commands while loading.
- **FR-007**: System MUST clear previous error/success messages before starting any new operation.
- **FR-008**: System MUST preserve existing grid contents when an import operation fails (no partial clearing).
- **FR-009**: System MUST display year range fields (Start Year, End Year) without visual clipping at all supported window widths.
- **FR-010**: System MUST use standard error codes: HOLIDAY_IMPORT_FAILED for network errors, HOLIDAY_PARSE_ERROR for HTML parsing failures, HOLIDAY_NOT_FOUND when no national holidays are found for the requested year.
- **FR-011**: System MUST set HasUnsavedChanges to true after a successful import replaces grid contents.
- **FR-012**: System MUST show an empty-state indicator when the holidays grid contains no entries.

### Constitution Alignment *(mandatory)*

- **CA-001 (Architecture)**: Impacted layers: Domain (HolidayConf, PublicHoliday — no changes expected), Application (IHolidayImporter interface, ImportHolidaysFromWebCommand handler), Infrastructure (TimeAndDateHolidayScraper parser fix), Desktop (HolidaySettingsView XAML layout, HolidaySettingsViewModel state handling, HolidayEntryViewModel DateOnly editing). Clean Architecture boundaries preserved: parsing logic stays in Infrastructure, command/query flow stays in Application, UI binding in Desktop.
- **CA-002 (Money and Dates)**: All holiday dates use `DateOnly`. Year range uses `int`. No monetary fields involved. The HolidayEntryViewModel.Date property is `DateOnly`; the DataGrid cell editing must convert string input to `DateOnly` and back.
- **CA-003 (Privacy and Security)**: All holiday data is stored locally in the application database. No user credentials are involved. The web import fetches publicly available information only.
- **CA-004 (Network Scope)**: Single outbound HTTP GET to `https://www.timeanddate.com/holidays/serbia/{YEAR}?hol=1`. This is a public, read-only request with no authentication. Import is triggered only by explicit user action (Constitution CA-EXT-001 compliance).
- **CA-005 (Async and UI)**: All I/O operations (load, save, import/fetch) use async commands via `ReactiveCommand.CreateFromTask`. UI thread is never blocked. IsLoading flag gates UI during operations.
- **CA-006 (Testing Impact)**: Required test updates:
  - **Infrastructure**: Parser tests using captured HTML samples from `holiday-scraped.txt` to verify correct extraction of dates, names, and filtering of non-national holidays.
  - **Application**: Handler tests for import success/failure flows (existing tests may need updating for new error codes).
  - **Desktop**: ViewModel tests for edit commit/cancel behavior, import state transitions (IsLoading, ErrorMessage, HasUnsavedChanges), and empty-state handling.
  - **Desktop (UI)**: Layout tests verifying year field visibility at supported window widths.

### Key Entities *(include if feature involves data)*

- **PublicHoliday**: Represents a single holiday entry with a unique identifier, date (DateOnly), name (string), and year (int, derived from date). Stored in the PublicHolidays table.
- **HolidayYearRange**: Singleton entity defining the configured year range (StartYear, EndYear). Constraints: StartYear ≥ 2020, EndYear ≤ StartYear + 10, EndYear ≥ StartYear.
- **HolidayConf**: Value object aggregating a read-only list of holiday dates. Used for domain calculations.
- **HolidayEntryDto**: Data transfer object carrying a single holiday's date and name between layers.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users can edit any holiday date cell inline and commit or cancel the edit within 2 seconds, with no data loss or UI freezes.
- **SC-002**: Import operation for any valid year (2020–2099) returns the expected national holidays and populates the grid within 10 seconds under normal network conditions.
- **SC-003**: Parser correctly extracts all national holidays from the source HTML — for the 2016 sample data, this yields exactly 13 national holiday entries (from Western New Year's Day through Armistice Day).
- **SC-004**: Year fields (Start Year, End Year) display all 4 digits without clipping at the minimum supported window width.
- **SC-005**: All async operations show and hide the loading indicator at the correct times, with no stale error or success messages visible after a new operation begins.
- **SC-006**: 100% of new functional requirements have corresponding automated tests that pass.

## Assumptions

- The HTML structure of `https://www.timeanddate.com/holidays/serbia/{YEAR}?hol=1` follows the format observed in the captured `holiday-scraped.txt` sample (table with id `holidays-table`, `<th>` for dates, `<td>` for day-of-week/name/type, CSS classes `showrow`/`hiderow` to distinguish holiday visibility).
- "National Holiday" type rows are the only ones to be imported; observances, seasons, optional holidays, and religious-only entries are excluded.
- The date format in the HTML source is consistently "d MMM" (English month abbreviations, e.g., "1 Jan", "15 Feb") with the year derived from the URL parameter.
- The existing AngleSharp library (already a project dependency) is used for HTML parsing in Infrastructure.
- The Avalonia DataGrid supports custom value converters for cell editing, enabling DateOnly string-to-value conversion.
- Network access is available when the user triggers import; no offline caching of previously imported holidays is required.
- The minimum supported window width for the Rentier application is sufficient for the toolbar and year range controls to display in a single horizontal row.
