# Implementation Plan: IMAP Mailbox Configuration

**Branch**: `feature/004-mailbox-configuration` | **Date**: 2026-04-06 | **Spec**: `specs/004-mailbox-configuration/spec.md`  
**Input**: Feature specification from `.specify/specs/004-mailbox-configuration/spec.md`

---

## Summary

Feature 004 delivers **Settings → Mailboxes**: a two-panel CRUD UI for IMAP mailbox connection configurations. Users can add, edit, and delete mailbox entries. Each entry stores connection parameters (`Host`, `Port`, `Username`, `InitialSyncDate`) in SQLite via EF Core 8, while passwords are stored exclusively in the **Windows Credential Manager** via P/Invoke to `advapi32.dll` (`CredWriteW` / `CredReadW` / `CredDeleteW`).

The feature completes the `OsCredentialStore` stub (created in Feature 001), modifies the `Mailbox` domain entity to support EF materialization and the new `InitialSyncDate` field, introduces a full CQRS Application layer (4 commands/queries + 4 handlers), wires an EF `OwnsOne<MailboxCursor>` mapping, and adds a reactive Avalonia two-panel settings tab. No IMAP network connections are made — this feature is configuration only.

---

## Technical Context

**Language/Version**: C# 12 / .NET 8  
**Primary Dependencies**: Avalonia UI 11+ (FluentTheme), ReactiveUI, EF Core 8 (SQLite provider), Microsoft.Extensions.DependencyInjection, xUnit + FluentAssertions + NSubstitute  
**Storage**: SQLite via EF Core 8; new `Mailboxes` table created by migration `0004_MailboxConfiguration`; IMAP passwords in Windows Credential Manager via P/Invoke (`advapi32.dll`)  
**Testing**: xUnit + FluentAssertions + NSubstitute; SQLite in-memory provider for Infrastructure integration tests; credential-store tests skipped on non-Windows  
**Target Platform**: Windows desktop (Avalonia); P/Invoke APIs are Windows-only — `[SupportedOSPlatform("windows")]` annotations applied  
**Project Type**: Desktop application  
**Performance Goals**: Save/delete < 200 ms on idle local SQLite + Credential Manager; list render < 100 ms for ≤ 50 mailboxes  
**Constraints**: Single-user, single-process, offline-only; no IMAP network calls in this feature; no `.Result`/`.Wait()`; no hard-coded UI strings; password never round-tripped to DTO or UI  
**Scale/Scope**: Multiple mailboxes (one per account); 1 new table (7 columns); 4 handlers; 2 new ViewModels; ~37 tests across 7 new test classes

---

## Constitution Check

_GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design._

- [x] **Clean Architecture boundary is preserved** (`Desktop → Application → Domain`; Infrastructure implements Application contracts only).  
  `MailboxSettingsViewModel` calls Application handlers only; no repository or EF imports in Desktop; `IMailboxRepository` and `ICredentialStore` defined in Application; `OsCredentialStore` and `MailboxRepository` implement those interfaces in Infrastructure.

- [x] **All monetary/rate/percentage values are modeled as `decimal`**.  
  `Mailbox` contains no monetary fields; `decimal` rule is not triggered by this feature.

- [x] **All business dates are modeled as `DateOnly`**.  
  `InitialSyncDate` is `DateOnly`. `MailboxCursor.LastSyncDate` is `DateOnly?`. EF Core 8 maps `DateOnly` to SQLite TEXT natively. No `DateTime` used anywhere in this feature.

- [x] **Security/privacy constraints hold**.  
  IMAP passwords stored exclusively in Windows Credential Manager (`advapi32.dll`). Password is never written to SQLite, never returned in `MailboxDto`, never pre-populated in the password UI field (AD-4). Credential key format: `Rentier/Mailbox/{mailboxId}`. No plaintext secrets in logs.

- [x] **External network usage is limited to approved endpoints**.  
  This feature makes zero outbound network calls (AD-8: no IMAP connection, no test-connection button). ✅ PASS.

- [x] **All I/O paths are async; UI avoids blocking calls**.  
  Repository and credential-store methods are `async Task`/`Task<T>`; handlers are `async Task<Result<T, Error>>`; `OsCredentialStore` wraps **all** synchronous P/Invoke calls in `Task.Run(...)` to offload them to the thread pool — compliant with Constitution IV ("MUST NOT block the UI thread"); ViewModel uses `ReactiveCommand.CreateFromTask`; UI updates via `RxApp.MainThreadScheduler`.  
  `.Result`/`.Wait()` are prohibited.

