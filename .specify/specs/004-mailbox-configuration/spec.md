# Feature Specification: IMAP Mailbox Connection Configuration

**Feature Branch**: `feature/004-mailbox-configuration`  
**Created**: 2026-04-06  
**Status**: Draft  
**Feature Number**: 004

---

## User Scenarios & Testing *(mandatory)*

### User Story 1 — View Configured Mailboxes (Priority: P1)

The user opens **Settings → Mailboxes** and sees a list of all IMAP mailbox connections they
have previously configured. On first use, the list is empty and the form is blank. Each
entry in the list shows the mailbox identity in the format `{Username} @ {Host}:{Port}`.
The user can scroll the list and click any entry to load its details into the form panel.

**Why this priority**: The Mailboxes tab is the entry point for all IMAP-related configuration.
Every downstream feature (mailbox sync, statement import) requires at least one configured
mailbox. Delivering a working, readable list with no errors is the minimum viable slice that
proves all four layers (Domain, Application, Infrastructure, Desktop) are wired correctly.

**Independent Test**: Launch the application against a database that already has two mailbox
rows seeded, navigate to Settings → Mailboxes, and verify both entries appear in the list
with correct display names. No further interaction is needed.

**Acceptance Scenarios**:

1. **Given** no mailboxes have been configured, **When** the user opens Settings → Mailboxes,
   **Then** the list is empty, the form is blank, and no error message is displayed.
2. **Given** two mailboxes exist in the database, **When** the user opens Settings → Mailboxes,
   **Then** both entries appear in the list, each showing `{Username} @ {Host}:{Port}`.
3. **Given** the Mailboxes tab is open with entries in the list, **When** the user clicks an
   entry, **Then** the form is populated with that mailbox's Host, Port, Username, and
   InitialSyncDate (password field remains empty).

---

### User Story 2 — Add a New Mailbox (Priority: P1)

The user opens **Settings → Mailboxes**, fills in the form fields — IMAP host, port, username,
password, and initial sync date — and clicks **Add**. A new entry appears in the list and
the mailbox is persisted to the local database. The password is stored securely in the
Windows OS Credential Locker; it is never written to the database file.

**Why this priority**: Adding a mailbox is the primary write path and the prerequisite for
all future IMAP sync operations. Without at least one successfully saved mailbox (including
its secure credential), the application cannot perform any email-based processing.

**Independent Test**: On a fresh database, open Settings → Mailboxes, fill in all required
fields including a password, click Add, then reopen the tab (or restart the app) and verify
the new entry appears in the list and the credential exists in Windows Credential Manager
under the key `Rentier/Mailbox/{id}`.

**Acceptance Scenarios**:

1. **Given** the form has all required fields filled with valid values, **When** the user
   clicks Add, **Then** a new mailbox entry appears in the list and a success state is
   reflected in the UI (no error message, list updates immediately).
2. **Given** a password was supplied during Add, **When** the operation completes,
   **Then** the password is stored in Windows Credential Manager and the database row
   contains no password data.
3. **Given** the host field is empty or the port is outside 1–65535, **When** the user
   attempts to save, **Then** the save is blocked and an inline error message is displayed
   below the offending field.
4. **Given** the InitialSyncDate field is not filled, **When** the user attempts to Add,
   **Then** the save is blocked and an inline error indicates the date is required.

---

### User Story 3 — Edit an Existing Mailbox (Priority: P2)

The user selects a mailbox from the list. The form populates with the saved Host, Port,
Username, and InitialSyncDate. The password field is intentionally blank. The user modifies
one or more fields and clicks **Save**. The existing database record is updated in-place.
If the user supplies a new password, the OS credential is updated; if the password field
is left blank, the existing credential is preserved unchanged.

**Why this priority**: IMAP server details change (host migrations, port changes, credential
rotations). Editing an existing entry without requiring full re-entry of unmodified fields
is essential for ongoing usability.

**Independent Test**: Save a mailbox with a password, reopen the tab, select the entry,
change only the port field, leave the password blank, and click Save. Verify the port is
updated in the database and the original credential in Windows Credential Manager is
unchanged.

**Acceptance Scenarios**:

1. **Given** a mailbox is selected in the list, **When** the form opens, **Then** Host, Port,
   Username, and InitialSyncDate are pre-populated and the password field is empty.
2. **Given** the user modifies the Host and leaves the password field blank, **When** Save
   is clicked, **Then** the Host is updated in the database and the existing OS credential
   is not modified.
