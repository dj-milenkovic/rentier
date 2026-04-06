# Feature Specification: Taxpayer Profile Management

**Feature Branch**: `feature/002-taxpayer-profile`  
**Created**: 2026-04-06  
**Status**: Draft  
**Feature Number**: 002

---

## User Scenarios & Testing *(mandatory)*

### User Story 1 — First-Run Profile Setup (Priority: P1)

On first launch the taxpayer opens **Settings → Profile** and sees a blank form.
They enter their JMBG (13 numeric digits), full name, street address, and opstina code.
Optionally they may also enter a phone number and email address.
They press **Save**; the profile is persisted to the local SQLite database.
After restarting the application, re-opening Settings → Profile shows all saved values
pre-populated in the form.

**Why this priority**: The taxpayer profile is the foundational identity record for the
entire application. Without it, PP-OPO filings cannot be generated or pre-filled. Every
downstream feature depends on this data being present.

**Independent Test**: Launch the app against a fresh (empty) database, navigate to
Settings → Profile, fill in all required fields (JMBG, FullName, Address, OpstinaCode),
press Save, restart the application, re-open Settings → Profile, and verify every saved
value is displayed correctly.

**Acceptance Scenarios**:

1. **Given** no profile has ever been saved, **When** the user opens Settings → Profile,
   **Then** the form is displayed with all fields empty and no pre-filled values.
2. **Given** the form contains a valid 13-digit numeric JMBG and all required fields are
   non-empty, **When** the user presses Save, **Then** the profile is written to SQLite
   and a success confirmation is visible in the UI.
3. **Given** a profile was saved in a previous session, **When** the app is restarted and
   Settings → Profile is opened, **Then** the form is pre-populated with the previously
   saved values.

---

### User Story 2 — Edit Existing Profile (Priority: P2)

The taxpayer opens **Settings → Profile**, sees their previously saved data, modifies one
or more fields (e.g., updates their address after moving), and presses **Save**.
The existing record is updated in-place; no duplicate record is created.

**Why this priority**: Tax identity data changes over time — address moves, phone number
updates. Editing must be reliable and must not produce orphaned or duplicate records that
could corrupt future filings.

**Independent Test**: Save a profile, re-open Settings → Profile, change the Address
field, press Save, then reopen the view and confirm only the updated address is stored
(same number of rows in the database as before the edit).

**Acceptance Scenarios**:

1. **Given** a profile already exists, **When** the user changes the address field and
   presses Save, **Then** the repository updates the existing record (preserving its `Id`)
   rather than inserting a new row.
2. **Given** a profile already exists, **When** the user clears a required field and
   presses Save, **Then** validation blocks the save and an inline error is displayed
   next to the invalid field.
3. **Given** a profile already exists and optional fields were previously populated,
   **When** the user clears PhoneNumber and Email and saves, **Then** those fields are
   stored as `NULL` in SQLite and the required fields remain unchanged.

---

### User Story 3 — JMBG Validation Feedback (Priority: P2)

When the user types a JMBG value that is not exactly 13 numeric digits, the **Save**
button is disabled and an inline error message appears next to the field — before any
data is dispatched to the Application layer.

**Why this priority**: JMBG is the primary identity key used in PP-OPO XML output.
Invalid values must be surfaced immediately to prevent corrupted filings.

**Independent Test**: Type a 12-digit string into the JMBG field; verify the Save button
is disabled and an inline error is shown. Type a 13-digit numeric string; verify the
Save button becomes enabled (assuming all other required fields are valid).

**Acceptance Scenarios**:

1. **Given** the JMBG field contains fewer or more than 13 characters, **When** the user
   inspects the field, **Then** an inline validation error reads "JMBG must be exactly
   13 digits" and the Save button is disabled.
2. **Given** the JMBG field contains exactly 13 non-numeric characters (e.g., letters or
   special characters), **When** the user attempts to save, **Then** the save is blocked
   and the same inline error is displayed.
3. **Given** the JMBG field contains exactly 13 numeric digits and all required fields
   are valid, **When** the user inspects the form, **Then** no JMBG validation error
   is shown and the Save button is enabled.

---

### Edge Cases

- **Empty database on first run**: `GetAsync()` returns `null`; the UI renders a blank
  form; the save path creates a new record with a freshly generated `Id`.
- **JMBG boundary values**: 12-digit input rejected; 14-digit input rejected; letters,
  special characters, and whitespace-only strings rejected.
- **Whitespace-only required fields**: `FullName`, `Address`, or `OpstinaCode` containing
  only spaces must be rejected by the Domain constructor; the ViewModel surfaces this as
  a user-visible inline error.
- **Optional fields empty on save**: `PhoneNumber` and `Email` may be `null` or empty
  string; persisted as `NULL` in SQLite.