- [x] **Tests and coverage impact are defined**.  
  Domain: 100% rule/state coverage (`MailboxTests.cs`, ~12 cases). Application: ≥ 90% coverage (4 handler test classes). Infrastructure: EF Core in-memory integration tests (`MailboxRepositoryTests.cs`). Desktop: `MailboxSettingsViewModelTests.cs`. Credential-store tests: Windows-only.

- [x] **Feature work is mapped to an approved spec task**.  
  Branch `feature/004-mailbox-configuration`; spec under `.specify/specs/004-mailbox-configuration/`.

**Result**: ✅ All 8 gates PASS. No violations requiring justification.

---

## Project Structure

### Documentation (this feature)

```text
.specify/specs/004-mailbox-configuration/
├── plan.md                      ← This file (speckit.plan output)
├── research.md                  ← Phase 0 output  ✅ GENERATED
├── data-model.md                ← Phase 1 output  ✅ GENERATED
├── quickstart.md                ← Phase 1 output  ✅ GENERATED
├── contracts/
│   ├── IMailboxRepository.cs    ← Full interface contract  ✅ GENERATED
│   └── OsCredentialStoreImpl.md ← P/Invoke impl notes  ✅ GENERATED
└── tasks.md                     ← Phase 2 output (speckit.tasks — NOT created by speckit.plan)
```

### Source Code (repository root)