3. **Given** the user supplies a new password in the password field, **When** Save is
   clicked, **Then** the OS credential entry for this mailbox is overwritten with the
   new password.
4. **Given** the user clears the Username field (making it blank), **When** Save is clicked,
   **Then** the save is blocked and an inline validation error is displayed.

---

### User Story 4 — Delete a Mailbox (Priority: P2)

The user selects a mailbox from the list and clicks **Delete**. The entry is removed from
the list. The associated credential is deleted from the Windows OS Credential Locker and
the database row is removed. The Delete button is disabled (greyed out) when no entry
is selected.

**Why this priority**: Decommissioning a mailbox — after account closure, broker change, or
error during Add — must be clean: both the local database row and the OS credential entry
are removed together. Leaving orphaned credentials is a security concern.

**Independent Test**: Add a mailbox with a password, select it in the list, click Delete,
and verify the list is now empty, the database row is gone, and the credential no longer
exists in Windows Credential Manager.

**Acceptance Scenarios**:

1. **Given** a mailbox is selected in the list, **When** the user clicks Delete, **Then**
   the entry is removed from the list and the form is cleared.
2. **Given** Delete was triggered, **When** the operation completes, **Then** the OS
   credential for that mailbox is removed from Windows Credential Manager.
3. **Given** the OS credential for the mailbox was never saved (e.g., no password was
   provided during Add), **When** Delete is triggered, **Then** the database row is still
   removed and no error is shown (credential-not-found is silently ignored).
4. **Given** no mailbox is selected in the list, **When** the user views the Delete button,
   **Then** the button is disabled and cannot be clicked.

---

### Edge Cases

- **Empty list on first launch**: `GetMailboxesQuery` returns an empty list; the UI renders
  a blank form and an empty ListBox without error.
- **Port boundary values**: Port 0 is rejected; port 65535 is accepted; port 65536 is
  rejected. Non-numeric port input must be rejected before the command is dispatched.
- **Host containing whitespace only**: Treated as empty; domain validation in the factory
  rejects it with an inline error.
- **Username containing whitespace only**: Same treatment as blank host — rejected by domain
  validation.
- **Add with no password**: Allowed (password is `string?` — nullable). No credential is
  written to the OS store. The mailbox record is created in the database. On subsequent
  edit, a password can be supplied.
- **Credential write failure**: If the OS Credential Manager rejects the write (e.g.,
  permissions error), the command returns a `Result.Failure` with the Win32 error message;
  the database row is NOT written (credential must succeed before DB insert).
- **Delete with DB failure**: If `IMailboxRepository.DeleteAsync` fails after the credential
  has already been deleted, the error is propagated as `Result.Failure`. The credential
  is already gone; the DB row may require manual cleanup (acceptable at this stage).
- **Multiple mailboxes with the same host/username**: Allowed — different brokerage accounts
  may share an IMAP host and differ only in username; the system assigns distinct `Guid` ids
  and separate credential entries.
- **Navigation away with unsaved form state**: Out of scope for this feature; dirty-state
  warning is deferred to a future UX pass.
- **Concurrent access**: Not applicable — single-user, single-process desktop application.
- **No IMAP connection test**: This feature explicitly does not attempt any IMAP connection.
  No "Test Connection" button exists.

---

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The Settings screen MUST expose a **Mailboxes** tab (third tab after Profile
  and Holidays) that renders a two-panel layout: a scrollable list on the left and an
  editable form on the right.
- **FR-002**: Each entry in the list MUST display the mailbox identity in the format
  `{Username} @ {Host}:{Port}`.
- **FR-003**: The user MUST be able to add a new mailbox by filling in Host, Port, Username,
  Password, and InitialSyncDate in the form and clicking **Add**.
- **FR-004**: The Host field MUST default to `imap.gmail.com` and the Port field MUST
  default to `993` when the form is in new-entry mode.
- **FR-005**: The user's IMAP password MUST be stored exclusively in the Windows OS
  Credential Locker (Windows Credential Manager). The password MUST NEVER be written to
  SQLite, plaintext files, or any other persistent storage outside the OS credential store.
- **FR-006**: The password input field MUST mask its contents using a password character
  (`•`) so the entered text is not visible on screen.
- **FR-007**: When the user selects an existing mailbox for editing, the password field
  MUST be empty. Submitting the form with an empty password field MUST preserve the
  existing OS credential unchanged. Submitting with a non-empty password field MUST
  overwrite the OS credential.
- **FR-008**: Selecting a mailbox entry in the list MUST populate the form with that
  mailbox's Host, Port, Username, and InitialSyncDate values.
