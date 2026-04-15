# Feature Specification: Holiday Fetcher — timeanddate.com Scraper

**Feature Branch**: `026-holiday-web-scraper`  
**Created**: 2025-07-18  
**Status**: Draft  
**Input**: User description: "Add a 'Fetch from web' capability to the Holidays settings page. Source URL: https://www.timeanddate.com/holidays/serbia/{year}?hol=1 where {year} is substituted at runtime. The query parameter hol=1 restricts the response to public holidays."

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Fetch Serbian Public Holidays for a Single Year (Priority: P1)

A user navigates to the Holidays settings page and wants to populate the holiday list for a specific year without manually entering each date. The user selects the desired year, clicks "Fetch from web", and the system retrieves all Serbian public (national) holidays from timeanddate.com for that year. The fetched holidays are merged into the existing holiday list — existing entries for the same dates are preserved (de-duplicated by date), and only genuinely new holidays are added.

**Why this priority**: This is the core value proposition — eliminating tedious manual data entry of 9–12 holiday dates per year while ensuring financial calculation accuracy through authoritative holiday data.

**Independent Test**: Can be fully tested by selecting year 2026, clicking "Fetch from web", and verifying that the holiday list populates with the correct Serbian national holidays (e.g., Nova godina, Božić, Sretenje, Praznik rada). Delivers immediate value as a standalone feature.

**Acceptance Scenarios**:

1. **Given** the Holidays settings page is open and the user has selected year 2026, **When** the user clicks "Fetch from web", **Then** the system fetches Serbian national holidays for 2026, displays them in the holiday grid, and marks the data as unsaved changes.
2. **Given** the holiday list already contains entries for 2026-01-01 (Nova godina), **When** the user fetches holidays for 2026, **Then** duplicate dates are not added; only holidays not already present (by date) are merged in.
3. **Given** the user is on the Holidays settings page, **When** the user clicks "Fetch from web", **Then** a loading indicator appears during the fetch and the button is disabled until the operation completes.
4. **Given** the fetch completes successfully, **When** the results are displayed, **Then** a success message shows the count of holidays fetched (e.g., "Fetched 9 holidays for 2026").

---

### User Story 2 — Handle Fetch Errors Gracefully (Priority: P1)

When the fetch fails — due to no internet, the website being unavailable, or the page structure having changed — the user sees a clear, actionable error message. No existing holiday data is lost or modified on failure. The user can retry the operation.

**Why this priority**: Equal to P1 because fetch errors will occur in real use (offline scenarios, site changes) and silent failures risk financial miscalculation if the user believes holidays were fetched when they weren't.

**Independent Test**: Can be tested by disconnecting from the internet and clicking "Fetch from web"; verify that an error message appears, no data is lost, and the button re-enables for retry.

**Acceptance Scenarios**:

1. **Given** the device has no internet connection, **When** the user clicks "Fetch from web", **Then** an error message is displayed (e.g., "Failed to fetch holidays: no internet connection") and the existing holiday list remains unchanged.
2. **Given** the remote page returns an unexpected structure (no holidays table found), **When** the fetch completes, **Then** the system displays "Could not find holiday data on the page — the website structure may have changed" and no data is modified.
3. **Given** the fetch returns zero national holidays for the selected year, **When** the operation completes, **Then** the system displays "No national holidays found for year {year}" and existing data is untouched.
4. **Given** a fetch fails, **When** the error is displayed, **Then** the "Fetch from web" button re-enables so the user can retry.

---

### User Story 3 — Fetch Holidays for Multiple Years (Priority: P2)

A user wants to populate holidays for the entire configured year range (e.g., 2024–2028) in one action. Rather than fetching one year at a time, the "Fetch from web" button fetches holidays for all years within the currently configured Start Year–End Year range, merging results into the existing list.

**Why this priority**: Convenience enhancement over P1 (single-year fetch). Users managing multi-year tax periods benefit significantly but the feature still works as a single-year fetch if this story is deferred.

**Independent Test**: Can be tested by setting Start Year = 2024 and End Year = 2026, clicking "Fetch from web", and verifying holidays for all three years appear in the grid.

**Acceptance Scenarios**:

