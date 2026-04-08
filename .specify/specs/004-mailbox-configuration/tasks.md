---
description: "Task list for Feature 004: IMAP Mailbox Configuration"
---

# Tasks: IMAP Mailbox Configuration (Feature 004)

**Input**: Design documents from `.specify/specs/004-mailbox-configuration/`  
**Branch**: `feature/004-mailbox-configuration`  
**Prerequisites**: spec.md ✅ plan.md ✅ data-model.md ✅ contracts/ ✅

**Tests**: Tests are included for all Domain, Application, Infrastructure, and Desktop layers
in line with constitution quality gates (Domain: 100% rule/state coverage; Application: ≥ 90%;
Infrastructure: EF in-memory integration; Desktop: ViewModel behaviour).

**Organization**: Tasks are grouped by user story to enable independent implementation and
testing of each story. All four user stories share a single foundational phase (Domain +
Application + Infrastructure) because the CRUD layer is atomic — you cannot list without
the repository, and you cannot add without the credential store.

## Format: `[ID] [P?] [Story?] Description`

- **[P]**: Can run in parallel (different files, no blocking dependency on an incomplete task)
- **[Story]**: User story label — `[US1]`, `[US2]`, `[US3]`, `[US4]` — required in all
  Phase 3–6 tasks
- All tasks include exact file paths

---

## Pre-Flight: Critical Existing State

> ⚠️ Before starting, confirm the following files **already exist** and must **NOT** be recreated:
>
> - `src/Rentier.Application/Repositories/IMailboxRepository.cs` — namespace `Rentier.Application.Repositories`; interface with `GetByIdAsync`, `GetAllAsync`, `AddAsync`, `UpdateAsync`, `DeleteAsync`. **DO NOT CREATE — use as-is.**
> - `src/Rentier.Application/Interfaces/ICredentialStore.cs` — namespace `Rentier.Application.Interfaces`; interface with `SaveCredentialAsync`, `GetCredentialAsync`, `DeleteCredentialAsync`. **DO NOT CREATE — use as-is.**
> - `src/Rentier.Infrastructure/Security/OsCredentialStore.cs` — stub exists; **IMPLEMENT** in T020.
> - `src/Rentier.Domain/Entities/Mailbox.cs` — entity exists; **MODIFY** in T001.
> - `src/Rentier.Domain/ValueObjects/MailboxCursor.cs` — **UNCHANGED**.

---

## Phase 1: Domain — Entity Modifications

**Purpose**: Bring the `Mailbox` entity into its final shape required by EF Core 8, the
`Mailbox.Create(...)` factory, and the `UpdateDetails(...)` mutation method. All subsequent
phases depend on the modified entity compiling cleanly.

> **⚠️ CRITICAL**: No Application, Infrastructure, or Desktop work may begin until T001 compiles.

- [x] T001 [DOMAIN] MODIFY `src/Rentier.Domain/Entities/Mailbox.cs`:
  add `private Mailbox() { }` parameterless EF constructor; change all existing `{ get; }` property
  setters to `{ get; private set; }`; add `public DateOnly InitialSyncDate { get; private set; }`
  property; update the existing public constructor signature to
  `public Mailbox(Guid id, string host, int port, string username, DateOnly initialSyncDate, MailboxCursor cursor)`
  with `DomainException` validation for blank host, port outside 1–65535, blank username, and
  `ArgumentNullException.ThrowIfNull(cursor)`; add
  `public static Mailbox Create(string host, int port, string username, DateOnly initialSyncDate)`
  factory that calls `new Mailbox(Guid.NewGuid(), host, port, username, initialSyncDate, new MailboxCursor(LastSyncDate: initialSyncDate, LastUid: null))`;
  add `public void UpdateDetails(string host, int port, string username, DateOnly initialSyncDate)`
  method that re-validates inputs using the same rules as the constructor and sets the four fields
  (`ArgumentNullException`/`DomainException` on invalid values). Retain `UpdateCursor(MailboxCursor)`
  method unchanged.

- [x] T002 [TEST] CREATE `tests/Rentier.Domain.Tests/MailboxTests.cs`:
  xUnit test class covering — `Create_ValidInputs_ReturnsMailboxWithCorrectProperties` (asserts Id
  is non-empty Guid, Host/Port/Username/InitialSyncDate match inputs);
  `Create_EmptyHost_ThrowsDomainException`;
  `Create_WhitespaceHost_ThrowsDomainException`;
  `Create_PortZero_ThrowsDomainException`;
  `Create_PortAbove65535_ThrowsDomainException`;
  `Create_Port65535_Succeeds` (boundary pass case);
  `Create_EmptyUsername_ThrowsDomainException`;
  `Create_SetsInitialCursorFromInitialSyncDate` (asserts `Cursor.LastSyncDate == initialSyncDate`
  and `Cursor.LastUid == null`);
  `Create_AssignsUniqueGuidEachCall`;
  `UpdateDetails_ValidInputs_UpdatesAllMutableFields`;
  `UpdateDetails_EmptyHost_ThrowsDomainException`;
  `UpdateDetails_InvalidPort_ThrowsDomainException`.
  Use `FluentAssertions`; no mocks required.

**Checkpoint**: `dotnet build` on `Rentier.Domain` and `Rentier.Domain.Tests` produces zero errors.
`dotnet test tests/Rentier.Domain.Tests` — all tests pass (implementation first, then verify RED → GREEN).

---

## Phase 2: Application — DTOs, Commands, Queries, and Handlers

**Purpose**: The full CQRS Application layer. DTOs and command/query records have no dependencies
on each other and can be created in parallel. Handlers depend on the records and on the modified
`Mailbox` entity from Phase 1.

> **⚠️ CRITICAL**: All Phase 2 tasks must complete before Phase 3 (Infrastructure) can start,
> because the repository tests and DI registration reference the handler types.

### Batch A — Records (parallel, T003–T007)

- [x] T003 [P] [APPLICATION] CREATE `src/Rentier.Application/DTOs/MailboxDto.cs`:
  `namespace Rentier.Application.DTOs;`
  `public sealed record MailboxDto(Guid Id, string Host, int Port, string Username, DateOnly InitialSyncDate, DateOnly? LastSyncDate, long? LastUid);`
  Password is intentionally absent. `LastSyncDate` and `LastUid` are from `MailboxCursor`; both
  nullable (null when no sync has occurred).