- **FR-009**: InitialSyncDate MUST be required when adding a mailbox. It represents the
  starting date from which future email sync will begin and is stored permanently on the
  mailbox record.
- **FR-010**: Deleting a mailbox MUST first remove its OS credential entry (using the key
  `Rentier/Mailbox/{mailboxId}`), then remove the database row. If no credential exists
  for the key, the missing credential MUST be silently ignored (not treated as an error).
- **FR-011**: The Domain MUST validate that Host is non-empty (not null, empty, or
  whitespace), Port is in the range 1–65535 (inclusive), and Username is non-empty. A
  `Mailbox` instance MUST NOT be constructible with invalid values.
- **FR-012**: The OS Credential Locker integration MUST use Windows Credential Manager
  via P/Invoke (`advapi32.dll`), calling `CredWriteW`, `CredReadW`, `CredFreeW`, and
  `CredDeleteW`. No additional NuGet packages are required for this integration.
- **FR-013**: The credential key format MUST be `Rentier/Mailbox/{mailboxId}` where
  `{mailboxId}` is the `Guid` string representation of the mailbox's Id.
- **FR-014**: `GetMailboxesQuery` MUST return a list of `MailboxDto` records. The
  `MailboxDto` MUST NOT contain any password or credential field.
- **FR-015**: All repository operations (`GetAllAsync`, `AddAsync`, `UpdateAsync`,
  `DeleteAsync`) and all credential store operations (`SaveCredentialAsync`,
  `DeleteCredentialAsync`) MUST be asynchronous. No blocking I/O calls are permitted.
- **FR-016**: `AddMailboxCommand` MUST return the `Guid` Id of the newly created mailbox
  on success, allowing the UI to immediately reflect the new entry's identity.
- **FR-017**: Validation errors MUST be displayed inline within the form (below the
  offending field or in a dedicated error area). No modal dialogs or message boxes are
  permitted for validation feedback.
- **FR-018**: The EF Core mapping for `Mailbox` MUST use `OwnsOne<MailboxCursor>` to
  persist the cursor as two nullable columns on the `Mailboxes` table:
  `Cursor_LastSyncDate` (`DateOnly?`) and `Cursor_LastUid` (`long?`). Both columns
  default to `NULL`, indicating no sync has occurred yet.

---

### Constitution Alignment *(mandatory)*

- **CA-001 (Architecture)**: All four Clean Architecture layers are modified and all
  inward-only dependency rules are preserved:
  - **Domain** (`Rentier.Domain`): `Mailbox` entity gains EF-compatible private
    constructor, `private set` on all properties, `DateOnly InitialSyncDate`, a
    `static Create(...)` factory that enforces invariants, and an `UpdateCursor(...)`
    method. `MailboxCursor` value object is unchanged. No I/O references are introduced.
  - **Application** (`Rentier.Application`): `IMailboxRepository` interface is added.
    Four CQRS handlers are added (`AddMailboxCommandHandler`, `UpdateMailboxCommandHandler`,
    `DeleteMailboxCommandHandler`, `GetMailboxesQueryHandler`). Handlers depend only on
    `IMailboxRepository` and the existing `ICredentialStore` interface. `MailboxDto` record
    is added. No EF Core or infrastructure packages are referenced.
  - **Infrastructure** (`Rentier.Infrastructure`): `MailboxRepository` implements
    `IMailboxRepository`. `OsCredentialStore` stub is fully implemented using P/Invoke.
    `MailboxConfiguration` (IEntityTypeConfiguration) maps `OwnsOne<MailboxCursor>`.
    `AppDbContext` gains `DbSet<Mailbox>`. Migration `0004_MailboxConfiguration` is
    generated. `InfrastructureServiceExtensions` registers all new services.
  - **Desktop** (`Rentier.Desktop`): `MailboxSettingsViewModel` and
    `MailboxSettingsView` are added. `SettingsViewModel` gains a `MailboxesTab` property.
    `SettingsView.axaml` gains a third TabItem. `CompositionRoot.cs` wires all new
    handlers and the new ViewModel. Desktop does not directly call repositories or
    infrastructure services.
- **CA-002 (Money and Dates)**: No monetary values are involved in this feature.
  All date fields — `InitialSyncDate`, `LastSyncDate` — use `DateOnly` exclusively.
  `DateTime` is not used anywhere in the mailbox domain model.
