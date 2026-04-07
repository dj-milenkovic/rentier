# Feature Specification: Holiday Configuration

**Feature Branch**: `feature/003-holiday-configuration`  
**Created**: 2026-04-06  
**Status**: Draft  
**Feature Number**: 003

---

## User Scenarios & Testing *(mandatory)*

### User Story 1 — View and Edit Holidays (Priority: P1)

The taxpayer opens **Settings → Holidays** and sees all persisted public holidays displayed
in an editable DataGrid, ordered by date. Each row shows the holiday date and name. The
taxpayer can modify any row inline — changing a date or a name — and press **Save** to
persist the changes to the local SQLite database. On the next app launch, the edited
holidays are loaded back into the DataGrid exactly as saved.

**Why this priority**: The full holiday list is the primary reference data that deadline
calculations depend on. Without it, PP-OPO filing deadlines cannot be adjusted for
non-working days. Viewing and persisting holidays is the foundational CRUD operation that
all other stories in this feature build on.

**Independent Test**: Launch the app against a seeded database, open Settings → Holidays,
verify all holidays appear in the DataGrid, edit one holiday name inline, press Save,
restart the application, re-open Settings → Holidays, and verify the updated name is
displayed.

**Acceptance Scenarios**:

1. **Given** the database contains persisted holidays, **When** the user opens
   Settings → Holidays, **Then** all holidays are shown in the DataGrid ordered by date,
   with editable Date and Name columns.
2. **Given** the DataGrid contains existing rows, **When** the user edits a holiday name
   inline and presses Save, **Then** the updated name is persisted to SQLite and
   visible after restarting the application.
3. **Given** the user edits a row but has not yet pressed Save, **When** they inspect the
   header area, **Then** an "unsaved changes" indicator is visible in the UI.
4. **Given** there are unsaved edits, **When** the user navigates away from the Holidays
   tab without saving, **Then** changes are silently discarded with no blocking dialog.

---

### User Story 2 — Add and Delete Holiday Rows (Priority: P2)

The taxpayer clicks **Add** to insert a blank row at the bottom of the DataGrid, fills in
a date and a name, then presses **Save** to persist the new holiday. Separately, the
taxpayer selects an existing row and clicks **Delete** to remove it from the list, then
presses **Save** to commit the deletion.

**Why this priority**: Holiday lists change year over year and may need corrections (e.g.,
moved public holidays, bank holidays). Add and Delete are the complementary CRUD
operations without which the editable grid has no practical value.

**Independent Test**: Open Settings → Holidays, click Add, enter a date of `2025-01-07`
and name `Orthodox Christmas`, press Save, reopen the tab, and confirm the new row is
present. Then select that row, click Delete, press Save, reopen the tab, and confirm
the row is gone.

**Acceptance Scenarios**:

1. **Given** the Holidays tab is open, **When** the user clicks Add, **Then** a new blank
   row is appended to the DataGrid with focus on the Date cell.
2. **Given** a new row has been filled with a valid date and name, **When** the user
   presses Save, **Then** the new holiday is persisted to SQLite and survives an app
   restart.
3. **Given** the user enters a date that duplicates an existing holiday date, **When** the
   user attempts to save, **Then** the save is blocked and an inline error message
   indicates that duplicate dates are not allowed.
4. **Given** the user selects an existing row and clicks Delete, **When** the row is
   removed from the DataGrid and Save is pressed, **Then** that holiday is permanently
   removed from the database.
5. **Given** no row is selected, **When** the user clicks Delete, **Then** nothing happens
   (the command does not execute).

---

### User Story 3 — Year Range Configuration (Priority: P3)

The taxpayer views the **Start Year** and **End Year** inputs on the Holidays tab. They
update the end year to extend the holiday window (e.g., from 2028 to 2030) and press
**Save**. The year range is persisted and applied to any future holiday queries.

**Why this priority**: The year range defines which years are actively managed for holiday
data. Without a configurable range, users cannot prepare holiday data for upcoming filing
years. This is a configuration complement to the holiday list itself.

**Independent Test**: Open Settings → Holidays, change EndYear to `StartYear + 5`, press
Save, restart the application, reopen the tab, and verify both year values are unchanged.

**Acceptance Scenarios**:

1. **Given** the Holidays tab is open, **When** the user views the StartYear and EndYear
   fields, **Then** the currently persisted range values are pre-populated.
2. **Given** the user sets a valid range (StartYear ≥ 2020 and EndYear ≤ StartYear + 10),
   **When** Save is pressed, **Then** the new range is persisted and loaded back correctly
   after an app restart.
3. **Given** the user sets StartYear below 2020, **When** attempting to save, **Then**
   the save is blocked and an inline validation error is shown.
4. **Given** the user sets EndYear greater than StartYear + 10, **When** attempting to
   save, **Then** the save is blocked and an inline validation error is shown.

---

### User Story 4 — Import Holidays from Web (Priority: P4)

The taxpayer clicks **Import from Web**, is prompted to enter a year, and the application
fetches and parses the Serbian public holiday list for that year from the configured web
source. The imported holidays are loaded into the DataGrid for review. The taxpayer
inspects the imported rows, optionally removes or edits any, and then presses **Save** to
persist them. If the import fails (network error, timeout, unexpected page structure),
an inline error message is shown and the DataGrid contents are left unchanged.

**Why this priority**: Manual entry of 10–15 holidays per year is error-prone. Web import
eliminates transcription errors while keeping the taxpayer in control of what gets saved.
It is deprioritised relative to basic CRUD because the feature is fully functional without
it, but it significantly reduces administrative overhead.

**Independent Test**: Open Settings → Holidays, click Import from Web, enter year `2025`,
verify a non-empty list of holidays is loaded into the DataGrid (without being saved), then
press Save and verify the holidays appear after an app restart. Separately, simulate a
network failure and confirm an inline error appears with DataGrid unchanged.

**Acceptance Scenarios**:

1. **Given** the user clicks Import from Web and enters a valid year, **When** the import
   succeeds, **Then** the DataGrid is populated with the fetched holidays and the Save
   button becomes active.
2. **Given** holidays are loaded into the DataGrid via import, **When** the user has NOT
   yet pressed Save, **Then** the database remains unchanged (import does not auto-save).
3. **Given** the user imports holidays and then presses Save, **Then** those holidays are
   persisted to SQLite and survive an app restart.
4. **Given** the web source is unreachable (DNS failure, HTTP 5xx, timeout), **When** the
   import is attempted, **Then** an inline error message is shown and the existing DataGrid
   contents remain intact.
5. **Given** the web page returns no visible holiday rows, **When** the import completes,
   **Then** the DataGrid shows an empty list and an informational message indicates no rows
   were found.
6. **Given** the application is in any state, **When** the Import from Web action is NOT
   explicitly triggered by the user, **Then** no outbound HTTP request to timeanddate.com
   or any other third-party host is made.

---

### Edge Cases

- **First-run with empty database**: No `HolidayYearRange` singleton exists; the app seeds
  the current-year Serbian holidays and creates the year range record before displaying the
  tab.
- **Deliberate save of empty list**: If the user clears all rows and presses Save, the
  empty list is persisted; no re-seeding occurs because `HolidayYearRange` already exists.
- **Duplicate date on add**: Entering a date that already exists in the DataGrid must be
  caught on save; the `HolidayConf` value object enforces the no-duplicate-dates invariant.
- **Year range boundary at exact limits**: `StartYear = 2020` and `EndYear = 2020` is
  valid; `StartYear = 2019` or `EndYear = StartYear + 11` must throw a `DomainException`.
- **Import of a year already partially in the DataGrid**: Imported rows replace the
  current DataGrid contents (not merged); the user reviews and saves the combined result.
- **Import timeout**: Long-running HTTP requests must not block the UI thread; the
  `IsLoading` indicator is shown until the operation completes or fails.
- **Import returns hidden rows only**: The scraper filters out CSS-hidden `<tr>` elements;
  if only hidden rows are present, the result is an empty list (treated as no rows found).
- **Navigation away during in-flight import**: If the user navigates away while an import
  is running, the operation is cancelled via `CancellationToken`; no data is written.

---

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The Settings screen MUST include a **Holidays** tab (second tab after
  Profile) that displays all persisted public holidays in an editable DataGrid with
  **Date** and **Name** columns.
- **FR-002**: The user MUST be able to add a new blank holiday row to the DataGrid by
  clicking the **Add** button; focus MUST be placed on the Date cell of the new row.
