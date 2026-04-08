# Feature Specification: Taxpayer Profile Management

**Feature Branch**: `feature/002-taxpayer-profile`  
**Created**: 2026-04-06  
**Status**: Draft  
**Feature Number**: 002

---

## User Scenarios & Testing *(mandatory)*

### User Story 1 — First-run profile setup (Priority: P1)

On first launch the taxpayer opens Settings → Profile and sees an empty form.
They enter their JMBG, full name, street address, opstina code, and optionally
phone number and email, then press **Save**. The profile is persisted to the
local SQLite database and survives an app restart.

**Why this priority**: Without a saved profile the app cannot generate or
prefill PP-OPO filings. This is the foundational identity record for the entire
application.

**Independent Test**: Launch the app with a fresh (empty) database, navigate to
Settings → Profile, fill in all required fields, press Save, restart the app,
re-open Settings → Profile, and verify all saved values are re-displayed.

**Acceptance Scenarios**:

1. **Given** no profile has ever been saved, **When** the user opens
   Settings → Profile, **Then** the form is displayed empty with no pre-filled
   values.
2. **Given** the form is filled with a valid 13-digit JMBG and all required
   fields, **When** the user presses Save, **Then** the profile is persisted
   to SQLite and a success confirmation is shown in the UI.
3. **Given** a profile was saved in a previous session, **When** the app is
   restarted and Settings → Profile is opened, **Then** the form is
   pre-populated with the previously saved values.

---

### User Story 2 — Edit existing profile (Priority: P2)

The taxpayer opens Settings → Profile, sees their previously saved data,
modifies one or more fields, and presses Save. The existing record is updated
in-place (upsert); no duplicate record is created.

**Why this priority**: Tax identity data changes over time (address moves,
phone number updates). Editing must not create orphaned records.

**Independent Test**: Save a profile, re-open Settings → Profile, change the
address field, save, and verify the new address is the only stored value when
re-opened.

**Acceptance Scenarios**:

1. **Given** a profile already exists, **When** the user changes the address
   and presses Save, **Then** the repository updates the existing record
   (same `Id`) rather than inserting a new one.
2. **Given** a profile already exists, **When** the user clears a required
   field and presses Save, **Then** validation prevents saving and an inline
   error is shown next to the invalid field.

---

### User Story 3 — JMBG validation feedback (Priority: P2)

When the user enters a JMBG value that is not exactly 13 numeric digits, the
Save button is disabled (or an inline error is shown) before the command is
even dispatched to the Application layer.

**Why this priority**: JMBG is the primary identity key used in PP-OPO XML
output. Invalid values must be caught early to prevent corrupted filings.

**Independent Test**: Type a 12-digit string in the JMBG field and attempt to
save; verify the Save button is disabled or an error message is displayed and
no database write occurs.

**Acceptance Scenarios**:

1. **Given** the JMBG field contains fewer or more than 13 characters,
   **When** the user inspects the field, **Then** an inline validation error
   indicates "JMBG must be exactly 13 digits".
2. **Given** the JMBG field contains 13 non-numeric characters,
   **When** the user attempts to save, **Then** the save is blocked and the
   error is surfaced in the UI.
3. **Given** the JMBG field contains exactly 13 numeric digits,
   **When** all required fields are also valid, **Then** the Save button
   is enabled and submission proceeds without error.

---

### Edge Cases

- **Empty database on first run**: `GetAsync()` returns `null`; the UI
  renders a blank form and routes the save to an insert path.
- **Concurrent save attempts**: Not applicable (single-user, single-process
  desktop app; no concurrency scenario).
- **JMBG boundary**: 12 or 14 digits rejected; letters or special characters
  rejected; whitespace-only rejected.
- **Optional fields empty on save**: `PhoneNumber` and `Email` MAY be `null`
  or empty string; persisted as `NULL` in SQLite.
- **Required fields whitespace-only**: Domain constructor throws
  `DomainException`; ViewModel surfaces this as a user-visible error.
- **Navigation away with unsaved changes**: Out of scope for this feature
  (dirty-state warning deferred to a future UX pass).
- **Profile deletion from UI**: Explicitly out of scope; deletion is rare and
  will be addressed in a future maintenance feature if needed.

---

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST store exactly one `TaxpayerProfile` record per
  app instance (singleton); a second profile MUST NOT be created.
