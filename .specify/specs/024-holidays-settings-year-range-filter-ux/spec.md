# Feature Specification: Holidays Settings — Year-Range Filter & UX Improvements

**Feature Branch**: `004-holidays-year-filter-ux`  
**Created**: 2025-07-15  
**Status**: Draft  
**Input**: User description: "Fix the Holidays settings year-range controls and improve page UX. Problems reported by QA: (1) Changing Start Year or End Year has no visible effect on the holidays table — users do not understand what the selectors do. (2) The purpose of the year range is not communicated in the UI."

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Filter Holidays by Year Range (Priority: P1)

A user opens the Holidays settings page and sees a list of all configured holidays. They adjust the Start Year or End Year selector to narrow down which holidays are displayed. The holidays table immediately updates to show only holidays whose dates fall within the selected year range. The user can quickly find and manage holidays for specific years without scrolling through years of irrelevant data.

**Why this priority**: This is the core defect reported by QA — the year-range selectors currently have no visible effect on the holidays table. Fixing this is the primary reason for this feature and delivers immediate user value by making the controls functional.

**Independent Test**: Can be fully tested by loading the Holidays page with holidays spanning multiple years, then changing Start Year or End Year and verifying the table updates accordingly.

**Acceptance Scenarios**:

1. **Given** the holidays list contains entries for years 2024, 2025, and 2026, **When** the user sets Start Year to 2025 and End Year to 2026, **Then** the table displays only holidays from 2025 and 2026.
2. **Given** the holidays list contains entries for years 2024 through 2027, **When** the user changes End Year from 2027 to 2025, **Then** the table immediately updates to show only holidays from years that fall within [Start Year, 2025].
3. **Given** the holidays list contains entries for years 2024 and 2025, **When** the user sets Start Year to 2025 and End Year to 2025, **Then** the table displays only holidays from 2025.
4. **Given** the year range is set to 2024–2026 and the table shows filtered results, **When** the user adds a new holiday entry with a date outside the current range (e.g., year 2023), **Then** that newly added entry does not appear in the filtered table (it remains in the underlying data but is hidden by the filter).
5. **Given** the year range is set to 2024–2026, **When** the user adds a new holiday entry with a date within the range (e.g., 2025-05-01), **Then** the new entry appears in the filtered table immediately.

---

### User Story 2 — Empty State for No Matching Holidays (Priority: P1)

A user adjusts the year range to a period for which no holidays have been configured. Instead of seeing a confusing empty table with no explanation, the user sees a clear placeholder message indicating that no holidays are configured for the selected range. This helps the user understand the table is intentionally empty (due to the filter) rather than broken.

**Why this priority**: Without an empty-state message, users cannot distinguish between "no data loaded" and "no data matches the filter." This directly supports the filtering feature and prevents user confusion, making it equally critical to the filter itself.

**Independent Test**: Can be tested by setting the year range to a period with no holidays and verifying the placeholder message appears.

**Acceptance Scenarios**:

1. **Given** the holidays list contains entries only for 2024 and 2025, **When** the user sets Start Year to 2027 and End Year to 2028, **Then** the table area displays a "No holidays configured for this range" message instead of an empty grid.
2. **Given** the empty-state message is displayed, **When** the user changes the year range back to include years with holidays, **Then** the placeholder disappears and the holidays table reappears with matching entries.
3. **Given** the holidays list is completely empty (no entries at all), **When** the user views the page regardless of year range, **Then** the existing "no holidays configured" placeholder is shown (not the range-specific message).

---

### User Story 3 — Helper Text Explains Year-Range Purpose (Priority: P2)

A user opening the Holidays settings page for the first time sees the year-range selectors but does not understand their purpose. A short helper text below the year selectors explains that the range controls which holidays are displayed and also determines which years are pre-seeded on first run. The user now understands the dual purpose of the year range without needing to consult documentation.

**Why this priority**: This addresses the second QA-reported issue — the purpose of the year range is not communicated. While less critical than making the controls functional (P1), it is essential for discoverability and preventing future confusion.

**Independent Test**: Can be tested by opening the Holidays settings page and verifying the helper text is visible below the year selectors.

**Acceptance Scenarios**:

1. **Given** the user navigates to the Holidays settings page, **When** the page loads, **Then** a helper text is visible below (or near) the year-range selectors explaining their purpose.
2. **Given** the helper text is displayed, **When** the user reads it, **Then** it clearly communicates that the range filters which holidays are shown and determines pre-seeded years on first run.

---

### User Story 4 — Improved Layout with Visual Separation (Priority: P2)

A user looking at the Holidays settings page sees the year-range controls clearly labeled and visually distinct from the holidays table below. A subtle separator (e.g., a horizontal line or spacing) distinguishes the controls area from the data area. The year selector labels ("Start Year" / "End Year") are fully visible and properly aligned at all reasonable window sizes.

**Why this priority**: Layout clarity reinforces the usability of the filtering controls. While the page is functional without this, clear visual hierarchy reduces cognitive load and prevents label clipping issues reported in prior QA cycles.

**Independent Test**: Can be tested by opening the Holidays settings page at various window sizes and verifying labels are visible, aligned, and a separator exists between controls and the grid.

**Acceptance Scenarios**:

1. **Given** the user opens the Holidays settings page, **When** the page renders, **Then** a visual separator is visible between the year-range controls area and the holidays data grid.
2. **Given** the page is displayed at a narrow window width, **When** the user looks at the year-range controls, **Then** the "Start Year" and "End Year" labels are fully visible (no clipping) and aligned consistently.
3. **Given** the helper text and separator are present, **When** the user views the page, **Then** the visual hierarchy clearly groups: (1) toolbar with action buttons, (2) year-range controls with helper text, (3) separator, (4) holidays data grid.

