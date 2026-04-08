# Implementation Plan: 010 IMAP Email Sync

**Branch**: `feature/010-011-sync-pipeline` | **Date**: 2026-04-07 | **Spec**: `.specify/specs/010-imap-email-sync/spec.md`  
**Input**: `.specify/specs/010-imap-email-sync/spec.md` + `.specify/specs/010-imap-email-sync/clarify.md`

---

## Summary

Implement IMAP mailbox synchronisation that polls configured mailboxes, downloads
email attachments matching per-importer filters, and persists them as `Report`
records (status `Init`) ready for downstream pipeline processing.  
Core strategy: Application layer defines `IMailboxSyncService` + `SyncMailboxCommand`;
Infrastructure provides `ImapMailboxSyncService` using MailKit; cursor advances from
date-based to UID-based after the first successful sync.  
As of the plan date, **the core implementation is already in place** across Domain,
Application, and Infrastructure. The remaining work is primarily tests and Desktop
integration.

---

## Technical Context

| Attribute | Value |
|---|---|
| **Language / Version** | C# 12 / .NET 8 |
| **Primary Dependencies** | MailKit 4.*, EF Core 8, xUnit 2.*, NSubstitute 5.*, FluentAssertions 6.* |
| **Storage** | SQLite via EF Core; Reports table with UNIQUE index `(ImporterId, ReportName)` |
| **Testing** | xUnit + FluentAssertions + NSubstitute; EF Core InMemory + SQLite for integration |
| **Target Platform** | Windows + macOS (GitHub Actions CI matrix) |
| **Project Type** | Desktop application (Avalonia / ReactiveUI) |
| **Performance Goals** | Sync is background I/O; progress surfaced via `IProgress<SyncProgress>` |
| **Constraints** | No blocking calls (`.Result`/`.Wait()`); `decimal` for money; `DateOnly` for dates; local-only data; IMAP passwords in OS credential store only |
| **Scale / Scope** | Personal-use tool; handful of mailboxes and importers |

---

## Constitution Check

*Gate status: ✅ all principles satisfied.*

| # | Principle | Status | Notes |
|---|---|---|---|
| I | Clean Architecture boundary | ✅ | Domain ← Application ← Infrastructure ← Desktop. `ImapMailboxSyncService` is in Infrastructure only; Application holds the interface `IMailboxSyncService`. |
| II | Local-First Security | ✅ | Passwords retrieved exclusively via `ICredentialStore` (`Rentier/Mailbox/{id}/password`). Never stored in SQLite. No telemetry. |
| III | Financial / Temporal Correctness | ✅ | No monetary values in this feature. All dates are `DateOnly`; boundary conversion is `since.ToDateTime(TimeOnly.MinValue)` in Infrastructure only. |
| IV | Async / UI Responsiveness | ✅ | All I/O is `async Task`. `IProgress<SyncProgress>` surfaced to Desktop. No `.Result`/`.Wait()` calls. |
| V | Specification-Driven Quality Gates | ✅ | Feature mapped to spec tasks. Domain rule coverage and Application ≥ 90% coverage required. |

---

## Project Structure

### Documentation (this feature)

```text
.specify/specs/010-imap-email-sync/
├── plan.md             ← this file
├── clarify.md          ← resolved decisions (13 items)
├── spec.md             ← full feature specification
├── research.md         ← Phase 0 (below — all resolved via clarify.md)
├── data-model.md       ← Phase 1 (below)
└── contracts/
    └── sync-service.md ← Phase 1 interface contract
```

### Source Code (existing layout, relevant paths)