- **CA-003 (Privacy and Security)**: IMAP passwords are stored exclusively in the Windows
  OS Credential Locker via `OsCredentialStore`. Passwords are never written to SQLite,
  plaintext files, logs, or environment variables. `MailboxDto` and all Application-layer
  outputs are password-free. This directly satisfies Constitution Principle II.
- **CA-004 (Network Scope)**: This feature makes zero outbound network calls. No IMAP
  connection is attempted; the feature configures credentials only. The only OS-level
  call is to the local Windows Credential Manager API.
- **CA-005 (Async and UI)**: All repository and credential store operations are
  `async Task` / `async Task<T>`. Application handlers are async. ViewModel commands
  use `ReactiveCommand.CreateFromTask`. UI state updates (`IsLoading`, `ErrorMessage`,
  list refresh) are scheduled via `RxApp.MainThreadScheduler`. `.Result` and `.Wait()`
  are prohibited throughout the call stack.
- **CA-006 (Testing Impact)**:
  - **Domain tests (100% rule/state coverage)**: `Mailbox.Create(...)` — valid inputs
    accepted; null/empty host rejected; port 0 rejected; port 65536 rejected; port 65535
    accepted; null/empty username rejected. `UpdateCursor(...)` — cursor replaced
    correctly. `MailboxCursor` — null/null construction (pre-sync state); non-null values
    retained.
  - **Application tests (≥ 90% coverage)**: `AddMailboxCommandHandler` — success path
    (new Guid returned, credential saved), password-empty path (no credential write),
    domain validation failure path. `UpdateMailboxCommandHandler` — success path with
    password update, success path without password update (credential untouched).
    `DeleteMailboxCommandHandler` — credential exists path (deleted then DB row deleted),
    credential missing path (silently continues, DB row deleted). `GetMailboxesQueryHandler`
    — empty list, populated list mapped to MailboxDto correctly.
    All infrastructure interfaces mocked with NSubstitute.
  - **Infrastructure tests**: Integration tests using SQLite in-memory provider verifying
    `MailboxRepository.GetAllAsync` returns correct rows; `AddAsync` persists host, port,
    username, InitialSyncDate, and null cursor columns; `DeleteAsync` removes the row.
    `OsCredentialStore` is tested on Windows CI only (guarded by
    `[SupportedOSPlatform("windows")]` and `[PlatformSpecific(TestPlatforms.Windows)]`).
  - **Desktop tests**: `MailboxSettingsViewModel` — initial load populates list, selecting
    entry populates form, Add command enabled when form is valid, Delete command disabled
    when nothing is selected, inline error shown on validation failure, `IsLoading` toggles
    correctly during async operations.

---

### Key Entities

- **`Mailbox`** (Domain Entity): Represents one IMAP account connection configuration.
  Attributes: `Guid Id` (system-generated unique identifier), `string Host` (IMAP server
  hostname, required), `int Port` (IMAP port, 1–65535), `string Username` (IMAP login,
  required), `DateOnly InitialSyncDate` (user-chosen starting date for email sync,
  immutable after creation), `MailboxCursor Cursor` (current sync position — date and/or
  UID of last processed message, updated by future sync feature). One `Mailbox` per
  email account; multiple mailboxes per application instance are supported.
- **`MailboxCursor`** (Domain Value Object): Represents the current position of the
  mailbox sync process. Fields: `DateOnly? LastSyncDate` (date of the last successfully
  synced email, null before first sync), `long? LastUid` (UID of the last processed
  message in the IMAP folder, null before first sync). Both fields are nullable — a
  freshly added mailbox has a null cursor until the first sync completes.
- **`MailboxDto`** (Application DTO): Read-only record transferring mailbox data to
  the Desktop layer without exposing the Domain entity. Fields: `Guid Id`, `string Host`,
  `int Port`, `string Username`, `DateOnly InitialSyncDate`, `DateOnly? LastSyncDate`,
  `long? LastUid`. Contains no password or credential data.

---

### EF Core Schema — MailboxCursor OwnsOne Mapping

The `MailboxCursor` value object is persisted as two owned columns on the `Mailboxes` table
via EF Core's `OwnsOne` API. Conceptual column layout:

```
Mailboxes table
───────────────────────────────────────────────────────────
Id                   BLOB (Guid)         NOT NULL  PK
Host                 TEXT                NOT NULL
Port                 INTEGER             NOT NULL
Username             TEXT                NOT NULL
InitialSyncDate      TEXT (DateOnly)     NOT NULL
Cursor_LastSyncDate  TEXT (DateOnly?)    NULL
Cursor_LastUid       INTEGER (long?)     NULL
```