- [x] T004 [P] [APPLICATION] CREATE `src/Rentier.Application/Commands/AddMailboxCommand.cs`:
  `namespace Rentier.Application.Commands;`
  `public sealed record AddMailboxCommand(string Host, int Port, string Username, string? Password, DateOnly InitialSyncDate);`
  Returns `Result<Guid, Error>` (Guid = new mailbox Id). Password is `string?`; null or empty means
  no credential is written.

- [x] T005 [P] [APPLICATION] CREATE `src/Rentier.Application/Commands/UpdateMailboxCommand.cs`:
  `namespace Rentier.Application.Commands;`
  `public sealed record UpdateMailboxCommand(Guid Id, string Host, int Port, string Username, string? Password, DateOnly InitialSyncDate);`
  Returns `Result<VoidResult, Error>`. Null/empty Password means preserve existing credential.

- [x] T006 [P] [APPLICATION] CREATE `src/Rentier.Application/Commands/DeleteMailboxCommand.cs`:
  `namespace Rentier.Application.Commands;`
  `public sealed record DeleteMailboxCommand(Guid Id);`
  Returns `Result<VoidResult, Error>`.

- [x] T007 [P] [APPLICATION] CREATE `src/Rentier.Application/Queries/GetMailboxesQuery.cs`:
  `namespace Rentier.Application.Queries;`
  `public sealed record GetMailboxesQuery();`
  Returns `Result<IReadOnlyList<MailboxDto>, Error>`.

### Batch B — Handlers (T008–T011, after Batch A)

- [x] T008 [APPLICATION] CREATE `src/Rentier.Application/Handlers/GetMailboxesQueryHandler.cs`:
  Implements `IQueryHandler<GetMailboxesQuery, Result<IReadOnlyList<MailboxDto>, Error>>`.
  Constructor-inject `IMailboxRepository _repository` (namespace `Rentier.Application.Repositories`).
  `HandleAsync`: call `await _repository.GetAllAsync(ct)`; project each `Mailbox m` to
  `new MailboxDto(m.Id, m.Host, m.Port, m.Username, m.InitialSyncDate, m.Cursor.LastSyncDate, m.Cursor.LastUid)`;
  return `Result<IReadOnlyList<MailboxDto>, Error>.Success(list.AsReadOnly())`.
  (`Result<T,E>` API confirmed from `src/Rentier.Application/Common/Result.cs`: use `.Success(...)` and
  `.Failure(...)` — `.Ok()` does **not** exist on this type.)

- [x] T009 [P] [APPLICATION] CREATE `src/Rentier.Application/Handlers/AddMailboxCommandHandler.cs`:
  Implements `ICommandHandler<AddMailboxCommand, Result<Guid, Error>>`.
  Constructor-inject `IMailboxRepository _repository`, `ICredentialStore _credentials`
  (namespace `Rentier.Application.Interfaces`).
  `HandleAsync`: wrap `Mailbox.Create(command.Host, command.Port, command.Username, command.InitialSyncDate)`
  in try/catch `DomainException` → return failure with `new Error("DOMAIN_VALIDATION", ex.Message)`;
  if `command.Password` is not null/empty: `await _credentials.SaveCredentialAsync($"Rentier/Mailbox/{mailbox.Id}", command.Password, ct)`;
  `await _repository.AddAsync(mailbox, ct)`; return success with `mailbox.Id`.

- [x] T010 [P] [APPLICATION] CREATE `src/Rentier.Application/Handlers/UpdateMailboxCommandHandler.cs`:
  Implements `ICommandHandler<UpdateMailboxCommand, Result<VoidResult, Error>>`.
  Constructor-inject `IMailboxRepository _repository`, `ICredentialStore _credentials`.
  `HandleAsync`: `var mailbox = await _repository.GetByIdAsync(command.Id, ct)`;
  if null return failure with `new Error("NOT_FOUND", $"Mailbox {command.Id} not found")`;
  wrap `mailbox.UpdateDetails(command.Host, command.Port, command.Username, command.InitialSyncDate)`
  in try/catch `DomainException` → return failure;
  `await _repository.UpdateAsync(mailbox, ct)`;
  if `command.Password` not null/empty: `await _credentials.SaveCredentialAsync($"Rentier/Mailbox/{mailbox.Id}", command.Password, ct)`;
  return `Result<VoidResult, Error>.Success(VoidResult.Value)`.

- [x] T011 [P] [APPLICATION] CREATE `src/Rentier.Application/Handlers/DeleteMailboxCommandHandler.cs`:
  Implements `ICommandHandler<DeleteMailboxCommand, Result<VoidResult, Error>>`.
  Constructor-inject `IMailboxRepository _repository`, `ICredentialStore _credentials`.
  `HandleAsync`: try `await _credentials.DeleteCredentialAsync($"Rentier/Mailbox/{command.Id}", ct)`
  — swallow all exceptions (credential may never have been stored);
  `await _repository.DeleteAsync(command.Id, ct)`;
  return `Result<VoidResult, Error>.Success(VoidResult.Value)`.

### Batch C — Application Tests (parallel, T012–T015)

- [x] T012 [P] [TEST] CREATE `tests/Rentier.Application.Tests/GetMailboxesQueryHandlerTests.cs`:
  Mock `IMailboxRepository` with NSubstitute.
  `HandleAsync_NoMailboxes_ReturnsEmptyList` — repo returns empty list; assert result is success,
  value is empty `IReadOnlyList<MailboxDto>`.
  `HandleAsync_WithTwoMailboxes_ReturnsMappedDtos` — repo returns two `Mailbox` instances created
  via `Mailbox.Create(...)`; assert result is success, Dto count = 2, each Dto `Id`/`Host`/`Port`/
  `Username`/`InitialSyncDate` matches the source entity.