```text
src/
├── Rentier.Domain/
│   ├── Entities/
│   │   └── Report.cs                        ✅ enriched: Status, ReportName, AttachmentContent, MailboxMessageId
│   ├── Enums/
│   │   └── ReportStatus.cs                  ✅ Init=0, Processed=1, Error=2
│   └── ValueObjects/
│       └── MailboxCursor.cs                 ✅ record(DateOnly? LastSyncDate, long? LastUid)
├── Rentier.Application/
│   ├── Commands/
│   │   └── SyncMailboxCommand.cs            ✅ record(IProgress<SyncProgress>? Progress)
│   ├── DTOs/
│   │   ├── SyncProgress.cs                  ✅ record(Total, Processed, CurrentFile, IsComplete)
│   │   └── SyncResult.cs                    ✅ record(ReportsCreated, Errors)
│   ├── Handlers/
│   │   └── SyncMailboxCommandHandler.cs     ✅ groups importers by MailboxId, delegates to IMailboxSyncService
│   ├── Interfaces/
│   │   ├── ICommandHandler.cs               ✅
│   │   └── IMailboxSyncService.cs           ✅ SyncAsync(mailbox, importers, progress, ct)
│   └── Repositories/
│       └── IReportRepository.cs             ✅ GetAll/ById/ByImporter/ByStatus, ExistsByImporterAndName, Add/Update/Delete
├── Rentier.Infrastructure/
│   ├── Sync/
│   │   └── ImapMailboxSyncService.cs        ✅ full IMAP impl: connect, search, dedup, cursor advance
│   ├── Repositories/
│   │   └── ReportRepository.cs             ✅ full CRUD + GetByStatus + ExistsByImporterAndName
│   ├── Persistence/
│   │   ├── Configurations/
│   │   │   └── ReportConfiguration.cs      ✅ UNIQUE index, BLOB, FK cascade
│   │   └── Migrations/
│   │       └── 20260407120000_0007_ReportEnrichment.cs  ✅ Reports table
│   └── InfrastructureServiceExtensions.cs  ✅ IMailboxSyncService + ICommandHandler<SyncMailboxCommand> registered
└── Rentier.Desktop/
    └── ViewModels/
        └── ReportsViewModel.cs             ⬜ placeholder — needs sync trigger + progress UX

tests/
├── Rentier.Domain.Tests/
│   └── ReportTests.cs                      ⬜ MISSING — Report.Create, SetStatus, name validation
├── Rentier.Application.Tests/
│   └── SyncMailboxCommandHandlerTests.cs   ⬜ MISSING — grouping, missing mailbox, error aggregation
├── Rentier.Infrastructure.Tests/
│   ├── ImapMailboxSyncServiceTests.cs      ⬜ MISSING — mocked MailKit + repos, cursor transitions, dedup
│   ├── ReportRepositoryTests.cs            ⬜ MISSING — EF SQLite in-memory
│   └── ImapSyncIntegrationTests.cs         ⬜ MISSING — placeholder [Trait("Category","Integration")]
└── Rentier.Desktop.Tests/
    └── ReportsViewModelTests.cs            ⬜ MISSING — sync command binding, progress updates
```

---

## Phase 0: Research

> All questions resolved in `clarify.md` (13 decisions) before coding began.
> No unknowns remain. See full resolution table in clarify.md.

**Key decisions for implementation reference:**

| Decision | Choice | Rationale |
|---|---|---|
| MailKit layer | Infrastructure only; `IMailboxSyncService` in Application | Constitution I — no I/O package in Application |
| Cursor transition | `LastUid == null` → `DeliveredAfter(InitialSyncDate)`; after first sync → UID range | Prevents re-processing; resilient to server clock drift |
| Cursor advance timing | Only on full success; not advanced on partial/error | Constitution V — data integrity over throughput |
| Duplicate guard | `ExistsByImporterAndNameAsync` check before `Report.Create`; DB UNIQUE fallback | Belt-and-suspenders; idempotent on retry |
| Credential key | `Rentier/Mailbox/{mailboxId}/password` | Matches OS keychain key convention already in use |
| ReportName format | `"{subject}_{filename}"` truncated to 500 chars | Fits UNIQUE index column width |
| Error isolation | Per-mailbox; errors on one mailbox do not stop others; aggregated in `SyncResult.Errors` | Resilience for multi-account users |
| Attachment storage | `byte[]` BLOB column on `Reports` table | Keeps attachment available offline, no secondary store |
| Search scope | INBOX only | Matches IBKR delivery behaviour |
| `MailboxMessageId` type | `long?` (UID stored as long) | SQLite INTEGER; MailKit `UniqueId` is `uint` — upcasted safely |

**Research output**: all decisions encoded above and in clarify.md — no `research.md` stub required.

---

## Phase 1: Design & Contracts

### Data Model

See `data-model.md` (generated alongside this plan).

**Summary of entities involved:**

