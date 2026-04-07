# Feature Specification: IBKR Statement Importer Configuration

**Feature Branch**: `feature/005-importer-configuration`  
**Created**: 2026-04-06  
**Status**: Draft  
**Feature Number**: 005

---

## User Scenarios & Testing *(mandatory)*

### User Story 1 — View Configured Importers (Priority: P1)

The user opens **Settings → Importers** and sees a list of all IBKR statement importer
configurations they have previously defined. On first use, the list is empty and the form
is blank. Each entry in the list shows the importer's `DisplayName` as the primary label
and the report type (e.g. "IBKR CSV") as a subtitle. The user can scroll the list and
click any entry to load its details into the form panel on the right.

**Why this priority**: The Importers tab is the configuration entry point for all statement
ingestion. Every downstream feature (email-based statement import, tax filing generation)
requires at least one correctly configured importer. A working, readable list with no errors
proves all four layers (Domain, Application, Infrastructure, Desktop) are wired correctly
end-to-end and constitutes the minimum viable foundation for the feature.

**Independent Test**: Launch the application against a database that already has two importer
rows seeded, navigate to Settings → Importers, and verify both entries appear in the list
with their display names and report type subtitles. No further interaction is required.

**Acceptance Scenarios**:

1. **Given** no importers have been configured, **When** the user opens Settings → Importers,
   **Then** the list is empty, the form fields are blank, and no error message is displayed.
2. **Given** two importers exist in the database, **When** the user opens Settings → Importers,
   **Then** both entries appear in the list, each showing `DisplayName` with the report type
   subtitle (e.g. "IBKR CSV").
3. **Given** the Importers tab is open with entries in the list, **When** the user clicks an
   entry, **Then** the form is populated with that importer's `DisplayName`, `ReportType`,
   `TaxpayerProfile`, `Mailbox`, `FromFilter`, `SubjectFilter`, `AttachmentRegex`, and
   `PaymentNotes` fields.

---

### User Story 2 — Add a New Importer (Priority: P1)

The user opens **Settings → Importers**, fills in the form — display name, report type, and
optionally a taxpayer profile, mailbox, filter fields, and payment notes — then clicks
**Save**. A new entry appears in the list and the importer is persisted to the local database.
The TaxpayerProfile and Mailbox selections are optional; the user may leave them unset and
return to configure them later.

**Why this priority**: Adding an importer is the primary write path for this feature. Without
at least one saved importer pointing to a mailbox and report type, the application cannot
begin automated statement ingestion in subsequent features.

**Independent Test**: On a fresh database, open Settings → Importers, fill in `DisplayName`
and leave all other fields at defaults, click Save, then verify the new entry appears in
the list and can be selected to view its persisted values.

**Acceptance Scenarios**:

1. **Given** the form has a valid `DisplayName` and `ReportType` selected, **When** the
   user clicks Save, **Then** a new importer appears in the list and the form reflects the
   saved values.
2. **Given** an optional `AttachmentRegex` is entered with a valid regular expression,
   **When** the user clicks Save, **Then** the importer is saved and no validation error is
   shown.
3. **Given** the `AttachmentRegex` field contains an invalid regular expression pattern,
   **When** the user clicks Save, **Then** the save is blocked and an inline validation
   error is displayed below the `AttachmentRegex` field.
4. **Given** the `DisplayName` field is empty, **When** the user attempts to save,
   **Then** the save is blocked and an inline error indicates the display name is required.
5. **Given** `TaxpayerProfile` and `Mailbox` dropdowns are left unset, **When** the user
   clicks Save, **Then** the importer is saved successfully with null FK values.

---

### User Story 3 — Edit an Existing Importer (Priority: P2)

The user selects an importer from the list. The form populates with all saved field values.
The user modifies one or more fields and clicks **Save**. The existing database record is
updated in-place and the list entry reflects the updated `DisplayName` immediately.

**Why this priority**: Importers evolve over time — the associated mailbox may change, filter
patterns may need tuning, or the taxpayer profile assignment may be added after initial
creation. In-place editing without deletion preserves the importer's identity.

**Independent Test**: Seed one importer, open Settings → Importers, select it, change the
`DisplayName`, click Save, then verify the list entry shows the new display name and the
database row reflects the update.

**Acceptance Scenarios**:

1. **Given** an importer is selected, **When** the user changes `DisplayName` and clicks Save,
   **Then** the list entry and form both reflect the new display name immediately.
2. **Given** an importer with no mailbox assigned is selected, **When** the user selects a
   mailbox from the dropdown and clicks Save, **Then** the `MailboxId` FK is persisted.
3. **Given** an importer is selected and `AttachmentRegex` is changed to an invalid pattern,
   **When** the user clicks Save, **Then** the save is blocked and an inline error is shown;
   the original record is unchanged.

---