1. **Given** Start Year is 2024 and End Year is 2026, **When** the user clicks "Fetch from web", **Then** the system fetches holidays for 2024, 2025, and 2026 sequentially and merges all results into the holiday list.
2. **Given** a multi-year fetch is in progress, **When** one year fails (e.g., 2025 returns no data), **Then** the system continues fetching remaining years, reports partial success (e.g., "Fetched holidays for 2024, 2026. Failed: 2025 — no holidays found"), and merges the successful results.
3. **Given** a multi-year fetch is in progress, **When** the loading indicator is displayed, **Then** progress feedback indicates which year is currently being fetched (e.g., "Fetching 2025…").

---

### User Story 4 — Validate Scraped Dates Before Committing (Priority: P2)

Before merging fetched holidays into the editable list, the system validates each scraped date to ensure it is a valid calendar date within the requested year. Malformed or out-of-range dates are silently excluded. If any dates fail validation, the user sees a warning identifying what was excluded.

**Why this priority**: Financial correctness requires that only valid, well-formed dates enter the holiday list. Invalid dates could corrupt filing deadline calculations.

**Independent Test**: Can be tested by verifying that if the scraper encounters a malformed date string, the holiday entry is excluded and a warning indicates "1 holiday excluded due to invalid date format".

**Acceptance Scenarios**:

1. **Given** the scraped page contains a holiday row with an unparseable date, **When** the system processes the response, **Then** the malformed entry is excluded from the results and a warning message notes the exclusion.
2. **Given** all scraped dates parse successfully, **When** the results are displayed, **Then** every holiday date falls within the requested year (e.g., all dates for a 2026 fetch have year = 2026).
3. **Given** scraped data contains entries that are not "National Holiday" type, **When** the system filters the results, **Then** only national holidays are included in the merge.

---

### Edge Cases

- What happens when the user clicks "Fetch from web" while already fetching? The button is disabled during fetch, preventing double-submission.
- What happens if the remote website changes its HTML structure? The scraper fails gracefully with a parse error and existing data is preserved.
- What happens if the user fetches for a far-future year (e.g., 2099) where no holidays are published? The system returns "No national holidays found for year 2099."
- What happens if the year range spans more than 10 years? The year range validation (existing StartYear/EndYear constraints: max span of 10) limits the scope of multi-year fetches.
- What happens if the same holiday appears with a different name (e.g., site renames "Vidovdan" to "St. Vitus Day")? De-duplication is by date, not by name — the existing entry's name is preserved.
- What happens if fetched holidays partially overlap with manually added entries? Merge preserves manual entries; only new dates are added.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST provide a "Fetch from web" button on the Holidays settings page that triggers a fetch of Serbian public holidays from timeanddate.com.
- **FR-002**: The system MUST substitute the selected year into the URL pattern `https://www.timeanddate.com/holidays/serbia/{year}?hol=1` to construct the fetch request.
- **FR-003**: The system MUST parse the HTML response and extract only rows classified as "National Holiday" from the holidays table.
- **FR-004**: The system MUST parse each holiday's date and name from the scraped HTML and convert dates to the internal date format (DateOnly).
- **FR-005**: The system MUST merge fetched holidays into the existing holiday list, de-duplicating by date — if a date already exists in the list, the existing entry is preserved and no duplicate is added.
- **FR-006**: The system MUST NOT automatically persist fetched holidays to storage. Fetched data appears as unsaved changes in the editable grid, requiring the user to explicitly click "Save".
- **FR-007**: The system MUST display a loading indicator while the fetch is in progress and disable the "Fetch from web" button to prevent concurrent fetches.
- **FR-008**: The system MUST display a success message after a successful fetch, indicating the number of holidays retrieved.
- **FR-009**: The system MUST display a user-friendly error message if the fetch fails (network error, parse error, or no holidays found) without modifying existing holiday data.
- **FR-010**: The system MUST perform all web fetching asynchronously without blocking the user interface.
- **FR-011**: The system MUST validate that each scraped date is a valid calendar date within the requested year before including it in the results.
- **FR-012**: The system MUST only invoke the web fetch on explicit user action (button click) — never automatically, on a schedule, or as a background task.
- **FR-013**: The system MUST support fetching holidays for each year in the configured Start Year–End Year range when multi-year fetch is used.
- **FR-014**: During multi-year fetch, the system MUST continue processing remaining years if one year's fetch fails, and report partial results to the user.