- **Navigation away with unsaved changes**: Out of scope for this feature; dirty-state
  warning is deferred to a future UX pass.
- **Profile deletion from UI**: Explicitly out of scope; `DeleteAsync` remains on the
  interface for future use only.
- **Concurrent save attempts**: Not applicable — single-user, single-process desktop
  application; no concurrent access scenario exists.

---

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST store exactly one `TaxpayerProfile` record per app
  instance (singleton). A second profile record MUST NOT be created under any
  circumstances.
- **FR-002**: The system MUST persist all `TaxpayerProfile` attributes — `Id`, `Jmbg`,
  `FullName`, `Address`, `OpstinaCode`, `PhoneNumber`, `Email` — in the local SQLite
  database via the ORM layer.
- **FR-003**: The `TaxpayerProfile` domain entity MUST be enriched with two new nullable
  fields: `PhoneNumber` (string?, optional) and `Email` (string?, optional).
- **FR-004**: `SaveTaxpayerProfileCommand` MUST perform an upsert: if no profile exists,
  a new record is inserted with a newly generated `Id`; if one exists, the existing record
  is updated in-place by `Id`.
- **FR-005**: `GetTaxpayerProfileQuery` MUST return the single saved profile as a
  `TaxpayerProfileDto`, or `null` when no profile has been saved.
- **FR-006**: The `Jmbg` field MUST be validated in the Domain constructor: the value
  MUST be exactly 13 characters, all numeric. Any other value MUST cause the constructor
  to raise a domain validation error.
- **FR-007**: `FullName`, `Address`, and `OpstinaCode` MUST NOT be null, empty, or
  whitespace; this constraint MUST be enforced in the Domain constructor.
- **FR-008**: `PhoneNumber` and `Email` are optional; they MAY be null or empty string
  without any format validation in this feature version.
- **FR-009**: The Settings screen MUST be refactored from a placeholder into a
  sub-navigation host with at least one tab: **Profile**. Additional tabs (Mailbox,
  Importer) are reserved for future features and MUST NOT be introduced in this feature.
- **FR-010**: The **Profile** tab MUST contain a form that exposes input controls for all
  seven profile attributes (`Id` is not user-editable), bound to the ViewModel via
  reactive bindings.
- **FR-011**: The Save button MUST be bound to a reactive command; it MUST be disabled
  whenever any required field contains an invalid value.
- **FR-012**: The persistence layer MUST include a `TaxpayerProfiles` table, configured
  via the ORM's entity-configuration API, with a new schema migration.
- **FR-013**: `Jmbg` uniqueness MUST be enforced at the database level via a unique index
  declared in the entity configuration.
- **FR-014**: All I/O operations on the profile (load and save) MUST be asynchronous;
  no blocking calls are permitted anywhere in the call stack.
- **FR-015**: The feature MUST NOT make any outbound network calls.
- **FR-016**: All user-visible strings displayed in the Profile tab MUST be sourced from
  the application's string resource file; no hard-coded text is permitted in view markup
  or view code-behind.

### Constitution Alignment *(mandatory)*

- **CA-001 (Architecture)**: Four layers are touched, and all Clean Architecture
  boundaries are preserved:
  - **Domain** (`Rentier.Domain`): `TaxpayerProfile` entity enriched with `PhoneNumber`
    and `Email`; all validation rules remain in the constructor; no I/O packages
    referenced.
  - **Application** (`Rentier.Application`): `SaveTaxpayerProfileCommand` + Handler and
    `GetTaxpayerProfileQuery` + Handler added; both consume the existing
    `ITaxpayerProfileRepository` interface; handlers return `Result<TaxpayerProfileDto>`
    / `Result<Unit>` for expected failures; no EF Core or infrastructure references.
  - **Infrastructure** (`Rentier.Infrastructure`): `AppDbContext` gains
    `DbSet<TaxpayerProfile>` and Fluent API configuration; `TaxpayerProfileRepository`
    implements `ITaxpayerProfileRepository`; a new EF migration
    (`0002_TaxpayerProfile`) is generated.
  - **Desktop** (`Rentier.Desktop`): `SettingsView` and `SettingsViewModel` (or a
    dedicated `ProfileSettingsViewModel`) wired to Application use cases via DI; no
    direct repository or infrastructure access from the Desktop layer.
- **CA-002 (Money and Dates)**: `TaxpayerProfile` contains no monetary or date fields;
  `decimal` and `DateOnly` rules are not triggered by this feature.
- **CA-003 (Privacy and Security)**: All profile data is stored locally in SQLite. No
  IMAP credentials or OS-level secrets are involved. `Jmbg` is sensitive identity data;
  raw JMBG values MUST NOT be written to application logs or diagnostic output.