- **FR-003**: The user MUST be able to edit an existing holiday row inline within the
  DataGrid (both Date and Name fields are editable without entering a separate edit mode).
- **FR-004**: The user MUST be able to delete the selected holiday row by clicking the
  **Delete** button; if no row is selected the Delete button MUST be disabled.
- **FR-005**: The user MUST be able to press **Save** to persist the current DataGrid
  contents (all rows, year range) to the local SQLite database.
- **FR-006**: The `HolidayConf` value object MUST enforce a no-duplicate-dates invariant:
  if the holiday list passed to its constructor contains two or more identical `DateOnly`
  values, the constructor MUST throw a `DomainException`.
- **FR-007**: The year range (`StartYear`, `EndYear`) MUST be configurable via numeric
  input fields displayed on the Holidays tab and persisted as part of the Save action.
- **FR-008**: All holiday and year range data MUST persist to the local SQLite database
  and be fully restored on application restart with no data loss.
- **FR-009**: On the application's first run, if no `HolidayYearRange` record exists in
  the database, the system MUST automatically seed the current calendar year's Serbian
  public holidays and create the `HolidayYearRange` singleton record before displaying
  the Holidays tab.
- **FR-010**: The user MUST be able to trigger a web import by clicking **Import from Web**,
  entering the target year, and receiving the parsed holiday list loaded into the DataGrid.
- **FR-011**: Web import MUST load holidays into the DataGrid only; the database MUST NOT
  be modified until the user explicitly presses **Save** after a successful import.
- **FR-012**: If a web import fails (network unreachable, HTTP error, parse failure, or
  timeout), the system MUST display an inline error message bound to `ErrorMessage` in
  the ViewModel, and the DataGrid contents MUST remain unchanged.
- **FR-013**: The web scraper MUST exclude rows that are hidden by CSS (rows carrying the
  hidden-state CSS class in the source HTML); only visible table rows are included in
  the returned holiday list.
- **FR-014**: `GetHolidayConfQuery` (no parameters) MUST return a `HolidayConfDto`
  containing the full holiday list (`IReadOnlyList<HolidayEntryDto>`) and the persisted
  `StartYear` and `EndYear` values.
- **FR-015**: `SaveHolidayConfCommand` MUST replace all existing `PublicHoliday` rows in
  the database with the rows supplied in the command (truncate-and-insert pattern) and
  MUST upsert the `HolidayYearRange` singleton record in the same operation.
- **FR-016**: All date values throughout the feature MUST use `DateOnly`; `DateTime` and
  `DateTimeOffset` are prohibited for holiday dates.
- **FR-017**: The system MUST NOT initiate any outbound network request to timeanddate.com
  or any other third-party host unless the user has explicitly clicked **Import from Web**
  in the current session.
- **FR-018**: All user-visible strings displayed in the Holidays tab MUST be sourced from
  `Resources/Strings.resx`; no hard-coded text is permitted in view markup or view
  code-behind.
- **FR-019**: The `HolidayYearRange` entity MUST validate that `StartYear >= 2020` and
  `EndYear <= StartYear + 10`; any violation MUST cause the entity constructor to throw a
  `DomainException` with a descriptive message.
- **FR-020**: Each `PublicHoliday` entity instance MUST have a unique `DateOnly` value
  within the stored dataset; the `HolidayConf` value object constructor enforces this
  invariant, and the `SaveHolidayConfCommandHandler` MUST propagate the resulting
  `DomainException` as an Application-layer `Error` result.

---

### Constitution Alignment *(mandatory)*