### Constitution Alignment *(mandatory)*

- **CA-001 (Architecture)**: This feature spans all four layers. Domain defines the PublicHoliday entity and HolidayConf value object. Application defines the IHolidayImporter contract, ImportHolidaysFromWebCommand, and handler. Infrastructure implements the HTML scraper behind the IHolidayImporter interface. Desktop wires the "Fetch from web" button to the command handler via ReactiveCommand. Clean Architecture boundaries are preserved — no layer depends inward.
- **CA-002 (Money and Dates)**: All holiday dates use `DateOnly`. No monetary values are involved in this feature. Date parsing from the scraped HTML must produce `DateOnly` values and reject any unparseable strings.
- **CA-003 (Privacy and Security)**: No credentials or sensitive data involved. The fetch is a read-only HTTP GET to a public website. All holiday data is stored locally in the existing SQLite database. No data is sent outbound.
- **CA-004 (Network Scope)**: Single outbound call to `https://www.timeanddate.com/holidays/serbia/{year}?hol=1`. This endpoint is authorized under Constitution amendment CA-EXT-001, which permits timeanddate.com access exclusively on explicit user action.
- **CA-005 (Async and UI)**: HTTP fetch, HTML parsing, and data merge are fully async (`async Task`). The UI uses `ReactiveCommand.CreateFromTask` to avoid blocking. Loading state and button disablement prevent UI freezes and double-submission.
- **CA-006 (Testing Impact)**: Domain — no new tests (existing PublicHoliday entity unchanged). Application — unit tests for ImportHolidaysFromWebCommandHandler with mocked IHolidayImporter. Infrastructure — integration tests for TimeAndDateHolidayScraper against known HTML fixtures. Desktop — ViewModel tests for FetchFromWebCommand behavior (success, failure, loading states, merge logic).

### Key Entities

- **PublicHoliday**: Represents a single holiday entry with a unique identifier, date (DateOnly), display name (max 200 characters), and derived year. The scraped holidays are mapped to this entity before storage.
- **HolidayConf**: Value object holding the complete list of holiday dates. Used by the filing deadline calculator to determine business day adjustments.
- **HolidayYearRange**: Singleton entity defining the configured Start Year–End Year range (2020 minimum, max span of 10 years). Constrains the scope of multi-year web fetches.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users can fetch and populate a full year's Serbian public holidays in under 10 seconds (including network round-trip), compared to 5+ minutes of manual data entry.
- **SC-002**: 100% of fetched holiday dates are valid calendar dates within the requested year — zero invalid dates enter the holiday list.
- **SC-003**: Fetched holidays match the authoritative timeanddate.com listing for Serbian national holidays with 100% accuracy for dates and names.
- **SC-004**: Fetch failures (network error, parse error) never corrupt or modify existing holiday data — the holiday list before and after a failed fetch is identical.
- **SC-005**: Users can complete the entire fetch-and-save workflow (select year → fetch → review → save) in under 30 seconds.
- **SC-006**: Multi-year fetch for a 5-year range completes within 60 seconds and reports partial success if individual years fail.

## Assumptions

- Users have internet connectivity when clicking "Fetch from web"; offline scenarios are handled as errors, not silent fallbacks.
- The timeanddate.com page structure (specifically the `#holidays-table` element with `showrow` class rows) remains stable across years. If the structure changes, the scraper will fail gracefully and require a code update.
- Only "National Holiday" type entries from timeanddate.com are relevant. Other holiday types (observances, religious holidays not officially designated) are excluded.
- The existing truncate-and-insert save pattern continues to be used — fetched holidays are merged into the in-memory editable list, and the "Save" button persists the entire list (replacing all stored holidays).
- Serbian locale date formatting ("d MMM" pattern with invariant culture) from timeanddate.com is consistent across years.
- The year range (Start Year–End Year) already configured on the page defines the scope for multi-year fetches. No separate year-range selector is needed for the fetch operation.
- Rate limiting or bot-detection by timeanddate.com is not expected for the low-volume usage pattern of this desktop application (one-off user-triggered fetches). No retry/backoff logic is required in v1.