- [x] T013 [P] [TEST] CREATE `tests/Rentier.Application.Tests/AddMailboxCommandHandlerTests.cs`:
  Mock `IMailboxRepository` and `ICredentialStore` with NSubstitute.
  `HandleAsync_ValidCommand_ReturnsSuccessWithGuid` — valid command, no password; assert result is
  success, returned Guid is non-empty, `AddAsync` called once.
  `HandleAsync_WithPassword_SavesCredentialBeforeAdd` — non-empty password; assert
  `SaveCredentialAsync` called once with key matching `"Rentier/Mailbox/{id}"` pattern and
  `AddAsync` called once.
  `HandleAsync_NullPassword_SkipsCredentialSave` — null password; assert `SaveCredentialAsync` never
  called, `AddAsync` still called.
  `HandleAsync_EmptyPassword_SkipsCredentialSave` — empty string password; same as above.
  `HandleAsync_InvalidHost_ReturnsDomainError` — empty host; assert result is failure with code
  `"DOMAIN_VALIDATION"`, `AddAsync` never called.

- [x] T014 [P] [TEST] CREATE `tests/Rentier.Application.Tests/UpdateMailboxCommandHandlerTests.cs`:
  Mock both interfaces.
  `HandleAsync_ValidUpdate_UpdatesRepoAndReturnsSuccess` — repo returns existing mailbox; valid new
  values; assert `UpdateAsync` called once, result is success.
  `HandleAsync_NotFound_ReturnsFailure` — repo returns null; assert result is failure with code
  `"NOT_FOUND"`, `UpdateAsync` never called.
  `HandleAsync_WithNewPassword_UpdatesCredential` — non-empty new password; assert
  `SaveCredentialAsync` called once.
  `HandleAsync_EmptyPassword_PreservesExistingCredential` — empty password; assert
  `SaveCredentialAsync` never called, `UpdateAsync` still called.

- [x] T015 [P] [TEST] CREATE `tests/Rentier.Application.Tests/DeleteMailboxCommandHandlerTests.cs`:
  Mock both interfaces.
  `HandleAsync_ExistingMailbox_DeletesCredentialThenRepo` — assert `DeleteCredentialAsync` called
  once with correct key, then `DeleteAsync` called once, result is success.
  `HandleAsync_CredentialThrows_StillDeletesRepo` — `DeleteCredentialAsync` throws; assert
  `DeleteAsync` still called, result is success (exception swallowed).

**Checkpoint**: `dotnet build` on Application projects produces zero errors. `dotnet test` on
`Rentier.Application.Tests` — all tests pass.

---

## Phase 3: Infrastructure — EF Config, Migration, Repository, OsCredentialStore

**Purpose**: Persist the `Mailbox` entity to SQLite and implement the Windows Credential Manager
integration. All Infrastructure work is independent of Desktop and can proceed in parallel with
Phase 4 once Phase 2 is complete.

### Batch A — EF Configuration (T016–T017, parallel)

- [x] T016 [P] [INFRASTRUCTURE] CREATE `src/Rentier.Infrastructure/Persistence/Configurations/MailboxConfiguration.cs`:
  `public sealed class MailboxConfiguration : IEntityTypeConfiguration<Mailbox>`.
  `Configure`: `builder.ToTable("Mailboxes"); builder.HasKey(m => m.Id);`
  `builder.Property(m => m.Host).IsRequired().HasMaxLength(253);` (RFC 1035 max hostname length)
  `builder.Property(m => m.Port).IsRequired();`
  `builder.Property(m => m.Username).IsRequired().HasMaxLength(320);` (RFC 5321 max email length)
  `builder.Property(m => m.InitialSyncDate).IsRequired();` (EF 8 maps DateOnly natively)
  `builder.OwnsOne(m => m.Cursor, cursor => { cursor.Property(c => c.LastSyncDate).HasColumnName("Cursor_LastSyncDate").IsRequired(false); cursor.Property(c => c.LastUid).HasColumnName("Cursor_LastUid").IsRequired(false); });`
  `OnModelCreating` in `AppDbContext` already calls `ApplyConfigurationsFromAssembly(...)` — no
  additional registration needed.

- [x] T017 [P] [INFRASTRUCTURE] MODIFY `src/Rentier.Infrastructure/Persistence/AppDbContext.cs`:
  add `public DbSet<Mailbox> Mailboxes => Set<Mailbox>();` property.
  Add `using Rentier.Domain.Entities;` if not already present.

### Batch B — Migration (T018, after T016 + T017)

- [x] T018 [INFRASTRUCTURE] RUN EF migration (depends on T016 and T017 compiling cleanly):
  Command: `dotnet ef migrations add 0004_MailboxConfiguration --project src/Rentier.Infrastructure --startup-project src/Rentier.Desktop`
  Verify the generated migration file is created under
  `src/Rentier.Infrastructure/Persistence/Migrations/` with a `Up()` method that creates the
  `Mailboxes` table with columns: `Id` (TEXT PK), `Host` (TEXT NOT NULL), `Port` (INTEGER NOT NULL),
  `Username` (TEXT NOT NULL), `InitialSyncDate` (TEXT NOT NULL), `Cursor_LastSyncDate` (TEXT nullable),
  `Cursor_LastUid` (INTEGER nullable). Correct any drift and re-run if the generated DDL does not
  match.

### Batch C — Repository (T019, after T018)

- [x] T019 [INFRASTRUCTURE] CREATE `src/Rentier.Infrastructure/Repositories/MailboxRepository.cs`:
  Implements `IMailboxRepository` (namespace `Rentier.Application.Repositories`).
  Constructor-inject `AppDbContext _db`.
  `GetByIdAsync(Guid id, CancellationToken ct)`: `return await _db.Mailboxes.FindAsync([id], ct);`
  — returns null if not found (EF `FindAsync` returns null for missing PK).
  `GetAllAsync(CancellationToken ct)`: `return await _db.Mailboxes.AsNoTracking().ToListAsync(ct);`
  — cast to `IReadOnlyList<Mailbox>` via `.AsReadOnly()` or direct cast.
  `AddAsync(Mailbox mailbox, CancellationToken ct)`: `_db.Mailboxes.Add(mailbox); await _db.SaveChangesAsync(ct);`
  `UpdateAsync(Mailbox mailbox, CancellationToken ct)`: detach any stale tracked entry for the same
  key via `_db.ChangeTracker.Entries<Mailbox>().FirstOrDefault(e => e.Entity.Id == mailbox.Id)?.State = EntityState.Detached;`
  then `_db.Mailboxes.Update(mailbox); await _db.SaveChangesAsync(ct);`
  `DeleteAsync(Guid id, CancellationToken ct)`: load via `FindAsync([id], ct)`; if not null:
  `_db.Mailboxes.Remove(entity); await _db.SaveChangesAsync(ct);`
  (do NOT use `ExecuteDeleteAsync` — EF InMemory provider used in tests does not support it).