- **CA-001 (Architecture)**: All four Clean Architecture layers are touched; inward-only
  dependencies are strictly preserved:
  - **Domain** (`Rentier.Domain`): `HolidayConf` value object gains a no-duplicate-dates
    invariant. Two new entities are introduced — `PublicHoliday` (`Guid Id`, `DateOnly Date`,
    `string Name`, `int Year`) and `HolidayYearRange` (singleton, `int StartYear`,
    `int EndYear`). All invariant checks remain in constructors; no I/O references are
    introduced.
  - **Application** (`Rentier.Application`): `GetHolidayConfQuery` + Handler,
    `SaveHolidayConfCommand` + Handler, `ImportHolidaysFromWebCommand` + Handler are added.
    Two new repository/service interfaces are defined here — `IHolidayRepository` and
    `IHolidayImporter`. No EF Core or MailKit references are permitted.
  - **Infrastructure** (`Rentier.Infrastructure`): `HolidayRepository` implements
    `IHolidayRepository` using `AppDbContext`. `TimeAndDateHolidayScraper` implements
    `IHolidayImporter` using `HttpClient` and AngleSharp. EF Core entity configurations
    for `PublicHoliday` and `HolidayYearRange` and migration `0003_HolidayConfiguration`
    are added here. AngleSharp is referenced only from this project.
  - **Desktop** (`Rentier.Desktop`): `HolidaySettingsViewModel` and `HolidayEntryViewModel`
    call Application use cases via DI. `SettingsViewModel` gains a `HolidayTab` property.
    `SettingsView.axaml` gains a "Holidays" tab hosting `HolidaySettingsView.axaml`. No
    direct repository or infrastructure access from the Desktop layer.
- **CA-002 (Money and Dates)**: No monetary values are involved in this feature.
  All holiday dates use `DateOnly` exclusively; `DateTime` and `DateTimeOffset` are
  prohibited for any holiday date field at any layer boundary.
- **CA-003 (Privacy and Security)**: All data is stored locally in SQLite. No user
  credentials or OS-level secrets are involved. The timeanddate.com URL is a public,
  unauthenticated endpoint; no API key, username, or password is required or stored.
  The URL is hard-coded in the Infrastructure scraper class as a public constant; it
  MUST NOT be sourced from user input.
- **CA-004 (Network Scope)**: This feature introduces a single new outbound HTTP
  endpoint — `https://www.timeanddate.com/holidays/serbia/{year}?hol=1` — governed by
  constitution amendment **CA-EXT-001** (see below). No other outbound calls are
  introduced. The scraper is invoked exclusively when the user clicks **Import from Web**.
  Background polling or automatic refresh are explicitly prohibited.
- **CA-005 (Async and UI)**: All repository and scraper operations are `async Task<T>`.
  Application handlers are `async Task<Result<T, Error>>`. The ViewModel `SaveCommand`,
  `ImportFromWebCommand`, `AddCommand`, and `DeleteSelectedCommand` all use
  `ReactiveCommand.CreateFromTask`. UI state transitions (`IsLoading`, `ErrorMessage`,
  `HasUnsavedChanges`) are scheduled via `RxApp.MainThreadScheduler`. `.Result` and
  `.Wait()` are prohibited throughout the call stack.
- **CA-006 (Testing Impact)**:
  - **Domain tests** (100% rule/state coverage): `HolidayConf` — valid list accepted,
    duplicate date rejected with `DomainException`, empty list accepted. `HolidayYearRange`
    — valid range constructed, `StartYear < 2020` throws `DomainException`, `EndYear >
    StartYear + 10` throws `DomainException`, boundary values `StartYear = 2020` and
    `EndYear = StartYear + 10` accepted.
  - **Application tests** (≥ 90% coverage): `GetHolidayConfQueryHandler` — holidays and
    year range returned as `HolidayConfDto`, empty holiday list handled. `SaveHolidayConf
    CommandHandler` — first-run seeding path (no `HolidayYearRange` row), update path
    (existing range), duplicate-date propagation, invalid year range propagation.
    `ImportHolidaysFromWebCommandHandler` — success path (list returned, no DB write),
    importer failure propagated as `Error`. All infrastructure dependencies mocked with
    NSubstitute.
  - **Infrastructure tests**: Integration tests using SQLite in-memory provider verifying
    truncate-and-insert for `PublicHoliday`, upsert for `HolidayYearRange`, and round-trip
    persistence of `DateOnly` values.
  - **Desktop tests**: `HolidaySettingsViewModel` — `IsLoading` transitions during save
    and import, `ErrorMessage` set on import failure, `HasUnsavedChanges` set after edit,
    `SaveCommand` disabled when DataGrid contains a duplicate date, `DeleteSelectedCommand`
    disabled when no row is selected.

### Constitution Amendment