```text
src/Rentier.Domain/Entities/Mailbox.cs
    MODIFIED — add private EF constructor, change all { get; } to { get; private set; },
               add DateOnly InitialSyncDate { get; private set; }, update public constructor
               signature to accept InitialSyncDate, add Mailbox.Create(...) factory,
               add UpdateCursor(MailboxCursor) mutation method.

src/Rentier.Domain/ValueObjects/MailboxCursor.cs
    UNCHANGED — record MailboxCursor(DateOnly? LastSyncDate, long? LastUid)

src/Rentier.Application/Interfaces/ICredentialStore.cs
    UNCHANGED — SaveCredentialAsync, GetCredentialAsync, DeleteCredentialAsync

src/Rentier.Application/Repositories/IMailboxRepository.cs
    UNCHANGED — already complete: GetByIdAsync, GetAllAsync, AddAsync, UpdateAsync, DeleteAsync

src/Rentier.Application/DTOs/MailboxDto.cs
    NEW — sealed record MailboxDto(Guid Id, string Host, int Port, string Username,
          DateOnly InitialSyncDate, DateOnly? LastSyncDate, long? LastUid)
          Password field intentionally absent.

src/Rentier.Application/Commands/AddMailboxCommand.cs
    NEW — sealed record AddMailboxCommand(string Host, int Port, string Username,
          string? Password, DateOnly InitialSyncDate)
          → Result<Guid, Error>

src/Rentier.Application/Commands/UpdateMailboxCommand.cs
    NEW — sealed record UpdateMailboxCommand(Guid Id, string Host, int Port, string Username,
          string? Password, DateOnly InitialSyncDate)
          → Result<VoidResult, Error>

src/Rentier.Application/Commands/DeleteMailboxCommand.cs
    NEW — sealed record DeleteMailboxCommand(Guid Id)
          → Result<VoidResult, Error>

src/Rentier.Application/Queries/GetMailboxesQuery.cs
    NEW — sealed record GetMailboxesQuery()
          → Result<IReadOnlyList<MailboxDto>, Error>

src/Rentier.Application/Handlers/AddMailboxCommandHandler.cs
    NEW — ICommandHandler<AddMailboxCommand, Result<Guid, Error>>
          Calls Mailbox.Create(...), IMailboxRepository.AddAsync, ICredentialStore.SaveCredentialAsync
          (skipped if Password null/empty).

src/Rentier.Application/Handlers/UpdateMailboxCommandHandler.cs
    NEW — ICommandHandler<UpdateMailboxCommand, Result<VoidResult, Error>>
          GetByIdAsync → NotFound if null → construct updated Mailbox (preserve Cursor)
          → UpdateAsync → SaveCredentialAsync if Password non-empty.

src/Rentier.Application/Handlers/DeleteMailboxCommandHandler.cs
    NEW — ICommandHandler<DeleteMailboxCommand, Result<VoidResult, Error>>
          DeleteCredentialAsync (swallow ERROR_NOT_FOUND silently) → DeleteAsync.

src/Rentier.Application/Handlers/GetMailboxesQueryHandler.cs
    NEW — IQueryHandler<GetMailboxesQuery, Result<IReadOnlyList<MailboxDto>, Error>>
          GetAllAsync → project each Mailbox to MailboxDto → return as IReadOnlyList.

src/Rentier.Infrastructure/Security/OsCredentialStore.cs
    REPLACE STUB — full P/Invoke implementation of ICredentialStore using advapi32.dll.
          CREDENTIALW struct, FILETIME struct, CredWriteW/CredReadW/CredFreeW/CredDeleteW imports.
          All methods: [SupportedOSPlatform("windows")].
          Error-not-found swallowed in GetCredentialAsync and DeleteCredentialAsync.
          UTF-8 byte array for CredentialBlob (not PtrToStringUni).
          Uses Marshal.GetLastPInvokeError() (.NET 6+ correct method).

src/Rentier.Infrastructure/Persistence/AppDbContext.cs
    MODIFIED — add: public DbSet<Mailbox> Mailboxes => Set<Mailbox>();
          No OnModelCreating change needed (ApplyConfigurationsFromAssembly discovers
          MailboxConfiguration automatically).

src/Rentier.Infrastructure/Persistence/Configurations/MailboxConfiguration.cs
    NEW — IEntityTypeConfiguration<Mailbox>
          Table: "Mailboxes"; PK: Id; Required: Host (max 253), Port, Username (max 320),
          InitialSyncDate; OwnsOne<MailboxCursor> with columns Cursor_LastSyncDate (nullable),
          Cursor_LastUid (nullable).

src/Rentier.Infrastructure/Persistence/Migrations/YYYYMMDDHHMMSS_0004_MailboxConfiguration.cs
    NEW — generated by: dotnet ef migrations add 0004_MailboxConfiguration
          Creates table Mailboxes with 7 columns (Id, Host, Port, Username,
          InitialSyncDate, Cursor_LastSyncDate, Cursor_LastUid).

src/Rentier.Infrastructure/Repositories/MailboxRepository.cs
    NEW — implements IMailboxRepository with AppDbContext.
          GetByIdAsync: FirstOrDefaultAsync(m => m.Id == id).
          GetAllAsync: AsNoTracking().ToListAsync() cast to IReadOnlyList.
          AddAsync: Add + SaveChangesAsync.
          UpdateAsync: detach stale entry → Update + SaveChangesAsync.
          DeleteAsync: ExecuteDeleteAsync(m => m.Id == id) — no-op if not found.

src/Rentier.Infrastructure/InfrastructureServiceExtensions.cs
    MODIFIED — add:
          services.AddTransient<ICredentialStore, OsCredentialStore>();
          services.AddTransient<IMailboxRepository, MailboxRepository>();

src/Rentier.Desktop/ViewModels/MailboxItemViewModel.cs
    NEW — ReactiveObject; properties: Guid Id, string DisplayName ("{Username} @ {Host}:{Port}"),
          bool IsNew (marks unsaved new entry). Two constructors:
          MailboxItemViewModel(MailboxDto dto) for loaded entries;
          MailboxItemViewModel() for new (unsaved) entry.

src/Rentier.Desktop/ViewModels/MailboxSettingsViewModel.cs
    NEW — ReactiveObject; two-panel VM.
          Properties: ObservableCollection<MailboxItemViewModel> Mailboxes,
          MailboxItemViewModel? SelectedMailbox (RaiseAndSetIfChanged),
          string Host, int Port, string Username, string Password,
          DateOnly InitialSyncDate — all RaiseAndSetIfChanged.
          bool IsLoading, string ErrorMessage, string SuccessMessage.
          Commands: AddCommand (clears form + adds unsaved item),
                    SaveCommand (AddMailboxCommand or UpdateMailboxCommand based on IsNew),
                    DeleteCommand (canExecute = SelectedMailbox != null).
          LoadAsync() loads via GetMailboxesQuery on activation.
          Password field is always empty on mailbox selection (AD-4).

src/Rentier.Desktop/Views/MailboxSettingsView.axaml
    NEW — ReactiveUserControl<MailboxSettingsViewModel>.
          Two-panel Grid:
            Left: ListBox bound to Mailboxes, DisplayMemberPath=DisplayName,
                  SelectedItem={Binding SelectedMailbox}.
            Right: StackPanel form with TextBox fields for Host, Port, Username,
                   Password (PasswordChar="•"), DatePicker for InitialSyncDate;
                   Add/Save/Delete buttons; ErrorMessage and SuccessMessage TextBlocks.
          All visible strings via Strings.resx.

src/Rentier.Desktop/Views/MailboxSettingsView.axaml.cs
    NEW — ReactiveUserControl<MailboxSettingsViewModel> code-behind with WhenActivated
          that calls ViewModel.LoadAsync().

src/Rentier.Desktop/ViewModels/SettingsViewModel.cs
    MODIFIED — add MailboxSettingsViewModel MailboxesTab { get; };
               update constructor to accept MailboxSettingsViewModel mailboxesTab.

src/Rentier.Desktop/Views/SettingsView.axaml
    MODIFIED — add third TabItem (after Profile tab item 1 and Holidays tab item 2):
               <TabItem Header="{x:Static res:Strings.Settings_Mailboxes_TabHeader}">
                 <views:MailboxSettingsView DataContext="{Binding MailboxesTab}" />
               </TabItem>

src/Rentier.Desktop/Composition/CompositionRoot.cs
    MODIFIED — add handler registrations (AddMailbox, UpdateMailbox, DeleteMailbox, GetMailboxes);
               add MailboxSettingsViewModel AddTransient;
               update SettingsViewModel factory to inject MailboxSettingsViewModel.

src/Rentier.Desktop/Resources/Strings.resx
    MODIFIED — add:
               Settings_Mailboxes_TabHeader = "Mailboxes"
               Mailboxes_Host_Label = "IMAP Host"
               Mailboxes_Port_Label = "Port"
               Mailboxes_Username_Label = "Username / Email"
               Mailboxes_Password_Label = "Password"
               Mailboxes_Password_Hint = "Leave blank to keep existing"
               Mailboxes_InitialSyncDate_Label = "Sync From Date"
               Mailboxes_AddNew_Button = "Add New"
               Mailboxes_Save_Button = "Save"
               Mailboxes_Delete_Button = "Delete"
               Mailboxes_ErrorMessage_Label = "Error"
               Mailbox_Saved_Confirmation = "Mailbox saved."

tests/Rentier.Domain.Tests/MailboxTests.cs
    NEW — ~12 test cases:
          Create_ValidArgs_ReturnsMailboxWithExpectedFields;
          Create_BlankHost_ThrowsDomainException;
          Create_PortZero_ThrowsDomainException;
          Create_PortAbove65535_ThrowsDomainException;
          Create_BlankUsername_ThrowsDomainException;
          PublicCtor_NullCursor_ThrowsDomainException (or ArgumentNullException per impl);
          UpdateCursor_ValidCursor_UpdatesProperty;
          UpdateCursor_NullCursor_ThrowsArgumentNullException;
          Create_SetsInitialSyncDate;
          Create_SetsCursorLastSyncDateToInitialSyncDate;
          Create_SetsCursorLastUidToNull;
          Create_AssignsNewGuid.

tests/Rentier.Application.Tests/AddMailboxCommandHandlerTests.cs
    NEW — ~4 test cases:
          Handle_ValidCommand_ReturnsSuccessWithGuid;
          Handle_ValidCommandWithPassword_SavesCredential;
          Handle_ValidCommandWithNullPassword_DoesNotCallCredentialStore;
          Handle_DomainException_ReturnsFailure.

tests/Rentier.Application.Tests/UpdateMailboxCommandHandlerTests.cs
    NEW — ~4 test cases:
          Handle_ExistingMailbox_UpdatesAndReturnsSuccess;
          Handle_NonExistentId_ReturnsNotFoundFailure;
          Handle_WithNewPassword_UpdatesCredential;
          Handle_WithEmptyPassword_DoesNotUpdateCredential.

tests/Rentier.Application.Tests/DeleteMailboxCommandHandlerTests.cs
    NEW — ~3 test cases:
          Handle_ExistingMailbox_DeletesCredentialAndRepo;
          Handle_CredentialNotFound_SwallowsAndDeletesRepo;
          Handle_RepoDeleteFails_PropagatesFailure.

tests/Rentier.Application.Tests/GetMailboxesQueryHandlerTests.cs
    NEW — ~2 test cases:
          Handle_EmptyRepo_ReturnsEmptyList;
          Handle_MultipleMailboxes_ReturnsAllProjectedToDtos.

tests/Rentier.Infrastructure.Tests/MailboxRepositoryTests.cs
    NEW — ~6 test cases (EF Core InMemory):
          AddAsync_ThenGetAll_ReturnsMailbox;
          GetByIdAsync_ExistingId_ReturnsMailbox;
          GetByIdAsync_MissingId_ReturnsNull;
          UpdateAsync_ChangesPersistedCorrectly;
          DeleteAsync_ExistingId_Removes;
          DeleteAsync_MissingId_IsNoOp.

tests/Rentier.Desktop.Tests/MailboxSettingsViewModelTests.cs
    NEW — ~6 test cases:
          SaveCommand_DisabledWhenHostEmpty;
          AddCommand_AddsUnsavedItemToList;
          SaveCommand_OnNewItem_CallsAddHandler_AndUpdatesId;
          SaveCommand_OnExistingItem_CallsUpdateHandler;
          DeleteCommand_CanExecute_OnlyWhenMailboxSelected;
          DeleteCommand_RemovesItemFromList.
```