### Batch D — OsCredentialStore (T020, independent)

- [x] T020 [INFRASTRUCTURE] IMPLEMENT `src/Rentier.Infrastructure/Security/OsCredentialStore.cs`
  (replace existing stub with full implementation):
  Add `[SupportedOSPlatform("windows")]` attribute to the class.
  Define private P/Invoke imports from `advapi32.dll`:
    `[DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool CredWriteW(ref CREDENTIALW credential, uint flags);`
    `[DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool CredReadW(string target, uint type, uint reservedFlag, out IntPtr credentialPtr);`
    `[DllImport("advapi32.dll")] private static extern void CredFreeW(IntPtr credentialPtr);`
    `[DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool CredDeleteW(string target, uint type, uint reservedFlag);`
  Define `[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct CREDENTIALW`
  with fields: `uint Flags; uint Type; string TargetName; string? Comment; uint LastWrittenLow; uint LastWrittenHigh; uint CredentialBlobSize; IntPtr CredentialBlob; uint Persist; uint AttributeCount; IntPtr Attributes; string? TargetAlias; string? UserName;`
  Constants: `const uint CRED_TYPE_GENERIC = 1u; const uint CRED_PERSIST_LOCAL_MACHINE = 2u; const int ERROR_NOT_FOUND = 1168;`
  `SaveCredentialAsync(string key, string secret, CancellationToken ct)`:
    **Wrap the entire P/Invoke body in `Task.Run(() => { ... }, ct)`** (required by Constitution IV —
    P/Invoke to `advapi32.dll` is synchronous; wrapping in `Task.Run` offloads it to the thread pool and
    prevents blocking the UI thread):
    encode `byte[] blob = Encoding.UTF8.GetBytes(secret)`;
    `IntPtr ptr = Marshal.AllocHGlobal(blob.Length);`
    in `try/finally { Marshal.FreeHGlobal(ptr); }`:
      `Marshal.Copy(blob, 0, ptr, blob.Length);`
      build `CREDENTIALW cred = new() { Type = CRED_TYPE_GENERIC, TargetName = key, CredentialBlobSize = (uint)blob.Length, CredentialBlob = ptr, Persist = CRED_PERSIST_LOCAL_MACHINE, UserName = key };`
      if `!CredWriteW(ref cred, 0)`: throw `Win32Exception(Marshal.GetLastPInvokeError())`.
    return `Task.Run(() => { /* body above */ }, ct)`.
  `GetCredentialAsync(string key, CancellationToken ct)`:
    **Wrap in `Task.Run<string?>(() => { ... }, ct)`**:
    if `!CredReadW(key, CRED_TYPE_GENERIC, 0, out IntPtr ptr)`: check `Marshal.GetLastPInvokeError() == ERROR_NOT_FOUND`; return `null` (from within the lambda);
    **Use `try/finally { CredFreeW(ptr); }`** to guarantee the OS-allocated pointer is freed even on exception:
      marshal `CREDENTIALW cred = Marshal.PtrToStructure<CREDENTIALW>(ptr)`;
      `byte[] blob = new byte[cred.CredentialBlobSize]; Marshal.Copy(cred.CredentialBlob, blob, 0, blob.Length);`
      return `Encoding.UTF8.GetString(blob)` (from within the try block; `CredFreeW(ptr)` runs in the finally).
    return `Task.Run<string?>(() => { /* body above */ }, ct)`.
  `DeleteCredentialAsync(string key, CancellationToken ct)`:
    **Wrap in `Task.Run(() => { ... }, ct)`**:
    if `!CredDeleteW(key, CRED_TYPE_GENERIC, 0)`: if `Marshal.GetLastPInvokeError() != ERROR_NOT_FOUND`: throw `Win32Exception(...)`.
    return `Task.Run(() => { /* body above */ }, ct)`.
  Add usings: `System.ComponentModel; System.Runtime.InteropServices; System.Runtime.Versioning; System.Text;`

### Batch E — Infrastructure Tests (T021, after T019)

- [x] T021 [TEST] CREATE `tests/Rentier.Infrastructure.Tests/MailboxRepositoryTests.cs`:
  Use EF Core InMemory provider: `new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options`
  (unique DB name per test to avoid state leakage).
  Helper: `private static Mailbox MakeMailbox(string host = "imap.example.com") => Mailbox.Create(host, 993, "user@example.com", new DateOnly(2024, 1, 1));`
  `GetAllAsync_EmptyDb_ReturnsEmptyList` — fresh DB; assert result count == 0.
  `AddAsync_NewMailbox_PersistsToDb` — Add then GetAll; assert one entry with matching Id/Host.
  `GetByIdAsync_ExistingId_ReturnsMailbox` — Add then GetById; assert entity returned is not null.
  `GetByIdAsync_UnknownId_ReturnsNull` — GetById with random Guid; assert null.
  `UpdateAsync_ModifiedMailbox_PersistsChanges` — Add; modify Host via `UpdateDetails`; UpdateAsync;
  GetById; assert new Host returned.
  `DeleteAsync_ExistingMailbox_RemovesFromDb` — Add; Delete; GetAll; assert count == 0.
  `DeleteAsync_UnknownId_IsNoOp` — Delete with random Guid; assert no exception thrown.

### Batch F — DI Wiring for Infrastructure (T026, independent)