- **FR-002**: The system MUST persist `TaxpayerProfile` data (Id, Jmbg,
  FullName, Address, OpstinaCode, PhoneNumber, Email) in the local SQLite
  database via EF Core.
- **FR-003**: The domain entity `TaxpayerProfile` MUST be enriched with two
  new nullable fields: `PhoneNumber` (string?) and `Email` (string?).
- **FR-004**: `SaveTaxpayerProfileCommand` MUST perform an upsert: if no
  profile exists it inserts a new record; if one exists it updates the
  existing record by `Id`.
- **FR-005**: `GetTaxpayerProfileQuery` MUST return the single profile or
  `null` (represented as `TaxpayerProfileDto?`) when no profile has been saved.
- **FR-006**: The JMBG field MUST be validated in the Domain constructor:
  exactly 13 numeric characters; any other value MUST throw `DomainException`.
- **FR-007**: `FullName`, `Address`, and `OpstinaCode` MUST NOT be null,
  empty, or whitespace; enforced in the Domain constructor.
- **FR-008**: `PhoneNumber` and `Email` are optional; they MAY be null or
  empty; no format validation is required in this feature version.
- **FR-009**: The `SettingsView` MUST be refactored from a placeholder into a
  sub-navigation host with at least one tab: **Profile** (additional tabs such
  as Mailbox and Importer are reserved for future features).
- **FR-010**: The **Profile** tab MUST contain a form bound to
  `SettingsViewModel` (or a dedicated `ProfileSettingsViewModel`) via
  ReactiveUI bindings. The form MUST expose fields for all seven profile
  attributes.
- **FR-011**: The Save button MUST be bound to `SaveTaxpayerProfileCommand`
  via `ReactiveCommand.CreateFromTask`; it MUST be disabled when any required
  field is invalid.
- **FR-012**: `AppDbContext` MUST include `DbSet<TaxpayerProfile>` and a new
  EF Core migration that creates the `TaxpayerProfiles` table.
- **FR-013**: JMBG uniqueness MUST be enforced via a Fluent API unique index
  in the EF entity configuration.
- **FR-014**: The feature MUST NOT make any outbound network calls.

### Constitution Alignment *(mandatory)*

- **CA-001 (Architecture)**: Four layers are touched:
  - **Domain**: `TaxpayerProfile` entity enriched with `PhoneNumber` and
    `Email`; domain validation rules remain enforced in the constructor.
  - **Application**: `SaveTaxpayerProfileCommand`/Handler and
    `GetTaxpayerProfileQuery`/Handler added; both use
    `ITaxpayerProfileRepository` (already defined). Handlers return
    `Result<TaxpayerProfileDto>` / `Result<Unit>`.
  - **Infrastructure**: `AppDbContext` gains `DbSet<TaxpayerProfile>` and
    entity configuration; `TaxpayerProfileRepository` implements the interface.
    A new EF migration is generated.
  - **Desktop**: `SettingsView` / `SettingsViewModel` (or
    `ProfileSettingsViewModel`) wired to Application use cases; no direct
    repository access from the Desktop layer.
- **CA-002 (Money and Dates)**: `TaxpayerProfile` contains no monetary or date
  fields; `decimal` / `DateOnly` rules are not triggered by this feature.
- **CA-003 (Privacy and Security)**: All data stored locally in SQLite. No
  IMAP credentials or secrets involved. JMBG is sensitive identity data;
  no logging of raw JMBG values is permitted.
- **CA-004 (Network Scope)**: No outbound calls; this feature is fully
  offline.
- **CA-005 (Async and UI)**: `SaveAsync` and `GetAsync` on the repository are
  `async Task`; handlers are `async Task<Result<T>>`; the ViewModel Save
  command uses `ReactiveCommand.CreateFromTask`; UI updates are scheduled via
  `RxApp.MainThreadScheduler`.
- **CA-006 (Testing Impact)**:
  - **Domain tests**: Cover JMBG validation (valid, short, long, non-numeric,
    whitespace), FullName/Address/OpstinaCode null/whitespace rejection,
    PhoneNumber/Email nullable acceptance.
  - **Application tests** (≥ 90% coverage): Cover
    `SaveTaxpayerProfileCommand` (insert path, update path, validation error
    path) and `GetTaxpayerProfileQuery` (existing profile, null profile).
    Use NSubstitute mock for `ITaxpayerProfileRepository`.
  - **Infrastructure tests**: Integration test with SQLite in-memory provider
    confirming JMBG unique index and upsert behaviour.
  - **Desktop tests**: `ProfileSettingsViewModel` unit tests covering field
    validation, Save command enabled/disabled state, and successful save flow.