---

## Design Notes

### Credential Key Scheme

```
Key = "Rentier/Mailbox/" + mailboxId.ToString()
      e.g.: Rentier/Mailbox/3fa85f64-5717-4562-b3fc-2c963f66afa6
```

The prefix `Rentier/Mailbox/` namespaces credentials to avoid collisions with other Windows applications. The `Id` is always the canonical `Guid.ToString()` (lower-hyphen format). The key is constructed in Application handlers — `OsCredentialStore` receives the full key and has no prefix knowledge.

### Cursor Model and `InitialSyncDate`

`InitialSyncDate` is the date the user configures in the UI for "sync email from this date". It is:
- Set once by the user during Add/Save.
- Stored as a separate column on `Mailboxes` table (not part of `MailboxCursor`).
- Used to initialise `Cursor.LastSyncDate` on first creation (`Mailbox.Create` sets `Cursor = new MailboxCursor(LastSyncDate: initialSyncDate, LastUid: null)`).
- NOT overwritten when the sync feature (Feature 006+) updates `Cursor` via `UpdateCursor(...)`. This allows the user to see the original configured start date even after sync advances the cursor.

`MailboxCursor` (value object — EF `OwnsOne`):
- `LastSyncDate`: the most recent successfully-synced date; null before first sync.
- `LastUid`: the most recent UID processed; null before first sync.
- Stored as `Cursor_LastSyncDate` and `Cursor_LastUid` columns (inline on `Mailboxes` table).