- **CA-EXT-001 (timeanddate.com Exception)**: The `IHolidayImporter` interface and
  `TimeAndDateHolidayScraper` implementation introduce outbound HTTP access to
  `https://www.timeanddate.com` — a third-party public website outside the previously
  approved endpoints (IMAP and NBS exchange rates). This is approved as a **user-initiated,
  on-demand exception only**. The scraper MUST NOT be invoked automatically, on a schedule,
  or in response to any application lifecycle event. It is invoked exclusively when the
  user explicitly clicks **Import from Web** and confirms the target year. No data returned
  by the scraper is persisted without an additional explicit user action (Save). This
  amendment must be recorded in the project constitution when it is next amended.

### Key Entities

- **`PublicHoliday`** (Domain Entity): Represents a single named public holiday on a
  specific calendar date. Attributes: `Guid Id` (unique identifier, system-generated),
  `DateOnly Date` (the holiday date), `string Name` (human-readable holiday name, required,
  non-empty), `int Year` (calendar year derived from `Date`). Must not share a `Date`
  value with another `PublicHoliday` in the same persisted set.
- **`HolidayYearRange`** (Domain Entity — Singleton): Represents the configured start and
  end year for the holiday management window. Attributes: `int Id` (always `1`, singleton),
  `int StartYear` (earliest year in scope, ≥ 2020), `int EndYear` (latest year in scope,
  ≤ StartYear + 10). Invalid values throw `DomainException` in the constructor.
- **`HolidayConf`** (Domain Value Object — existing, amended): An immutable list of
  `DateOnly` holiday dates used for filing deadline calculations. Gains a new invariant:
  duplicate `DateOnly` entries in the constructor argument throw `DomainException`.
- **`HolidayEntryDto`** (Application DTO): Immutable record transferring a single
  holiday entry between layers. Attributes: `DateOnly Date`, `string Name`.
- **`HolidayConfDto`** (Application DTO): Immutable record transferring the full holiday
  configuration. Attributes: `IReadOnlyList<HolidayEntryDto> Holidays`, `int StartYear`,
  `int EndYear`.

---

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A user can open Settings → Holidays, view all seeded holidays, edit one row,
  and confirm persistence with a single Save action — completing the full round-trip within
  30 seconds of opening the tab on a standard development machine.
- **SC-002**: Year range validation rejects 100% of inputs where `StartYear < 2020` or
  `EndYear > StartYear + 10`, with zero false negatives, as verified by Domain unit tests.
- **SC-003**: The duplicate-date invariant in `HolidayConf` blocks 100% of save attempts
  containing duplicate `DateOnly` values, as verified by Domain and Application unit tests.
- **SC-004**: The Save operation (write all holidays and year range to local database and
  return confirmation in the UI) completes in under 500 ms on a device with an idle local
  database containing up to 50 holiday rows.
- **SC-005**: A successful web import populates the DataGrid with at least one valid
  holiday row within 10 seconds of the user confirming the target year, assuming a
  standard broadband connection and a responsive upstream source.
- **SC-006**: A web import failure (any network or parse error) surfaces a user-readable
  inline error message within 15 seconds and leaves the DataGrid contents unchanged,
  with no unhandled exception or application crash.
- **SC-007**: The Domain test suite achieves 100% rule and state coverage for
  `HolidayConf`, `PublicHoliday`, and `HolidayYearRange`; the Application test suite
  achieves ≥ 90% coverage for all three new command/query handlers.
- **SC-008**: No compiler warnings are introduced; CI remains green on both Windows and
  macOS matrix targets after the schema migration `0003_HolidayConfiguration` is applied.

---

## Assumptions

- (A-001) `HolidayConf` value object in Domain remains structurally unchanged (still
  `IReadOnlyList<DateOnly> Holidays`); only a new no-duplicate-dates invariant is added
  to its constructor. Persistence is handled exclusively via the new `PublicHoliday`
  entity.
- (A-002) `PublicHoliday` attributes are: `Guid Id`, `DateOnly Date`, `string Name`,
  `int Year` (computed from `Date.Year`). The `Year` property aids indexed retrieval by
  calendar year without parsing the date.
- (A-003) `HolidayYearRange` is a singleton entity with `Id = 1`. Only one row ever
  exists in the `HolidayYearRanges` table; subsequent saves perform an upsert on `Id = 1`.