| Entity | Layer | Key Fields for This Feature | Migration |
|---|---|---|---|
| `Report` | Domain | `Id`, `ImporterId`, `ImportDate`, `Status`, `ReportName`, `AttachmentContent`, `MailboxMessageId` | `0007_ReportEnrichment` ✅ |
| `ReportStatus` | Domain (enum) | `Init=0`, `Processed=1`, `Error=2` | — |
| `Mailbox` | Domain | `Id`, `Host`, `Port`, `Username`, `InitialSyncDate`, `Cursor` | `0004_MailboxConfiguration` ✅ |
| `MailboxCursor` | Domain (VO) | `LastSyncDate?`, `LastUid?` | stored as owned columns on Mailboxes |
| `Importer` | Domain | `MailboxId?`, `FromFilter`, `SubjectFilter`, `AttachmentRegex` | `0005_ImporterConfiguration` ✅ |

**State machine — `Report.Status`:**

```text
(created) → Init
              │
       ProcessReports command
              │
     ┌────────┴────────┐
  Processed           Error
```

Transitions enforced only by command handlers; domain entity exposes `SetStatus(ReportStatus)`.

**Cursor state machine — `MailboxCursor`:**

```text
New mailbox:     Cursor(LastSyncDate=InitialSyncDate, LastUid=null)
                    │
              first SyncAsync success
                    │
                    ▼
After first sync: Cursor(LastSyncDate=<unchanged>, LastUid=<maxUid>)
                    │
           subsequent SyncAsync success
                    │
                    ▼
Subsequent:       Cursor(LastSyncDate=<unchanged>, LastUid=<new maxUid>)
```

Cursor is only mutated via `Mailbox.UpdateCursor(MailboxCursor)`.

### Interface Contracts

See `contracts/sync-service.md` (generated alongside this plan).

**`IMailboxSyncService`** (Application/Interfaces):

```csharp
/// <summary>
/// Connects to a mailbox via IMAP, downloads attachments matching each importer's
/// filters, and persists new Report records.
/// </summary>
public interface IMailboxSyncService
{
    Task<Result<SyncResult, Error>> SyncAsync(
        Mailbox mailbox,
        IReadOnlyList<Importer> importers,
        IProgress<SyncProgress>? progress,
        CancellationToken ct);
}
```

Pre-conditions:
- `mailbox` is non-null and has `Host`, `Port`, `Username` populated
- `importers` is non-empty and all have `MailboxId == mailbox.Id`
- OS credential store has a password under key `Rentier/Mailbox/{mailbox.Id}/password`

Post-conditions (success path):
- For each importer, all IMAP messages matching filters and `AttachmentRegex` are persisted as `Report` records with status `Init`
- Duplicate `(ImporterId, ReportName)` pairs are silently skipped
- `mailbox.Cursor.LastUid` is advanced to the highest UID seen
- `IMailboxRepository.UpdateAsync` is called once per mailbox

Post-conditions (failure path):
- Returns `Result.Failure(Error.Infrastructure(...))` — cursor NOT advanced
- Per-importer exceptions are caught and added to `SyncResult.Errors` (partial success)
- Caller aggregates errors via `SyncMailboxCommandHandler`

**`SyncMailboxCommand` / `SyncMailboxCommandHandler`**:

```csharp
// Command
public sealed record SyncMailboxCommand(IProgress<SyncProgress>? Progress = null);

// Result DTO
public sealed record SyncResult(int ReportsCreated, IReadOnlyList<string> Errors);

// Progress DTO
public sealed record SyncProgress(int Total, int Processed, string? CurrentFile, bool IsComplete);
```

Handler semantics: load all importers → filter to those with `MailboxId` set → group by
`MailboxId` → load each `Mailbox` → call `IMailboxSyncService.SyncAsync` → aggregate
`ReportsCreated` and `Errors` → return single `Result<SyncResult, Error>`.

---

## Implementation Status

### ✅ Done (core implementation complete)