### Key Entities

- **`TaxpayerProfile`** (Domain Entity): Represents the solo taxpayer's
  Serbian tax identity. Attributes: `Guid Id`, `string Jmbg` (13 digits,
  unique), `string FullName` (required), `string Address` (required),
  `string OpstinaCode` (required), `string? PhoneNumber` (optional),
  `string? Email` (optional). Singleton: at most one instance in the
  database.
- **`TaxpayerProfileDto`** (Application DTO / record): Mirrors all seven
  attributes for transport between Application and Desktop layers.

---

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A solo developer can complete initial profile entry (all 5
  required fields + 0–2 optional fields) and confirm persistence within
  60 seconds of first opening Settings → Profile.
- **SC-002**: JMBG validation rejects all non-13-digit or non-numeric inputs
  with 100% accuracy (zero false negatives or false positives in unit tests).
- **SC-003**: Save operation completes in under 200 ms on a device with an
  idle SQLite database (measured in Infrastructure integration test).
- **SC-004**: Domain test suite achieves 100% rule/state coverage for
  `TaxpayerProfile`; Application test suite achieves ≥ 90% coverage for the
  two new handlers.
- **SC-005**: No compiler warnings introduced; CI remains green on both
  Windows and macOS matrix after the migration is applied.

---

## Assumptions

- The app is installed and used by a single person on a single machine; no
  multi-user or multi-device scenarios exist.
- The SQLite database file already exists and EF Core migrations run
  automatically on startup (or via explicit migration apply on first run).
- `ITaxpayerProfileRepository.SaveAsync` is the canonical upsert contract;
  the Infrastructure implementation decides internally whether to call EF
  `Add` or `Update` based on entity tracking state.
- `SettingsViewModel` will be refactored to host a `ProfileSettingsViewModel`
  child; the existing `Placeholder` property can be removed as part of this
  feature.
- Navigation to the Settings section is already wired in the main shell;
  this feature only replaces the placeholder content, not the navigation entry.
- No data migration from a prior version is required; this is the first
  feature to introduce a persistent `TaxpayerProfiles` table.
- `PhoneNumber` and `Email` format validation (regex, length) is deferred to a
  future enhancement; this version treats them as free-text nullable strings.
- JMBG is stored as a plain string in SQLite (no encryption at rest beyond
  OS-level file permissions); full-database encryption is out of scope for
  this feature.
- The tab control used for Settings sub-navigation will be Avalonia's built-in
  `TabControl`; its visual styling follows the existing `FluentTheme`.

---

## Clarifications

### Session 2026-04-06

- Q: Should `TaxpayerProfile` domain entity be enriched with `PhoneNumber` and `Email`? → A: Yes — domain enrichment with two nullable string fields (`PhoneNumber?`, `Email?`).
- Q: Is the profile a strict singleton (one per app instance)? → A: Yes — `GetAsync()` returns `TaxpayerProfile?`; null on first run; UI shows empty form.
- Q: What is the validation contract for the new optional fields? → A: `PhoneNumber` and `Email` are optional (nullable); no format validation in v1. `FullName`, `Address`, `OpstinaCode` remain required (non-null, non-whitespace enforced in Domain constructor).
- Q: How does Settings navigation change? → A: `SettingsView` becomes a sub-navigation host with a `TabControl`; first tab is **Profile**; additional tabs (Mailbox, Importer) are reserved for future features.
- Q: How does the save path distinguish insert vs. update? → A: `SaveAsync` is a upsert contract; Application handler checks `GetAsync()` result — if null, new `Guid` is generated and entity is inserted; if existing, the entity is reconstructed with the same `Id` and updated.
- Q: Is profile deletion exposed in the UI? → A: No — deletion is out of scope; `DeleteAsync` remains on the interface for future use only.
- Q: How is JMBG uniqueness enforced at the persistence layer? → A: Fluent API unique index on the `Jmbg` column in `AppDbContext.OnModelCreating`; EF migration generates the corresponding `UNIQUE` constraint in SQLite.