### User Story 4 — Delete an Importer (Priority: P3)

The user selects an importer from the list and clicks **Delete**. The importer is permanently
removed from the database, the list entry disappears, and the form is cleared. No confirmation
dialog is required.

**Why this priority**: Deletion is necessary for housekeeping but carries no structural risk to
downstream data — no reports or filings reference importers directly. It is lower priority
because the application is functional without it; orphaned importer records do not break
any workflow.

**Independent Test**: Seed one importer, open Settings → Importers, select it, click Delete,
and verify the list is empty and the database contains no `Importers` rows.

**Acceptance Scenarios**:

1. **Given** an importer is selected, **When** the user clicks Delete, **Then** the importer
   is removed from the list and from the database, and the form is cleared.
2. **Given** the Delete button is shown while no importer is selected, **Then** the Delete
   button is disabled (CanExecute = false).
3. **Given** a related `TaxpayerProfile` or `Mailbox` row is deleted from its own settings tab,
   **Then** the importer's corresponding FK column is set to null; the importer row itself
   is not deleted.

---

### Edge Cases

- What happens when the user enters a `DisplayName` that exceeds 200 characters? The save is
  blocked and an inline validation error is shown; the field does not accept more than 200
  characters.
- What happens when `AttachmentRegex` contains a syntactically valid but semantically extreme
  pattern (e.g. catastrophic backtracking)? Validation only checks syntactic correctness via
  `new Regex(pattern)`; semantic performance of the pattern is not assessed at configuration
  time.
- What happens when the same `DisplayName` is used for two importers? Duplicate display names
  are allowed; no uniqueness constraint is enforced.
- What happens when the user clicks Save without selecting an importer and without making any
  changes? The Save button has `CanExecute` logic that prevents saving when no importer is
  selected and no new-importer form is active.
- What happens when the `TaxpayerProfile` or `Mailbox` referenced by an importer is deleted
  from its respective settings tab? The FK column is set to null (`DeleteBehavior.SetNull`);
  the importer remains in the list with the dropdown showing no selection.
- What happens if `PaymentNotes` exceeds 4000 characters? The field enforces a max-length
  constraint; the UI limits input to 4000 characters and the Infrastructure layer enforces
  the column constraint.
- What happens when an importer is loaded for editing and no TaxpayerProfile exists in the
  system? The profile dropdown displays an empty list; the profile FK remains null and the
  importer can still be saved.
- What happens when the user navigates away from the Importers tab with unsaved changes? The
  form state is discarded on deactivation; no unsaved-changes warning is shown (consistent
  with the Mailboxes tab behaviour in feature 004).

---

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST display a dedicated **Importers** tab inside the Settings screen,
  consistent in layout with the existing Mailboxes and TaxpayerProfile tabs.
- **FR-002**: The Importers tab MUST present a two-panel layout: a scrollable `ListBox` on the
  left (≈250 px wide) listing all configured importers, and a form panel on the right for
  viewing and editing importer details.
- **FR-003**: Each list entry MUST show the importer's `DisplayName` as the primary label and
  the resolved `ReportType` display string (e.g. "IBKR CSV") as a secondary subtitle.
- **FR-004**: Selecting a list entry MUST populate all form fields with the corresponding
  persisted values: `DisplayName`, `ReportType`, `TaxpayerProfile`, `Mailbox`, `FromFilter`,
  `SubjectFilter`, `AttachmentRegex`, and `PaymentNotes`.
- **FR-005**: The form MUST include a toolbar (or equivalent button row) with at minimum three
  actions: **Add New** (clears the form for a new importer), **Save** (persists the current
  form state), and **Delete** (removes the selected importer).
- **FR-006**: `DisplayName` MUST be required, non-empty, and limited to a maximum of 200
  characters. Save MUST be blocked when this constraint is violated and an inline error MUST
  be displayed.
- **FR-007**: `ReportType` MUST be presented as a `ComboBox` populated with all available
  report types. The initial version MUST include exactly one option: **IBKR CSV**. A
  `ReportType` value is always required; it defaults to `IbkrCsv`.
- **FR-008**: `TaxpayerProfile` MUST be presented as an optional `ComboBox` populated by
  querying the existing profile from the database on tab activation. Selecting no profile
  stores a null FK.
- **FR-009**: `Mailbox` MUST be presented as an optional `ComboBox` populated by querying
  the list of configured mailboxes on tab activation. Selecting no mailbox stores a null FK.
- **FR-010**: `AttachmentRegex` MUST be validated as a syntactically valid .NET regular
  expression when non-empty. If the pattern is invalid, Save MUST be blocked and an inline
  error MUST be displayed below the field. An empty value MUST be accepted without validation
  and is interpreted as "accept all attachments".
- **FR-011**: `FromFilter` and `SubjectFilter` are plain-text filter strings. They MUST be
  accepted without regex validation; an empty value means "no filter".