### EF `OwnsOne<MailboxCursor>` Approach

EF Core 8 supports `OwnsOne` on `record` types when the record's constructor parameter names match the property names (case-insensitive). `MailboxCursor(DateOnly? LastSyncDate, long? LastUid)` satisfies this condition. The Fluent API in `MailboxConfiguration.OwnsOne(...)` explicitly maps column names to avoid EF's default concatenation pattern. Both columns are nullable — EF stores `null` for un-synced mailboxes.

No custom `DateOnly` → SQLite converter is required; EF Core 8 handles `DateOnly` → `TEXT "YYYY-MM-DD"` natively.

### Password Flow on Edit (AD-4)

When the user selects an existing mailbox, the `Password` binding is set to `string.Empty`. The credential is never retrieved for display. On Save:
- Empty `Password` → no `SaveCredentialAsync` call (existing credential preserved).
- Non-empty `Password` → `SaveCredentialAsync` overwrites the credential (`CredWriteW` is an upsert).

This prevents credential exposure in the UI and avoids unnecessary reads from the Credential Manager.

### Two-Panel ViewModel Design

```
MailboxSettingsViewModel
├── ObservableCollection<MailboxItemViewModel> Mailboxes
├── MailboxItemViewModel? SelectedMailbox          ← drives form population
├── string Host, Port (int), Username, Password    ← form fields
├── DateOnly InitialSyncDate                       ← form field (use DateTimeOffset? InitialSyncDateOffset bridge for XAML DatePicker)
├── ReactiveCommand AddNewCommand                  ← always enabled
├── ReactiveCommand SaveCommand                    ← enabled when required fields non-empty
├── ReactiveCommand DeleteCommand                  ← enabled when SelectedMailbox != null
└── bool IsLoading, string? ErrorMessage, string? SuccessMessage
```

`MailboxItemViewModel` wraps a `MailboxDto` for display. A temporary "new" item (with `IsNew = true`) is added to the collection when Add is clicked; the `Id` is only assigned after `AddMailboxCommandHandler` returns success.

### ViewModel String Resources Policy

All user-visible strings must be in `Strings.resx`. The ViewModel accesses `Strings.*` constants for `SuccessMessage` and error displays. View XAML accesses them via `{x:Static res:Strings.*}`.

### `AddTransient` for All Registrations

Consistent with Feature 002. Desktop uses a root `ServiceProvider` with no HTTP scope. `AddTransient` is safe and correct for all handlers, repositories, and ViewModels (the `MailboxSettingsViewModel` instance is effectively long-lived through the `SettingsViewModel` singleton chain, but correctness is not affected by `AddTransient` here).

---

## Complexity Tracking

> No Constitution Check violations requiring justification.