- [x] T026 [DI] MODIFY `src/Rentier.Infrastructure/InfrastructureServiceExtensions.cs`:
  Add `services.AddTransient<IMailboxRepository, MailboxRepository>();`
  Add `services.AddTransient<ICredentialStore, OsCredentialStore>();`
  Add required usings: `using Rentier.Application.Repositories;`
  `using Rentier.Infrastructure.Repositories;`
  `using Rentier.Application.Interfaces;`
  `using Rentier.Infrastructure.Security;`

**Checkpoint**: `dotnet build` on Infrastructure projects produces zero errors. `dotnet test` on
`Rentier.Infrastructure.Tests` — all repository tests pass. `dotnet ef database update` succeeds
and creates the `Mailboxes` table in the local SQLite file.

---

## Phase 4: User Story 1 — View Configured Mailboxes (Priority: P1) 🎯 MVP

**Goal**: The user opens Settings → Mailboxes and sees all configured mailboxes listed. Clicking
an entry populates the form. On first use the list is empty with no errors.

**Independent Test**: Launch the app against a database seeded with two `Mailbox` rows. Navigate
to Settings → Mailboxes. Verify both entries appear in the list as `{Username} @ {Host}:{Port}`.
Click an entry; verify the form populates with the correct Host, Port, Username, and
InitialSyncDate, and the password field is empty.

### Implementation for User Story 1

- [x] T022 [US1] [DESKTOP] CREATE `src/Rentier.Desktop/ViewModels/MailboxItemViewModel.cs`:
  `public sealed class MailboxItemViewModel : ReactiveObject`.
  Properties (all `private set` backing fields with `RaiseAndSetIfChanged`):
    `public Guid Id { get; private set; }`
    `public string Host { get; private set; } = string.Empty;`
    `public int Port { get; private set; }`
    `public string Username { get; private set; } = string.Empty;`
    `public DateOnly InitialSyncDate { get; private set; }`
    `public DateOnly? LastSyncDate { get; private set; }`
    `public long? LastUid { get; private set; }`
  Computed read-only property: `public string DisplayName => $"{Username} @ {Host}:{Port}";`
  Static factory: `public static MailboxItemViewModel From(MailboxDto dto)` — constructs and sets all fields.
  `public void UpdateFrom(MailboxDto dto)` — updates all mutable fields in this order:
  `Id`, `Host`, `Port`, `Username`, `InitialSyncDate`, `LastSyncDate`, `LastUid`; then calls
  `this.RaisePropertyChanged(nameof(DisplayName))` **after** all individual setters so that bound
  ListBox items do not briefly show stale display names during multi-property updates.
  No `[Reactive]` or `[ObservableProperty]` attributes — use explicit `RaiseAndSetIfChanged`.

- [x] T023 [US1] [DESKTOP] CREATE `src/Rentier.Desktop/ViewModels/MailboxSettingsViewModel.cs`:
  `public sealed class MailboxSettingsViewModel : ReactiveObject, IActivatableViewModel`.
  `public ViewModelActivator Activator { get; } = new();`
  Observable properties via `RaiseAndSetIfChanged` (backing fields listed with defaults):
    `string _host = "imap.gmail.com"` → `public string Host`
    `int _port = 993` → `public int Port`
    `string _username = string.Empty` → `public string Username`
    `string _password = string.Empty` → `public string Password`
    `DateOnly _initialSyncDate = DateOnly.FromDateTime(DateTime.Today)` → `public DateOnly InitialSyncDate`
    Also expose `public DateTimeOffset? InitialSyncDateOffset` as a **bridging property** required because
    Avalonia 11's `DatePicker.SelectedDate` is `DateTimeOffset?`, not `DateOnly`. Getter:
    `new DateTimeOffset(_initialSyncDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero)`. Setter:
    `InitialSyncDate = DateOnly.FromDateTime(value?.DateTime ?? DateTime.Today); this.RaisePropertyChanged(nameof(InitialSyncDateOffset));`
    The `InitialSyncDateOffset` property is **only** for XAML binding; all Application layer code uses `InitialSyncDate`.
    `MailboxItemViewModel? _selectedMailbox` → `public MailboxItemViewModel? SelectedMailbox`
    `bool _isLoading` → `public bool IsLoading`
    `string? _errorMessage` → `public string? ErrorMessage`
    `string? _successMessage` → `public string? SuccessMessage`
    `bool _isEditMode` → `public bool IsEditMode`
  `public ObservableCollection<MailboxItemViewModel> Mailboxes { get; } = new();`
  Constructor-inject all four handler interfaces (see Phase 2 Batch B):
    `IQueryHandler<GetMailboxesQuery, Result<IReadOnlyList<MailboxDto>, Error>> _queryHandler`
    `ICommandHandler<AddMailboxCommand, Result<Guid, Error>> _addHandler`
    `ICommandHandler<UpdateMailboxCommand, Result<VoidResult, Error>> _updateHandler`
    `ICommandHandler<DeleteMailboxCommand, Result<VoidResult, Error>> _deleteHandler`
  `AddNewCommand = ReactiveCommand.Create(OnAddNew)` — resets form fields to defaults, sets
  `IsEditMode = false`, clears `SelectedMailbox`.
  `SaveCommand = ReactiveCommand.CreateFromTask(OnSaveAsync)` — if `IsEditMode` dispatches
  `UpdateMailboxCommand`; otherwise dispatches `AddMailboxCommand`.
  On **success**:
    - Reload list via `GetMailboxesQuery` (scheduled on `RxApp.MainThreadScheduler`).
    - For Add path: after reload, find the newly created item in `Mailboxes` by the returned `Guid` Id,
      set `SelectedMailbox` to it, set `IsEditMode = true`, and clear `Password = string.Empty`.
    - For Update path: update `SelectedMailbox.UpdateFrom(dto)` and clear `Password = string.Empty`.
    - Set `SuccessMessage` to `Strings.Mailbox_Saved_Confirmation`; clear `ErrorMessage`.
  On **failure**: set `ErrorMessage` from the result error message; clear `SuccessMessage`.
  `DeleteCommand = ReactiveCommand.CreateFromTask(OnDeleteAsync, this.WhenAnyValue(x => x.SelectedMailbox).Select(m => m != null))` — dispatches `DeleteMailboxCommand` for selected item, removes from `Mailboxes` collection on success.
  `WhenActivated` block: dispatch `GetMailboxesQuery`, populate `Mailboxes` on
  `RxApp.MainThreadScheduler`, set `IsLoading` during operation.
  When `SelectedMailbox` changes (via `WhenAnyValue`): populate form fields
  (Host, Port, Username, InitialSyncDate from the selected item); set `Password = string.Empty`;
  set `IsEditMode = true` (or false when null).