- (A-004) Seeding fires exactly once: when `GetYearRangeAsync()` returns `null` on
  startup. The seeded holidays are the standard Serbian public holidays for the current
  calendar year (New Year Jan 1–2, Sretenje Feb 15–16, Labour Day May 1–2, Vidovdan
  Jun 28, Armistice Day Nov 11, Orthodox Christmas Jan 7).
- (A-005) On Save, all existing `PublicHoliday` rows are deleted and the current DataGrid
  contents are inserted fresh (truncate-and-insert). No soft-delete or merge logic is used.
- (A-006) `IHolidayRepository` is defined in `Rentier.Application` with three members:
  `GetHolidayConfAsync`, `GetYearRangeAsync`, and `SaveHolidaysAsync`.
- (A-007) `IHolidayImporter` is defined in `Rentier.Application` with one member:
  `ImportAsync(int year, CancellationToken) → Task<Result<IReadOnlyList<HolidayEntryDto>, Error>>`.
- (A-008) `TimeAndDateHolidayScraper` fetches `https://www.timeanddate.com/holidays/serbia/{year}?hol=1`
  and uses AngleSharp CSS selectors to parse visible `<tr>` elements from the holiday
  table, excluding rows with a CSS class indicating hidden state.
- (A-009) No background polling, scheduled task, or application lifecycle hook invokes
  the scraper; it is only called from `ImportHolidaysFromWebCommandHandler` in direct
  response to a user-triggered command.
- (A-010) `GetHolidayConfQuery` returns `HolidayConfDto` with all persisted holidays and
  the year range; if the database is empty (post-seed) it returns the seeded set.
- (A-011) `SaveHolidayConfCommand` carries `IReadOnlyList<HolidayEntryDto> Holidays`,
  `int StartYear`, and `int EndYear`. The handler constructs a `HolidayConf` value object
  from the holiday list (triggering the duplicate-date invariant) before persisting.
- (A-012) `HolidayEntryDto` is an immutable `record` with properties `DateOnly Date` and
  `string Name`. It is used as the transfer type in all application commands, queries, and
  the importer interface.
- (A-013) The DataGrid toolbar exposes four buttons: **Add** (inserts blank row), **Delete**
  (removes selected row, disabled when no row is selected), **Import from Web** (prompts
  for year, fires scraper), and **Save** (persists to DB). Year range inputs (StartYear,
  EndYear) are numeric text boxes adjacent to the toolbar.
- (A-014) The `HolidayConf` constructor validates uniqueness of the input `DateOnly` list
  and throws `DomainException("Duplicate holiday dates are not allowed.")` on violation.
- (A-015) `HolidaySettingsViewModel` is added to `SettingsViewModel` as a `HolidayTab`
  property. `SettingsView.axaml` gains a second `TabItem` with header sourced from
  `Strings.resx`.
- (A-016) AngleSharp (`AngleSharp`, version 1.x) is added as a `<PackageReference>` to
  `Rentier.Infrastructure.csproj` only. No other project references AngleSharp.
- (A-017) `HttpClient` for the scraper is registered via
  `services.AddHttpClient<TimeAndDateHolidayScraper>()` in `InfrastructureServiceExtensions`.
  A reasonable timeout (e.g., 15 seconds) is configured at registration time.
- (A-018) The default seed set for the current year covers: New Year (Jan 1, Jan 2),
  Sretenje/Statehood Day (Feb 15, Feb 16), Labour Day (May 1, May 2), Vidovdan (Jun 28),
  Armistice Day (Nov 11), and Orthodox Christmas (Jan 7). Exact date adjustments for
  observed holidays (e.g., when a holiday falls on a weekend) are left to the user via
  the editable DataGrid.
- The application is used by a single person on a single machine; no multi-user, sync, or
  cloud scenarios exist for this feature.
- The local database file already exists and schema migrations are applied automatically
  on application startup; no manual migration step is required of the user.
- Navigation to Settings is already wired in the main application shell; this feature only
  adds the second tab to the existing Settings `TabControl`.
- Web import does not support authentication, session cookies, or browser rendering; it
  performs a plain `GET` request and parses the static HTML response.
- Holiday name format is free-text; no length constraint or character set restriction is
  enforced in this version (deferred to a future UX pass).