| Component | File | Status |
|---|---|---|
| `ReportStatus` enum | `Domain/Enums/ReportStatus.cs` | ✅ |
| `Report` entity enrichment | `Domain/Entities/Report.cs` | ✅ |
| `MailboxCursor` VO | `Domain/ValueObjects/MailboxCursor.cs` | ✅ |
| `SyncProgress` DTO | `Application/DTOs/SyncProgress.cs` | ✅ |
| `SyncResult` DTO | `Application/DTOs/SyncResult.cs` | ✅ |
| `SyncMailboxCommand` | `Application/Commands/SyncMailboxCommand.cs` | ✅ |
| `IMailboxSyncService` interface | `Application/Interfaces/IMailboxSyncService.cs` | ✅ |
| `IReportRepository` additions | `Application/Repositories/IReportRepository.cs` | ✅ |
| `SyncMailboxCommandHandler` | `Application/Handlers/SyncMailboxCommandHandler.cs` | ✅ |
| `ReportConfiguration` (EF) | `Infrastructure/Persistence/Configurations/ReportConfiguration.cs` | ✅ |
| Migration `0007_ReportEnrichment` | `Infrastructure/Persistence/Migrations/…` | ✅ |
| `ReportRepository` | `Infrastructure/Repositories/ReportRepository.cs` | ✅ |
| `ImapMailboxSyncService` | `Infrastructure/Sync/ImapMailboxSyncService.cs` | ✅ |
| DI registration | `Infrastructure/InfrastructureServiceExtensions.cs` | ✅ |

### ⬜ Remaining Work (tests + desktop)

| Component | File | Priority |
|---|---|---|
| `ReportTests` (Domain) | `tests/Rentier.Domain.Tests/ReportTests.cs` | P0 — domain rule coverage required |
| `SyncMailboxCommandHandlerTests` | `tests/Rentier.Application.Tests/SyncMailboxCommandHandlerTests.cs` | P0 — Application ≥90% coverage |
| `ReportRepositoryTests` | `tests/Rentier.Infrastructure.Tests/ReportRepositoryTests.cs` | P1 — integration with EF SQLite |
| `ImapMailboxSyncServiceTests` | `tests/Rentier.Infrastructure.Tests/ImapMailboxSyncServiceTests.cs` | P1 — mocked MailKit, cursor + dedup |
| `ImapSyncIntegrationTests` | `tests/Rentier.Infrastructure.Tests/ImapSyncIntegrationTests.cs` | P2 — placeholder class only |
| `ReportsViewModel` (Desktop) | `src/Rentier.Desktop/ViewModels/ReportsViewModel.cs` | P1 — sync trigger + progress UX |
| `ReportsViewModelTests` | `tests/Rentier.Desktop.Tests/ReportsViewModelTests.cs` | P1 — ViewModel test coverage |

---

## Test Plan

### Domain Tests — `ReportTests`

Naming convention: `MethodName_StateUnderTest_ExpectedBehavior`

| Test | Scenario |
|---|---|
| `Create_ValidArgs_ReturnsReportWithInitStatus` | happy path |
| `Create_EmptyReportName_ThrowsDomainException` | name guard |
| `Create_ReportNameOver500Chars_ThrowsDomainException` | length guard |
| `Create_NullAttachment_ReportContentIsNull` | nullable attachment |
| `SetStatus_ValidTransition_UpdatesStatus` | status mutation |
| `Create_SetsImportDateToToday` | temporal correctness |

### Application Tests — `SyncMailboxCommandHandlerTests`

NSubstitute mocks: `IImporterRepository`, `IMailboxRepository`, `IMailboxSyncService`

| Test | Scenario |
|---|---|
| `HandleAsync_NoImportersWithMailbox_ReturnsZeroCreated` | no-op when no mailbox-linked importers |
| `HandleAsync_TwoMailboxes_CallsSyncTwice` | grouping by MailboxId |
| `HandleAsync_MissingMailbox_AddsErrorToResult` | mailbox not found |
| `HandleAsync_SyncServiceFailure_AggregatesError` | failure propagation |
| `HandleAsync_PartialSuccess_ReturnsMixedResult` | one mailbox ok, one fails |
| `HandleAsync_PassesProgressToSyncService` | progress threading |

### Infrastructure Tests — `ImapMailboxSyncServiceTests`

Mocked using NSubstitute wrappers over `IReportRepository`, `IMailboxRepository`,
`ICredentialStore`. MailKit classes (`ImapClient`, `IMailFolder`) are not directly
mockable via interface — use a seam or factory pattern in `ImapMailboxSyncService`
via `protected virtual ImapClient CreateClient()` override in test subclass.