- [x] T024 [US1] [DESKTOP] CREATE `src/Rentier.Desktop/Views/MailboxSettingsView.axaml`:
  `<UserControl ...>` root with `ReactiveUserControl<MailboxSettingsViewModel>` code-behind type.
  Root layout: `<Grid ColumnDefinitions="250,*">` (left panel fixed 250px, right panel fills).
  Left panel (`Grid.Column="0"`): `<DockPanel>` with `<Button DockPanel.Dock="Bottom" Command="{Binding AddNewCommand}" Content="{x:Static res:Strings.Mailboxes_AddNew_Button}"/>`
  and `<ListBox ItemsSource="{Binding Mailboxes}" SelectedItem="{Binding SelectedMailbox}">` with
  `<ListBox.ItemTemplate><DataTemplate><TextBlock Text="{Binding DisplayName}"/></DataTemplate></ListBox.ItemTemplate>`.
  Right panel (`Grid.Column="1"`): `<StackPanel Margin="16" Spacing="8">` containing:
    `<TextBlock Text="{x:Static res:Strings.Mailboxes_Host_Label}"/>` + `<TextBox Text="{Binding Host}"/>`
    `<TextBlock Text="{x:Static res:Strings.Mailboxes_Port_Label}"/>` + `<NumericUpDown Value="{Binding Port}" Minimum="1" Maximum="65535" FormatString="0"/>`
    `<TextBlock Text="{x:Static res:Strings.Mailboxes_Username_Label}"/>` + `<TextBox Text="{Binding Username}"/>`
    `<TextBlock Text="{x:Static res:Strings.Mailboxes_Password_Label}"/>` + `<TextBox Text="{Binding Password}" PasswordChar="•" Watermark="{x:Static res:Strings.Mailboxes_Password_Hint}"/>`
    `<TextBlock Text="{x:Static res:Strings.Mailboxes_InitialSyncDate_Label}"/>` + `<DatePicker SelectedDate="{Binding InitialSyncDateOffset}"/>`
    (binds to the `DateTimeOffset?` bridging property — see T023; Avalonia 11 `DatePicker.SelectedDate` is `DateTimeOffset?`)
    `<StackPanel Orientation="Horizontal" Spacing="8">` with `<Button Command="{Binding SaveCommand}" Content="{x:Static res:Strings.Mailboxes_Save_Button}"/>` and `<Button Command="{Binding DeleteCommand}" Content="{x:Static res:Strings.Mailboxes_Delete_Button}"/>`
    `<TextBlock Text="{Binding ErrorMessage}" Foreground="Red" IsVisible="{Binding ErrorMessage, Converter={x:Static StringConverters.IsNotNullOrEmpty}}"/>`
    `<TextBlock Text="{Binding SuccessMessage}" Foreground="Green" IsVisible="{Binding SuccessMessage, Converter={x:Static StringConverters.IsNotNullOrEmpty}}"/>`
    `<ProgressBar IsIndeterminate="True" IsVisible="{Binding IsLoading}" Height="4"/>`
  All string literals from `Strings.resx` (namespace alias `res:` — matches existing `SettingsView.axaml` convention) — no hard-coded visible strings.

- [x] T025 [US1] [DESKTOP] CREATE `src/Rentier.Desktop/Views/MailboxSettingsView.axaml.cs`:
  `public partial class MailboxSettingsView : ReactiveUserControl<MailboxSettingsViewModel>`
  Constructor calls `InitializeComponent();` and uses `this.WhenActivated(disposables => { /* bindings handled by XAML */ });`.
  No event handlers — all interactions via ReactiveUI command bindings.

**Checkpoint**: App launches, Settings → Mailboxes tab is visible, list renders without crash.
Empty state shows blank form with no error message.

---

## Phase 5: User Story 2 — Add a New Mailbox (Priority: P1)

**Goal**: The user fills in Host, Port, Username, Password, and InitialSyncDate, clicks Add. A
new entry appears in the list; the record is in the database; the credential is in Windows
Credential Manager under `Rentier/Mailbox/{id}`.

**Independent Test**: On a fresh database, open Settings → Mailboxes. Fill in all fields including
a password. Click Add. Verify the new entry appears in the list. Reopen the tab and verify it
persists. Check Windows Credential Manager for `Rentier/Mailbox/{id}`.

> **Dependencies**: Requires Phase 3 (Infrastructure) and Phase 4 (US1 ViewModels/Views) to be
> complete. `AddNewCommand` and `SaveCommand` are already wired in `MailboxSettingsViewModel`
> (T023) — this phase provides validation test coverage and the string resources.

### Tests for User Story 2

- [x] T027 [P] [TEST] [US2] CREATE `tests/Rentier.Desktop.Tests/MailboxSettingsViewModelTests.cs`:
  Mock all four handler interfaces with NSubstitute.
  `WhenActivated_NoMailboxes_MailboxesCollectionIsEmpty` — activate VM; query returns empty;
  assert `Mailboxes.Count == 0`, no error.
  `WhenActivated_TwoMailboxes_PopulatesCollection` — query returns two dtos; assert
  `Mailboxes.Count == 2`, `DisplayName` values match expected format.
  `SelectMailbox_PopulatesFormFields` — set `SelectedMailbox`; assert `Host`, `Port`, `Username`,
  `InitialSyncDate` match dto values, `Password == string.Empty`, `IsEditMode == true`.
  `AddNewCommand_Execute_ResetsFormAndClearsSelection` — execute `AddNewCommand`; assert
  `SelectedMailbox == null`, `Host == "imap.gmail.com"`, `Port == 993`, `IsEditMode == false`.
  `SaveCommand_NewMode_CallsAddHandlerAndRefreshesList` — set up add handler to return success
  Guid; execute `SaveCommand`; assert add handler called once, query handler called a second time.
  `SaveCommand_EditMode_CallsUpdateHandler` — set `IsEditMode = true` with `SelectedMailbox` set;
  execute `SaveCommand`; assert update handler called, add handler NOT called.
  `DeleteCommand_CanExecute_OnlyWhenMailboxSelected` — assert `DeleteCommand.CanExecute` is false
  with null `SelectedMailbox`, true when `SelectedMailbox` is set.
  `DeleteCommand_Executes_RemovesFromCollection` — add item to `Mailboxes`, set as
  `SelectedMailbox`; execute `DeleteCommand`; assert `Mailboxes.Count == 0`.