- **FR-012**: `PaymentNotes` MUST be stored as optional free text with a maximum of 4000
  characters. The field MUST support multiline input.
- **FR-013**: Adding an importer MUST persist a new row in the `Importers` table with an
  auto-generated `Guid` identifier and return that identifier to the caller.
- **FR-014**: Updating an importer MUST locate the existing row by `Id`, apply all field
  changes via the domain entity's `UpdateDetails` method, and persist the result.
- **FR-015**: Deleting an importer MUST permanently remove the row from the `Importers`
  table. Any FK columns in related tables that reference the deleted importer MUST be set to
  null rather than causing cascading deletes.
- **FR-016**: When a `TaxpayerProfile` or `Mailbox` row is deleted, the corresponding FK
  column (`TaxpayerProfileId` or `MailboxId`) in all affected `Importers` rows MUST be set
  to null (`DeleteBehavior.SetNull`). The importer rows themselves MUST NOT be deleted.

---

### Constitution Alignment *(mandatory)*

- **CA-001 (Architecture)**: Four layers are affected.
  - *Domain*: `Importer` entity redesigned; new `ReportType` enum added in `Rentier.Domain/Enums`.
    Domain MUST remain free of I/O or framework references.
  - *Application*: `ImporterDto`, `GetImportersQuery`, `AddImporterCommand`,
    `UpdateImporterCommand`, `DeleteImporterCommand` and their handlers added to
    `Rentier.Application`. `IImporterRepository` (already defined) is used as-is.
    Regex validation lives here (Application), not in Domain.
  - *Infrastructure*: `ImporterConfiguration` (EF), `ImporterRepository`, `AppDbContext`
    `DbSet<Importer>`, migration `0005_ImporterConfiguration`, and DI registration added
    to `Rentier.Infrastructure`. No application or domain logic leaks into this layer.
  - *Desktop*: `ImporterItemViewModel`, `ImporterSettingsViewModel`,
    `ImporterSettingsView.axaml`, `ReportTypeExtensions`, and `SettingsViewModel` update
    added to `Rentier.Desktop`. Desktop calls only Application use-case handlers; no direct
    repository or infrastructure access.

- **CA-002 (Money and Dates)**: This feature introduces no monetary values, rates, or business
  dates. No `decimal` or `DateOnly` fields are required. The `PaymentNotes` field is plain
  text and does not represent a monetary value.

- **CA-003 (Privacy and Security)**: No credentials or secrets are handled by this feature.
  No network calls are made. All data is stored in the local SQLite database. Filter strings
  and regex patterns contain no personally identifiable information.

- **CA-004 (Network Scope)**: This feature makes no outbound network calls. Importer
  configuration is a local database CRUD operation only. No IMAP, HTTP, or external API
  calls occur in this feature.

- **CA-005 (Async and UI)**: All repository operations (`GetAllAsync`, `AddAsync`,
  `UpdateAsync`, `DeleteAsync`, `GetByIdAsync`) MUST be `async Task`/`async Task<T>`.
  Desktop commands MUST use `ReactiveCommand.CreateFromTask`. All ViewModel properties
  MUST use `RaiseAndSetIfChanged`. UI updates (list refresh after save/delete) MUST be
  scheduled via `RxApp.MainThreadScheduler`. No `.Result` or `.Wait()` calls.

- **CA-006 (Testing Impact)**:
  - *Domain (100%)*: Unit tests for `Importer.Create` (valid/invalid DisplayName),
    `Importer.UpdateDetails` (all fields updated correctly), and `ReportType` default value.
  - *Application (≥90%)*: Unit tests for all four handlers covering: success path,
    invalid regex rejection, not-found on update/delete, empty AttachmentRegex accepted.
    All handler tests use NSubstitute mocks for `IImporterRepository`.
  - *Infrastructure (integration)*: EF in-memory SQLite integration tests confirming
    `ImporterRepository` CRUD, shadow FK `DeleteBehavior.SetNull` behaviour, and correct
    `ReportType` int mapping.
  - *Desktop (ViewModel)*: Unit tests for `ImporterSettingsViewModel` covering load, add,
    edit, delete, and regex validation error propagation.

---

### Key Entities

- **Importer**: Represents a named configuration that describes how to locate and identify
  a specific type of brokerage statement within a mailbox. Key attributes: unique identifier,
  human-readable display name, report type (the format of the statement), optional links to
  a taxpayer profile and a mailbox, three filter criteria (sender address filter, subject
  line filter, attachment filename regex), and free-text payment notes for downstream
  filing generation.

- **ReportType**: An enumeration of supported statement formats. Initial version contains a
  single value: `IbkrCsv` (Interactive Brokers CSV activity statement). Designed to be
  extended with additional formats in future features.

---