- **CA-004 (Network Scope)**: No outbound network calls are made; this feature is
  entirely offline.
- **CA-005 (Async and UI)**: `SaveAsync` and `GetAsync` on the repository are
  `async Task` / `async Task<T>`; Application handlers are `async Task<Result<T>>`;
  the ViewModel Save command uses `ReactiveCommand.CreateFromTask`; UI state updates
  are scheduled via `RxApp.MainThreadScheduler`; `.Result` and `.Wait()` are prohibited.
- **CA-006 (Testing Impact)**:
  - **Domain tests** (100% rule/state coverage): JMBG valid (exactly 13 digits),
    JMBG invalid — short (12), long (14), non-numeric, whitespace-only;
    `FullName` / `Address` / `OpstinaCode` null and whitespace rejection;
    `PhoneNumber` / `Email` nullable acceptance (null and empty string both valid).
  - **Application tests** (≥ 90% coverage): `SaveTaxpayerProfileCommand` — insert path
    (null profile from repository), update path (existing profile returned), validation
    error path (invalid JMBG or missing required field); `GetTaxpayerProfileQuery` —
    existing profile returned as DTO, null profile returns null DTO.
    All infrastructure dependencies mocked with NSubstitute.
  - **Infrastructure tests**: Integration test using SQLite in-memory provider verifying
    JMBG unique index enforcement and upsert behaviour (insert then update same row).
  - **Desktop tests**: `ProfileSettingsViewModel` (or `SettingsViewModel`) unit tests
    covering JMBG field inline validation, Save command enabled/disabled transitions,
    and successful save flow (including `IsLoading` and `ErrorMessage` state).

### Key Entities

- **`TaxpayerProfile`** (Domain Entity): Represents the solo taxpayer's Serbian tax
  identity. Attributes: `Guid Id` (unique identifier, system-generated), `string Jmbg`
  (13 numeric digits, unique across the table), `string FullName` (required),
  `string Address` (required), `string OpstinaCode` (required), `string? PhoneNumber`
  (optional, free-text), `string? Email` (optional, free-text). At most one instance
  exists in the database at any time.
- **`TaxpayerProfileDto`** (Application DTO): A read-only record mirroring all seven
  attributes of `TaxpayerProfile`, used to transfer data between the Application layer
  and the Desktop layer without exposing the Domain entity directly.

---

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A user can complete initial profile entry (all 5 required fields plus 0–2
  optional fields) and confirm persistence within 60 seconds of first opening
  Settings → Profile on a standard development machine.
- **SC-002**: JMBG validation rejects 100% of non-13-digit and non-numeric inputs with
  zero false negatives or false positives, as verified by the Domain unit test suite.
- **SC-003**: The save operation (write to local database and return confirmation in the
  UI) completes in under 200 ms on a device with an idle local database, as measured in
  the Infrastructure integration test.
- **SC-004**: The Domain test suite achieves 100% rule and state coverage for
  `TaxpayerProfile`; the Application test suite achieves ≥ 90% coverage for the two new
  handlers (`SaveTaxpayerProfileCommandHandler`, `GetTaxpayerProfileQueryHandler`).
- **SC-005**: No compiler warnings are introduced; CI remains green on both Windows and
  macOS matrix targets after the schema migration is applied.

---

## Assumptions

- The application is installed and used by a single person on a single machine; no
  multi-user or multi-device scenarios exist for this feature.
- The local database file already exists and schema migrations are applied automatically
  on application startup (or via an explicit migration step on first run).
- `ITaxpayerProfileRepository.SaveAsync` is the canonical upsert contract; the
  Infrastructure implementation determines internally whether to insert or update based
  on entity tracking state.
- `SettingsViewModel` will be refactored to host a `ProfileSettingsViewModel` child
  ViewModel; the existing placeholder property can be removed as part of this feature.
- Navigation to the Settings section is already wired in the main application shell;
  this feature only replaces the placeholder content, not the shell navigation entry.
- No data migration from a prior version is required; this is the first feature to
  introduce a persistent `TaxpayerProfiles` table.
- `PhoneNumber` and `Email` format validation (regex, maximum length) is deferred to a
  future enhancement; this version treats them as free-text nullable strings.
- `Jmbg` is stored as a plain string in SQLite (no column-level encryption); full
  database encryption is out of scope for this feature.
- The sub-navigation control used for the Settings tabs is Avalonia's built-in
  `TabControl`; visual styling follows the existing `FluentTheme` without customisation
  in this feature.
- The `DeleteAsync` method remains on `ITaxpayerProfileRepository` but is not surfaced
  in the UI; it is reserved for a future maintenance feature.