**Checkpoint**: `dotnet test tests/Rentier.Desktop.Tests` — all ViewModel tests pass. Add flow
works end-to-end against the running app.

---

## Phase 6: User Stories 3 & 4 — Edit and Delete (Priority: P2)

**Goal US3**: User selects an existing mailbox, modifies fields, clicks Save. Database record is
updated. Empty password leaves credential unchanged; new password overwrites credential.

**Goal US4**: User selects a mailbox and clicks Delete. Entry removed from list, database row
deleted, OS credential removed. Delete button is disabled when nothing is selected.

**Independent Test US3**: Add a mailbox with password "old". Select it, change Port, leave
password blank, click Save. Verify port updated in DB; credential is still "old" in Credential
Manager.

**Independent Test US4**: Add a mailbox with password "test". Select it, click Delete. Verify list
is empty, DB row gone, credential gone from Credential Manager.

> **Note**: The `UpdateMailboxCommand` handler (T010), `DeleteMailboxCommand` handler (T011),
> the `SaveCommand` (edit branch), and `DeleteCommand` are all already implemented in
> `MailboxSettingsViewModel` (T023). This phase verifies the full end-to-end flows and adds any
> remaining wiring.

### Implementation for User Stories 3 & 4

- [x] T028 [US3] [US4] [DESKTOP] MODIFY `src/Rentier.Desktop/ViewModels/SettingsViewModel.cs`:
  Add `public MailboxSettingsViewModel MailboxesTab { get; }` property.
  Add `MailboxSettingsViewModel mailboxesTab` constructor parameter.
  Set `MailboxesTab = mailboxesTab` in constructor body.

- [x] T029 [US3] [US4] [DESKTOP] MODIFY `src/Rentier.Desktop/Views/SettingsView.axaml`:
  Add third `<TabItem>` after the existing Holidays tab:
  ```xml
  <TabItem Header="{x:Static res:Strings.Settings_Mailboxes_TabHeader}">
      <views:MailboxSettingsView DataContext="{Binding MailboxesTab}" />
  </TabItem>
  ```
  Add XML namespace for `views:` if not already declared:
  `xmlns:views="clr-namespace:Rentier.Desktop.Views"`.

**Checkpoint**: Settings screen shows three tabs (Profile, Holidays, Mailboxes). Edit and Delete
flows work end-to-end. Delete button is disabled when no mailbox is selected.

---

## Phase 7: DI Wiring — Desktop Composition

**Purpose**: Register all new Application handlers and `MailboxSettingsViewModel` in the DI
container so the application starts without `InvalidOperationException`.

- [x] T030 [DI] MODIFY `src/Rentier.Desktop/Composition/CompositionRoot.cs`:
  Register Application handlers (all `AddTransient`):
    `services.AddTransient<IQueryHandler<GetMailboxesQuery, Result<IReadOnlyList<MailboxDto>, Error>>, GetMailboxesQueryHandler>()`
    `services.AddTransient<ICommandHandler<AddMailboxCommand, Result<Guid, Error>>, AddMailboxCommandHandler>()`
    `services.AddTransient<ICommandHandler<UpdateMailboxCommand, Result<VoidResult, Error>>, UpdateMailboxCommandHandler>()`
    `services.AddTransient<ICommandHandler<DeleteMailboxCommand, Result<VoidResult, Error>>, DeleteMailboxCommandHandler>()`
  Register Desktop VM: `services.AddTransient<MailboxSettingsViewModel>()`
  **No factory lambda change is needed** for `SettingsViewModel`. The existing registration
  `services.AddTransient<SettingsViewModel>()` uses DI auto-resolution. After T028 adds
  `MailboxSettingsViewModel mailboxesTab` as a third constructor parameter, DI will automatically
  resolve it because `MailboxSettingsViewModel` is registered in the same step above.
  Add all required `using` directives.

- [x] T031 [P] [DI] MODIFY `src/Rentier.Desktop/Resources/Strings.resx`:
  Add the following key/value entries:
    `Settings_Mailboxes_TabHeader` = `Mailboxes`
    `Mailboxes_Host_Label` = `IMAP Host`
    `Mailboxes_Port_Label` = `Port`
    `Mailboxes_Username_Label` = `Username / Email`
    `Mailboxes_Password_Label` = `Password`
    `Mailboxes_Password_Hint` = `Leave blank to keep existing`
    `Mailboxes_InitialSyncDate_Label` = `Sync From Date`
    `Mailboxes_AddNew_Button` = `Add New`
    `Mailboxes_Save_Button` = `Save`
    `Mailboxes_Delete_Button` = `Delete`
    `Mailboxes_ErrorMessage_Label` = `Error`
  Edit as XML directly or via the Visual Studio resource editor. Verify `Strings.Designer.cs` is
  auto-regenerated with the new static properties.

**Checkpoint**: `dotnet build src/Rentier.Desktop` produces zero errors and zero warnings.
Application starts, navigates to Settings → Mailboxes, and all three user-story flows (view, add,
edit, delete) work end-to-end.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Verify quality gates, fix any remaining issues, confirm quickstart scenarios pass.

- [x] T032 [P] Verify Domain test coverage gate: run `dotnet test tests/Rentier.Domain.Tests`
  with coverage; confirm all `Mailbox` factory and mutation paths are exercised (100% branch
  coverage on `Mailbox.cs` validation). Fix any gaps.

- [x] T033 [P] Verify Application test coverage gate: run `dotnet test tests/Rentier.Application.Tests`
  with coverage; confirm ≥ 90% line coverage on all four handler files. Fix any gaps.