---

### Edge Cases

- What happens when Start Year is set greater than End Year? The filter should return zero results and show the empty-state placeholder. The system should not swap the values automatically or prevent the user from setting this — it simply results in an empty view.
- What happens when the user edits a holiday's date to move it outside the current filter range? The entry should disappear from the filtered view (since it no longer matches), but remain in the underlying data and be visible when the range is adjusted to include that year.
- What happens when holidays are imported for a year outside the current filter range? The imported entries are added to the underlying data but do not appear in the filtered table until the range is adjusted to include the import year.
- What happens when the Entries collection is empty (no holidays at all)? The existing generic empty-state message ("No holidays configured") should display, not the range-specific filter message.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST filter the displayed holidays to show only entries whose date year falls within the inclusive range [Start Year, End Year] whenever Start Year or End Year changes.
- **FR-002**: System MUST perform the year-range filtering entirely in memory against the already-loaded holiday entries, without re-querying the database on each year change.
- **FR-003**: System MUST maintain the full unfiltered list of holiday entries separately from the filtered display list, so that changes to the year range do not discard or lose any data.
- **FR-004**: System MUST update the filtered display immediately (without user action beyond changing the year value) when Start Year or End Year changes.
- **FR-005**: System MUST display a range-specific empty-state placeholder message ("No holidays configured for this range") when the filtered list contains zero entries but the underlying unfiltered list is non-empty.
- **FR-006**: System MUST display the existing generic empty-state message when the underlying unfiltered list itself is empty, regardless of the year-range filter values.
- **FR-007**: System MUST display a localized helper text near the year-range selectors that explains the purpose of the year range (filtering display and determining pre-seeded years).
- **FR-008**: System MUST store the helper text and the range-specific empty-state message as localized resource strings.
- **FR-009**: System MUST render a visual separator between the year-range controls area and the holidays data grid.
- **FR-010**: System MUST ensure the "Start Year" and "End Year" labels are fully visible and consistently aligned at all supported window sizes.
- **FR-011**: When a new holiday entry is added, the system MUST include it in the filtered display if and only if its date year falls within the current [Start Year, End Year] range.
- **FR-012**: When a holiday entry's date is edited, the system MUST re-evaluate whether it belongs in the filtered display based on the updated date year and the current range.

### Constitution Alignment *(mandatory)*

- **CA-001 (Architecture)**: This feature impacts only the Desktop layer (ViewModel and View). No changes to Domain, Application, or Infrastructure layers. The ViewModel derives a filtered collection from the existing Entries collection. Clean Architecture boundaries remain intact — no business logic moves into the View, and no presentation logic enters Application or Domain.
- **CA-002 (Money and Dates)**: Holiday dates use `DateOnly`. Year-range fields are `int`. No monetary fields involved. The filtering compares the year component of `DateOnly` values against integer year bounds — all date handling remains consistent with the constitution.
- **CA-003 (Privacy and Security)**: All holiday data remains local (SQLite). No new data is transmitted externally. Resource strings are compiled into the application. No credentials or secrets are involved.
- **CA-004 (Network Scope)**: No new outbound network calls. This feature operates entirely on in-memory data already loaded from the local database.
- **CA-005 (Async and UI)**: Year-range filter recalculation is a synchronous in-memory operation (filtering a small collection). No I/O is introduced. The reactive pipeline reacts to property changes without blocking the UI thread.
- **CA-006 (Testing Impact)**: Desktop layer unit tests required for the ViewModel filtering logic: verifying FilteredEntries updates correctly when StartYear, EndYear, or Entries change. No Domain, Application, or Infrastructure test changes needed.

### Key Entities

- **Holiday Entry**: Represents a single public holiday with a date (`DateOnly`) and a display name. This is the entity being filtered. Key attribute for this feature: the year component of the date determines filter inclusion.
- **Year Range**: The pair of Start Year and End Year values that define the inclusive bounds for filtering. These values already exist in the ViewModel and are persisted as part of the holiday configuration.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: When a user changes Start Year or End Year, the holidays table updates within 200 milliseconds to show only holidays matching the selected range — users perceive the filtering as instantaneous.
- **SC-002**: 100% of holidays displayed in the table have dates within the selected [Start Year, End Year] range; zero holidays outside the range are shown.
- **SC-003**: Users can identify the purpose of the year-range selectors without external documentation — the helper text is visible on the page at all times.
- **SC-004**: When no holidays match the selected year range, users see an explanatory placeholder message rather than an empty table with no context.
- **SC-005**: Year-range labels ("Start Year" / "End Year") remain fully readable (no clipping or truncation) at window widths down to the application's minimum supported size.
- **SC-006**: A clear visual boundary separates the year-range controls from the holidays data grid, so users can distinguish the filter area from the data area at a glance.

## Assumptions

- The existing Entries collection in the ViewModel is the single source of truth for all holiday data on this page; no secondary data source needs filtering.
- The number of holiday entries is small enough (typically tens to low hundreds) that in-memory filtering introduces no perceptible performance overhead.
- The Start Year and End Year values are always valid integers within a reasonable range (enforced by existing NumericUpDown min/max constraints of 2020–2099).
- The helper text content ("Showing holidays for the selected year range. The range also determines which years are pre-seeded on first run.") is the agreed-upon wording; localization into other languages is not required for this iteration.
- No database schema or Application/Domain layer changes are needed — this is a purely Desktop-layer (ViewModel + View) enhancement.
- The existing save workflow persists all entries in the Entries collection (not just the filtered subset), so filtering does not affect data persistence.