### Schema Notes

#### EF Real-Property FK Configuration Pattern

Because `Importer` stores `MailboxId` and `TaxpayerProfileId` as **real `Guid?` CLR properties**
(not shadow properties), EF Core must be configured using the **expression overload** of
`HasForeignKey` inside `ImporterConfiguration : IEntityTypeConfiguration<Importer>`:

```
// Pseudo-code — implementation detail for planning reference only
entity.HasOne<Mailbox>()
      .WithMany()
      .HasForeignKey(i => i.MailboxId)
      .IsRequired(false)
      .OnDelete(DeleteBehavior.SetNull);

entity.HasOne<TaxpayerProfile>()
      .WithMany()
      .HasForeignKey(i => i.TaxpayerProfileId)
      .IsRequired(false)
      .OnDelete(DeleteBehavior.SetNull);
```

> ⚠ Do NOT use the string overload `HasForeignKey("MailboxId")` — that is the shadow-property
> approach and does not bind to the real CLR property on the entity.

This configuration prevents cascade-delete: when a linked `Mailbox` or `TaxpayerProfile`
row is removed, the FK column on `Importers` is set to null rather than deleting the
importer row. No navigation property is exposed on the `Importer` entity.

---

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A user can create a complete importer configuration (all fields filled) in under
  60 seconds from opening the Settings → Importers tab for the first time.
- **SC-002**: All configured importers load and appear in the list within 1 second of opening
  the Importers tab, even when 50 or more importers are stored in the database.
- **SC-003**: An invalid regular expression in `AttachmentRegex` is detected and reported
  before the save operation reaches the database on 100% of attempts.
- **SC-004**: Deleting a `Mailbox` or `TaxpayerProfile` that is referenced by one or more
  importers results in the corresponding FK columns being set to null on 100% of affected
  importer rows, with no importer rows deleted.
- **SC-005**: All CRUD operations (add, update, delete, list) complete without errors on a
  database containing 100 importer records, with no UI blocking or unresponsive state.
- **SC-006**: 100% of domain rules (required DisplayName, default ReportType) and at least
  90% of application use-case logic are covered by automated tests, and all tests pass in
  CI on both Windows and macOS.

---

## Assumptions

1. **No prior Importers migration**: Migrations 0001–0004 do not create an `Importers` table.
   Migration `0005_ImporterConfiguration` creates the table from scratch.
2. **Migration ordering**: Migration 0005 depends on the `Mailboxes` table (created in 0004)
   for the FK constraint. The implementation agent must ensure migration 0004 is applied
   (or present in the EF model snapshot) before adding migration 0005.
3. **`FilterExpression` removal**: The existing `Importer` entity's `FilterExpression` property
   has never been written to the database (no migration exists for it), so removing it
   requires only a code change, not a data migration.
4. **`GetTaxpayerProfileQuery` returns a single nullable DTO**: The ViewModel wraps the result
   in a collection of 0 or 1 items to populate the profile `ComboBox`.
5. **`GetMailboxesQuery` returns a list of `MailboxDto`**: Delivered by feature 004; this
   feature depends on feature 004 being complete before implementation begins.
6. **`VoidResult` used for void command results**: Not `Unit`. Consistent with existing
   application layer conventions.
7. **`RaiseAndSetIfChanged` for ViewModel properties**: No Fody or CommunityToolkit source
   generators; all property change notifications use ReactiveUI's `RaiseAndSetIfChanged`.
8. **`AddTransient` for all services**: Registered as transient in `CompositionRoot.cs`
   consistent with the existing service registration pattern.
9. **`x:CompileBindings="False"` on the view**: Avoids compiled-binding issues with complex
   types (ComboBox item templates, nullable DTOs); consistent with feature 004 pattern.
10. **`FromFilter` and `SubjectFilter` are plain strings**: Only `AttachmentRegex` undergoes
    regex validity checking; the other two filter fields are not validated as regex patterns.
11. **No importer execution in this feature**: This feature is configuration-only. No email
    fetch, CSV parse, or statement import logic is implemented here.
12. **`SettingsViewModel` signature evolution**: After feature 003 the constructor takes
    `(ProfileSettingsViewModel, HolidaySettingsViewModel)`; after feature 004 it takes a
    third `MailboxSettingsViewModel` argument. Feature 005 adds `ImporterSettingsViewModel`
    as the fourth constructor parameter.
13. **`ReportType` enum in Domain**: Placed in `Rentier.Domain/Enums/ReportType.cs` in a
    new `Enums` sub-folder; value `IbkrCsv = 0` is the only initial entry. EF stores the
    value as an `int` column.
14. **`MailboxDto` defined in feature 004**: The `ImporterSettingsViewModel` depends on
    `MailboxDto` for the mailbox dropdown. Feature 005 implementation must follow
    feature 004's completion.