The `MailboxConfiguration` class (implementing `IEntityTypeConfiguration<Mailbox>`) calls
`entity.OwnsOne(m => m.Cursor, cursor => { cursor.Property(c => c.LastSyncDate).HasColumnName("Cursor_LastSyncDate"); cursor.Property(c => c.LastUid).HasColumnName("Cursor_LastUid"); })`.
EF Core 8 handles `DateOnly?` natively on the SQLite provider without a custom value
converter.

---

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A user can add a new mailbox (fill all fields and click Add) and see the
  entry appear in the list within 1 second on a standard development machine with an idle
  local database.
- **SC-002**: Credential security: zero password values are present in the SQLite database
  file, in application logs, or in any `MailboxDto` instance, as verified by static
  analysis and the Application test suite.
- **SC-003**: Domain validation rejects 100% of invalid inputs (empty host, empty username,
  port ≤ 0 or > 65535) with zero false negatives or false positives, as verified by the
  Domain unit test suite.
- **SC-004**: The Domain test suite achieves 100% rule and state coverage for `Mailbox`
  entity logic; the Application test suite achieves ≥ 90% coverage for all four new
  command/query handlers.
- **SC-005**: The Delete operation (remove OS credential + remove DB row) completes within
  500 ms on a device with an idle local database and responsive Windows Credential Manager.
- **SC-006**: No compiler warnings are introduced; CI remains green on both Windows and
  macOS matrix targets after the `0004_MailboxConfiguration` migration is applied.
  (The `OsCredentialStore` P/Invoke implementation and its platform-specific tests are
  guarded so that non-Windows CI jobs skip them without failing.)
- **SC-007**: The Settings → Mailboxes tab is reachable within two navigation clicks from
  the application main window, consistent with the Settings → Profile and Settings →
  Holidays tabs established in prior features.

---

## Assumptions

- The application is installed and used by a single person on a single Windows machine;
  no multi-user or multi-device scenarios apply to this feature.
- The local SQLite database already exists and EF Core migrations are applied automatically
  on application startup before any repository calls are made.
- `net8.0` TFM remains unchanged across all projects. P/Invoke to `advapi32.dll` does not
  require a `net8.0-windows` target framework moniker change.
- `CRED_PERSIST_LOCAL_MACHINE` (value 2) is used for the `Persist` field of
  `CREDENTIALW`. This causes credentials to persist across reboots and user logoff/logon
  cycles within the same Windows user account.
- The credential blob is encoded as UTF-8 bytes (`Encoding.UTF8.GetBytes(password)`).
  The maximum credential blob size enforced by Windows Credential Manager (2,560 bytes)
  is sufficient for any realistic IMAP password.
- If `CredWriteW` returns false (write failure), the handler returns
  `Result.Failure(new Error("CREDENTIAL_STORE_FAILED", <Win32 error message>))` and the
  database operation is not attempted.
- `MailboxSettingsViewModel` is registered with the DI container in a way consistent with
  the existing Profile and Holiday tab ViewModels (AddTransient, effectively captive
  through the `SettingsViewModel` singleton lifetime).
- The Avalonia `TextBox` with `PasswordChar="•"` is used for the password field. Avalonia
  does not have a dedicated PasswordBox control; this is the approved pattern per
  clarification assumption 10.
- `RaiseAndSetIfChanged` is used for all observable properties on ViewModels. Fody,
  CommunityToolkit.Mvvm source generators, and other reactive property helpers are not
  used in this feature.
- The `VoidResult` type (not `Unit`) is used as the success payload for commands that
  return no data, consistent with the result type conventions already established in the
  codebase.
- `IMailboxRepository` did not exist before this feature; this feature creates it from
  scratch. Any stub generated by a prior scaffolding step (feature 001) should be checked
  and reconciled.
- Navigation to the Settings section is already wired in the main application shell from
  a prior feature. This feature only adds the third Mailboxes tab to the existing
  Settings TabControl; it does not modify shell navigation.
- No IMAP server connection is attempted during mailbox configuration. There is no "Test
  Connection" button. Connection validation is deferred to the future mailbox sync feature.
- Format validation beyond non-empty/length constraints (e.g., hostname DNS format,
  username email format) is deferred to a future enhancement. This version treats Host
  and Username as free-text non-empty strings.
- `InitialSyncDate` is immutable after creation. The field is displayed in the edit form
  for informational purposes but changes to it on an existing record are out of scope for
  this version.