- [x] T034 Run `dotnet build` on the full solution and resolve any remaining compiler warnings
  (nullable reference warnings, unused variable warnings, platform compatibility warnings for
  `OsCredentialStore` — suppress with `#pragma warning disable CA1416` only where the
  `[SupportedOSPlatform("windows")]` attribute is already present).

- [x] T035 Run quickstart validation scenarios from `.specify/specs/004-mailbox-configuration/quickstart.md`
  manually against a local database; confirm all acceptance scenarios from spec.md US1–US4 pass.
  Document any deviations.

---

## Dependencies & Execution Order

### Phase Dependencies

```
Phase 1 (Domain)           → No dependencies — start immediately
Phase 2 (Application)      → Depends on Phase 1 compiling (modified Mailbox entity)
Phase 3 (Infrastructure)   → Depends on Phase 2 (handlers reference commands/queries)
Phase 4 (Desktop US1)      → Depends on Phase 2 (ViewModels reference handler interfaces)
Phase 5 (Desktop US2)      → Depends on Phase 4 (ViewModels/Views already created)
Phase 6 (Desktop US3+US4)  → Depends on Phase 4 (SettingsViewModel modification)
Phase 7 (DI)               → Depends on Phases 3, 4, 5, 6 (registers all new types)
Phase 8 (Polish)           → Depends on all previous phases
```

### User Story Dependencies

| Story | Phase | Depends On |
|-------|-------|------------|
| US1 — View Mailboxes | Phase 4 | Domain + Application + Infrastructure |
| US2 — Add Mailbox | Phase 5 | US1 (ViewModels/Views reused) |
| US3 — Edit Mailbox | Phase 6 | US1 (UpdateCommand handler in VM) |
| US4 — Delete Mailbox | Phase 6 | US1 (DeleteCommand handler in VM) |

> **Note**: US3 and US4 share Phase 6 because they both require only `SettingsViewModel`
> modification and XAML tab wiring — the handlers are already complete in Phase 2.

### Within Each Phase

- Domain entity changes (T001) → all Application types
- Application DTOs/Commands/Queries (T003–T007) → Handlers (T008–T011)
- Handlers → Tests (T012–T015)
- EF Config (T016–T017) → Migration (T018) → Repository (T019)
- Repository (T019) → Repository Tests (T021)
- Infrastructure DI (T026) → Desktop DI (T030)
- ViewModels (T022–T023) → Views (T024–T025)

---

## Parallel Opportunities

### Batch diagram (all phases in parallel where marked)

```bash
# Phase 1
T001  # Sequential — entity shape affects everything

# Phase 2, Batch A — all parallel
T003 T004 T005 T006 T007   # DTOs + Commands + Queries (independent files)

# Phase 2, Batch B — after Batch A
T008   # GetMailboxesQueryHandler
T009 T010 T011   # Add/Update/Delete handlers (parallel — independent files)

# Phase 2, Batch C — after Batch B
T012 T013 T014 T015   # All handler test classes (parallel — independent files)

# Phase 3, Batch A — parallel after Phase 2
T016 T017   # EF Config + DbSet (independent files)

# Phase 3, Batch B — after T016 + T017
T018   # Migration (sequential — requires both EF config files)

# Phase 3, Batch C — after T018
T019   # Repository (sequential — references DbSet)

# Phase 3, Batch D — independent of T016–T019
T020   # OsCredentialStore (no EF dependency)

# Phase 3, Batch E — after T019
T021   # Repository tests

# Phase 3, Batch F — after T019 + T020
T026   # Infrastructure DI wiring

# Phase 4 — after Phase 2 + Phase 3
T022 → T023 → T024 T025   # ItemVM → SettingsVM → Views (sequential within VM layer)

# Phase 7 — after Phases 3–6
T030 T031   # DI registration + Strings (parallel)
```

---

## Implementation Strategy

### MVP First (US1 + US2 — Read and Add)

1. Complete Phase 1: Domain modifications (T001–T002)
2. Complete Phase 2: Application layer (T003–T015)
3. Complete Phase 3: Infrastructure (T016–T021, T026)
4. Complete Phase 4: Desktop US1 ViewModels/Views (T022–T025)
5. Complete Phase 5: Desktop US2 tests (T027)
6. Complete Phase 7: DI wiring (T030–T031)
7. **STOP AND VALIDATE**: App lists and adds mailboxes with credential storage — MVP delivered.

### Incremental Delivery

1. MVP above → US1 + US2 working → demo/commit
2. Add Phase 6 (T028–T029) → US3 Edit + US4 Delete → full CRUD
3. Phase 8 Polish → coverage gates + quickstart validation → merge-ready

### Parallel Team Strategy

With two developers:
- **Developer A**: Phase 1 → Phase 2 Batch A–B → Phase 3
- **Developer B**: Phase 2 Batch C (tests) → Phase 4 → Phase 5
- Both merge into feature branch; DI wiring (Phase 7) done together last.

---

## Notes

- `[P]` tasks operate on different files with no dependency on an incomplete sibling task.
- `[US1]`/`[US2]`/`[US3]`/`[US4]` labels map to user stories in spec.md for traceability.
- `IMailboxRepository` namespace: `Rentier.Application.Repositories` (NOT `Interfaces`).
- `ICredentialStore` namespace: `Rentier.Application.Interfaces`.
- `Result<T, E>` factory methods: use `.Ok(...)` / `.Fail(...)` (or `.Success(...)` / `.Failure(...)`)
  as defined in `src/Rentier.Application/Common/Result.cs` — check that file before coding handlers.
- `VoidResult.Value` is the singleton for void command results.
- No `.Result` / `.Wait()` calls anywhere in the codebase.
- EF InMemory provider in repository tests — do NOT use `ExecuteDeleteAsync` in `MailboxRepository`.
- `OsCredentialStore` is Windows-only — use `[SupportedOSPlatform("windows")]` and
  `Marshal.GetLastPInvokeError()` (not `Marshal.GetLastWin32Error()`).
- Commit after each phase checkpoint to maintain a bisectable history.
- Stop at any phase checkpoint to validate independently before proceeding.