| Test | Scenario |
|---|---|
| `SyncAsync_NoPassword_ReturnsFailure` | missing credential guard |
| `SyncAsync_FirstSync_UsesDateQuery` | cursor `LastUid == null` → `DeliveredAfter` |
| `SyncAsync_SubsequentSync_UsesUidRange` | cursor `LastUid = 42` → UID > 42 |
| `SyncAsync_MatchingAttachment_CreatesReport` | happy path report creation |
| `SyncAsync_DuplicateReport_SkipsAdd` | `ExistsByImporterAndName = true` → no add |
| `SyncAsync_AdvancesCursorAfterSuccess` | `UpdateAsync` called with new max UID |
| `SyncAsync_ImapException_ReturnFailureNoAdvance` | cursor not advanced on exception |
| `SyncAsync_FromFilterApplied_QueryContainsFromFilter` | filter AND composition |
| `SyncAsync_EmptyAttachmentRegex_NoAttachmentsReturned` | empty regex skips extraction |
| `BuildReportName_LongName_TruncatesTo500` | static helper method |

### Infrastructure Integration — `ImapSyncIntegrationTests`

Placeholder only:
```csharp
[Trait("Category", "Integration")]
public sealed class ImapSyncIntegrationTests
{
    // TODO: requires live IMAP test server (GreenMail / Docker)
    [Fact(Skip = "Requires live IMAP server")]
    public Task SyncAsync_LiveServer_ReturnsReports() => Task.CompletedTask;
}
```

### Infrastructure Tests — `ReportRepositoryTests`

EF Core SQLite in-memory (using `AppDbContext` with `UseSqlite("DataSource=:memory:")`):

| Test | Scenario |
|---|---|
| `AddAsync_ValidReport_PersistedInDb` | basic add |
| `GetByStatusAsync_InitStatus_ReturnsMatchingReports` | status filter |
| `ExistsByImporterAndNameAsync_Existing_ReturnsTrue` | dedup query |
| `ExistsByImporterAndNameAsync_Missing_ReturnsFalse` | negative case |
| `AddAsync_DuplicateImporterAndName_ThrowsDbUpdateException` | UNIQUE constraint enforcement |

### Desktop Tests — `ReportsViewModelTests`

NSubstitute mock for `ICommandHandler<SyncMailboxCommand, Result<SyncResult, Error>>`

| Test | Scenario |
|---|---|
| `SyncCommand_Execute_CallsHandlerHandleAsync` | command wired correctly |
| `SyncCommand_InProgress_IsBusyIsTrue` | busy state |
| `Progress_Update_PropagatesProgressToProperty` | progress binding |
| `SyncCommand_Failure_ErrorMessageVisible` | error display |

---

## Desktop Integration Notes

`ReportsViewModel` is currently a stub. Needed additions:

```csharp
public sealed class ReportsViewModel : ReactiveObject
{
    // Injected via DI
    private readonly ICommandHandler<SyncMailboxCommand, Result<SyncResult, Error>> _syncHandler;

    // Bindable state
    [Reactive] public bool IsSyncing { get; private set; }
    [Reactive] public string? StatusMessage { get; private set; }
    [Reactive] public int Progress { get; private set; }

    // ReactiveCommand (CreateFromTask — never blocking)
    public ReactiveCommand<Unit, Unit> SyncCommand { get; }
}
```

- `SyncCommand` → `ReactiveCommand.CreateFromTask` wrapping `_syncHandler.HandleAsync`
- `IProgress<SyncProgress>` implementation: `Progress<SyncProgress>` with callback posting to `RxApp.MainThreadScheduler`
- Error display: bind `SyncResult.Errors` to an observable collection or inline message

---

## Complexity Justification

> No constitution violations. No additional projects introduced.
> All complexity is justified by specification requirements.

---

## Definition of Done Checklist

- [ ] `ReportTests` — all scenarios green, 100% rule/state coverage for `Report`
- [ ] `SyncMailboxCommandHandlerTests` — all scenarios green
- [ ] `ReportRepositoryTests` — all scenarios green
- [ ] `ImapMailboxSyncServiceTests` — all scenarios green
- [ ] `ImapSyncIntegrationTests` — placeholder committed (skip annotation)
- [ ] `ReportsViewModel` enriched with sync command + progress binding
- [ ] `ReportsViewModelTests` — all scenarios green
- [ ] `dotnet build` — zero warnings (CA1416 pragma for `OsCredentialStore` already present)
- [ ] CI green on Windows + macOS matrix
- [ ] PR linked to spec task, merged to `develop`
