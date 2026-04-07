# Tasks: 010 IMAP Email Sync

**Input**: `.specify/specs/010-imap-email-sync/` — spec.md, plan.md, data-model.md, contracts/sync-service.md, clarify.md  
**Branch**: `feature/010-011-sync-pipeline`  
**Source state**: master (clean) — nothing implemented yet; all tasks are new

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: User story label — US1/US2/US3/US4
- Exact file paths are required in every task description

---

## User Stories

| Label | Goal | Independent Test |
|---|---|---|
| **US1** | Report domain model enriched + persisted with status filtering | `ReportRepositoryTests` + `ReportTests` all green |
| **US2** | `SyncMailboxCommand` executes and aggregates results across all configured mailboxes | `SyncMailboxCommandHandlerTests` all green |
| **US3** | IMAP mailboxes connected, attachments downloaded, deduplicated, stored as `Report` records | `ImapMailboxSyncServiceTests` all green; build succeeds |
| **US4** | User can trigger sync from Reports view and observe live progress | `ReportsViewModelTests` all green |

---

## Phase 1: Domain Foundation

**Purpose**: Enrich the `Report` aggregate and introduce `ReportStatus` enum. These are pure domain changes with zero external dependencies — they unblock all subsequent phases.

- [ ] T001 Create `ReportStatus` enum (`Init=0, Processed=1, Error=2`) in `src/Rentier.Domain/Enums/ReportStatus.cs`
- [ ] T002 Add EF-required private parameterless constructor to `Report` entity in `src/Rentier.Domain/Entities/Report.cs`. Also **make the existing `public Report(Guid id, DateOnly importDate, Guid importerId)` constructor `private`** — all external construction MUST go through `Report.Create()`. This prevents callers from creating a `Report` with null `ReportName` or unset `Status`.
- [ ] T003 Add properties `Status` (`ReportStatus`), `ReportName` (`string`), `AttachmentContent` (`byte[]?`), `MailboxMessageId` (`long?`) to `Report` in `src/Rentier.Domain/Entities/Report.cs`
- [ ] T004 Add `Report.SetStatus(ReportStatus status)` instance method to `Report` in `src/Rentier.Domain/Entities/Report.cs`
- [ ] T005 Add `Report.Create(Guid importerId, string reportName, byte[]? attachmentContent, long? mailboxMessageId)` static factory with validation (not-empty name, max 500 chars, sets `Status = Init`, `ImportDate = DateOnly.FromDateTime(DateTime.UtcNow)`, `Id = Guid.NewGuid()`) in `src/Rentier.Domain/Entities/Report.cs`

**Checkpoint**: Domain compiles; `Report` has all new fields and factory. No other projects need changes yet.

---

## Phase 2: Persistence Foundation (Blocking Prerequisite)

**Purpose**: EF configuration, `AppDbContext` update, and migration 0007 must be in place before any repository or IMAP code can be exercised. Blocks US1 integration tests and US3.

**⚠️ CRITICAL**: Phase 3+ cannot begin integration testing until this phase is complete.

- [ ] T006 Create `ReportConfiguration` EF type configuration in `src/Rentier.Infrastructure/Persistence/Configurations/ReportConfiguration.cs` — `HasKey`, `Property(ReportName).HasMaxLength(500).IsRequired()`, `Property(AttachmentContent)` nullable, `HasIndex(ImporterId+ReportName).IsUnique()`, `HasOne<Importer>().WithMany().HasForeignKey(r => r.ImporterId).OnDelete(DeleteBehavior.Cascade)`
- [ ] T007 Add `public DbSet<Report> Reports => Set<Report>();` to `AppDbContext` in `src/Rentier.Infrastructure/Persistence/AppDbContext.cs`
- [ ] T008 Generate EF migration `0007_ReportEnrichment` by running `dotnet ef migrations add 0007_ReportEnrichment` from `src/Rentier.Infrastructure/` — creates `Reports` table with UNIQUE index `IX_Reports_ImporterId_ReportName` and FK `FK_Reports_Importers_ImporterId ON DELETE CASCADE`

**Checkpoint**: `dotnet build` succeeds; `dotnet ef migrations list` shows 0007 pending.

---

## Phase 3: US1 — Report Persistence Layer

**Goal**: `Report` records can be added, retrieved by status, checked for duplicates, updated, and deleted via `IReportRepository` + `ReportRepository`. Fully testable in isolation via EF SQLite in-memory.

**Independent Test**: Run `ReportRepositoryTests` and `ReportTests` — all green.

### Implementation — US1

- [ ] T009 [US1] Add `GetByStatusAsync(ReportStatus status, CancellationToken ct)`, `ExistsByImporterAndNameAsync(Guid importerId, string reportName, CancellationToken ct)`, and `UpdateAsync(Report report, CancellationToken ct)` to `IReportRepository` in `src/Rentier.Application/Repositories/IReportRepository.cs`
- [ ] T010 [US1] Implement `ReportRepository` with full CRUD matching `IReportRepository` contract (use `FindAsync`+`Remove` for delete; detach stale tracker entry before `Update`; `AsNoTracking()` for read queries) in `src/Rentier.Infrastructure/Repositories/ReportRepository.cs`
- [ ] T011 [US1] Register `IReportRepository → ReportRepository` with `AddTransient` in `InfrastructureServiceExtensions.AddInfrastructureServices()` in `src/Rentier.Infrastructure/InfrastructureServiceExtensions.cs`

### Tests — US1

- [ ] T012 [P] [US1] Write `ReportTests` covering `Create_ValidArgs_ReturnsReportWithInitStatus`, `Create_EmptyReportName_ThrowsDomainException`, `Create_ReportNameOver500Chars_ThrowsDomainException`, `Create_NullAttachment_ReportContentIsNull`, `SetStatus_ValidTransition_UpdatesStatus`, `Create_SetsImportDateToToday` in `tests/Rentier.Domain.Tests/ReportTests.cs`
- [ ] T013 [P] [US1] Write `ReportRepositoryTests` using `IAsyncLifetime` + `new SqliteConnection("Data Source=:memory:")` + `AppDbContext` covering: `AddAsync_ValidReport_PersistedInDb`, `GetByStatusAsync_InitStatus_ReturnsMatchingReports`, `ExistsByImporterAndNameAsync_Existing_ReturnsTrue`, `ExistsByImporterAndNameAsync_Missing_ReturnsFalse`, `AddAsync_DuplicateImporterAndName_ThrowsDbUpdateException` in `tests/Rentier.Infrastructure.Tests/ReportRepositoryTests.cs`

**Checkpoint**: `dotnet test tests/Rentier.Domain.Tests` and `dotnet test tests/Rentier.Infrastructure.Tests` green for new test classes.

---

## Phase 4: US2 — Sync Command Pipeline

**Goal**: `SyncMailboxCommand` loads all importers, groups by mailbox, delegates to `IMailboxSyncService` per mailbox, and aggregates a `SyncResult`. Fully testable via mocked dependencies.

**Independent Test**: Run `SyncMailboxCommandHandlerTests` — all green (no real IMAP or DB needed).

### Implementation — US2

- [ ] T014 [P] [US2] Create `SyncProgress` record (`int Total, int Processed, string? CurrentFile, bool IsComplete`) in `src/Rentier.Application/DTOs/SyncProgress.cs`
- [ ] T015 [P] [US2] Create `SyncResult` record (`int ReportsCreated, IReadOnlyList<string> Errors`) in `src/Rentier.Application/DTOs/SyncResult.cs`
- [ ] T016 [P] [US2] Create `SyncMailboxCommand` record (`IProgress<SyncProgress>? Progress = null`) in `src/Rentier.Application/Commands/SyncMailboxCommand.cs`
- [ ] T017 [P] [US2] Create `IMailboxSyncService` interface with `Task<Result<SyncResult, Error>> SyncAsync(Mailbox mailbox, IReadOnlyList<Importer> importers, IProgress<SyncProgress>? progress, CancellationToken ct)` in `src/Rentier.Application/Interfaces/IMailboxSyncService.cs`
- [ ] T018 [US2] Implement `SyncMailboxCommandHandler` implementing `ICommandHandler<SyncMailboxCommand, Result<SyncResult, Error>>` — inject `IImporterRepository` + `IMailboxRepository` + `IMailboxSyncService`; load all importers; filter to those with `MailboxId != null`; group by `MailboxId`; for each group load mailbox via `GetByIdAsync` (missing → add error string); call `SyncAsync`; aggregate `ReportsCreated` and `Errors`; return `Result.Success(aggregated)` in `src/Rentier.Application/Handlers/SyncMailboxCommandHandler.cs`

### Tests — US2

- [ ] T019 [US2] Write `SyncMailboxCommandHandlerTests` using `NSubstitute` mocks for `IImporterRepository`, `IMailboxRepository`, `IMailboxSyncService` covering: `HandleAsync_NoImportersWithMailbox_ReturnsZeroCreated`, `HandleAsync_TwoMailboxes_CallsSyncTwice`, `HandleAsync_MissingMailbox_AddsErrorToResult`, `HandleAsync_SyncServiceFailure_AggregatesError`, `HandleAsync_PartialSuccess_ReturnsMixedResult`, `HandleAsync_PassesProgressToSyncService` in `tests/Rentier.Application.Tests/SyncMailboxCommandHandlerTests.cs`

**Checkpoint**: `dotnet test tests/Rentier.Application.Tests` green for `SyncMailboxCommandHandlerTests`.

---

## Phase 5: US3 — IMAP Sync Implementation

**Goal**: `ImapMailboxSyncService` connects to IMAP via MailKit, builds cursor-aware search queries, downloads attachments matching `AttachmentRegex`, deduplicates via `ExistsByImporterAndNameAsync`, persists `Report` records, advances cursor only on full success. Fully testable via mock seam.

**Independent Test**: Run `ImapMailboxSyncServiceTests` — all green.

### Implementation — US3

- [ ] T020 [US3] Add `MailKit` NuGet package reference (`MailKit 4.*`) to `src/Rentier.Infrastructure/Rentier.Infrastructure.csproj` if not already present
- [ ] T021 [US3] Implement `ImapMailboxSyncService` in `src/Rentier.Infrastructure/Sync/ImapMailboxSyncService.cs` with:
  - Constructor injecting `IReportRepository`, `IMailboxRepository`, `ICredentialStore`
  - `protected virtual ImapClient CreateClient()` seam for test overrides
  - IMAP connect via `client.ConnectAsync(host, port, SecureSocketOptions.SslOnConnect, ct)`
  - Auth via `ICredentialStore.GetCredentialAsync($"Rentier/Mailbox/{mailbox.Id}/password", ct)` — return `Failure` immediately if null/empty
  - Open INBOX with `FolderAccess.ReadOnly`
  - Build `SearchQuery`: if `Cursor.LastUid == null` → `DeliveredAfter(Cursor.LastSyncDate ?? mailbox.InitialSyncDate)`; else `Uids(new UniqueIdRange(new UniqueId((uint)(cursor.LastUid.Value + 1)), UniqueId.MaxValue))`
  - AND-compose `FromContains` / `SubjectContains` from importer filters if non-empty
  - For each UID: fetch message (`MessageSummaryItems`), iterate attachments, match against `Importer.AttachmentRegex`
  - Build `reportName = $"{subject}_{filename}"`; truncate to 500 chars
  - Check `ExistsByImporterAndNameAsync` → skip if duplicate
  - `Report.Create(importerId, reportName, content, (long)uid.Id)` → `IReportRepository.AddAsync`
  - Track max UID seen; report progress via `IProgress<SyncProgress>`
  - On all-success: `mailbox.UpdateCursor(new MailboxCursor(cursor.LastSyncDate, maxUid))` → `IMailboxRepository.UpdateAsync`
  - Per-importer exception caught → add to errors list; on outer exception → `Result.Failure(Error.Infrastructure(...))`
- [ ] T022 [US3] Register `IMailboxSyncService → ImapMailboxSyncService` and `ICommandHandler<SyncMailboxCommand, Result<SyncResult, Error>> → SyncMailboxCommandHandler` with `AddTransient` in `src/Rentier.Infrastructure/InfrastructureServiceExtensions.cs`

### Tests — US3

- [ ] T023 [US3] Write `ImapMailboxSyncServiceTests` using a test-subclass overriding `CreateClient()` to return a mock `ImapClient` (or stub), with `NSubstitute` for `IReportRepository`, `IMailboxRepository`, `ICredentialStore` covering: `SyncAsync_NoPassword_ReturnsFailure`, `SyncAsync_FirstSync_UsesDateQuery`, `SyncAsync_SubsequentSync_UsesUidRange`, `SyncAsync_MatchingAttachment_CreatesReport`, `SyncAsync_DuplicateReport_SkipsAdd`, `SyncAsync_AdvancesCursorAfterSuccess`, `SyncAsync_ImapException_ReturnFailureNoAdvance`, `SyncAsync_FromFilterApplied_QueryContainsFromFilter`, `SyncAsync_EmptyAttachmentRegex_NoAttachmentsReturned`, `BuildReportName_LongName_TruncatesTo500` in `tests/Rentier.Infrastructure.Tests/ImapMailboxSyncServiceTests.cs`
- [ ] T024 [P] [US3] Write `ImapSyncIntegrationTests` placeholder class with `[Trait("Category","Integration")]` and a single `[Fact(Skip = "Requires live IMAP server")]` test returning `Task.CompletedTask` in `tests/Rentier.Infrastructure.Tests/ImapSyncIntegrationTests.cs`

**Checkpoint**: `dotnet test tests/Rentier.Infrastructure.Tests` green for new test classes; `dotnet build` zero warnings.

---

## Phase 6: US4 — Desktop Sync UX

**Goal**: User can press a "Sync" button in the Reports view, see live progress, and see an error message if sync fails. `ReportsViewModel` replaces its placeholder stub with a real `ReactiveCommand` wired to `SyncMailboxCommandHandler`.

**Independent Test**: Run `ReportsViewModelTests` — all green.

### Implementation — US4

- [ ] T025 [US4] Rewrite `ReportsViewModel` in `src/Rentier.Desktop/ViewModels/ReportsViewModel.cs`:
  - Inject `ICommandHandler<SyncMailboxCommand, Result<SyncResult, Error>>` via constructor
  - `[Reactive] public bool IsSyncing { get; private set; }`
  - `[Reactive] public string? StatusMessage { get; private set; }`
  - `[Reactive] public int ProgressValue { get; private set; }` (0–100 or 0–Total)
  - `public ReactiveCommand<Unit, Unit> SyncCommand { get; }` created via `ReactiveCommand.CreateFromTask` wrapping `HandleSyncAsync`
  - `IProgress<SyncProgress>` implementation using `Progress<SyncProgress>` with callback marshalled to `RxApp.MainThreadScheduler` (via `ObservableEx` or `SchedulerExtensions`)
  - On failure: populate `StatusMessage` with joined error strings
  - `IsSyncing` true while command executes, false on completion

### Tests — US4

- [ ] T026 [US4] Write `ReportsViewModelTests` with `NSubstitute` mock for `ICommandHandler<SyncMailboxCommand, Result<SyncResult, Error>>` covering: `SyncCommand_Execute_CallsHandlerHandleAsync`, `SyncCommand_InProgress_IsBusyIsTrue`, `Progress_Update_PropagatesProgressToProperty`, `SyncCommand_Failure_ErrorMessageVisible` in `tests/Rentier.Desktop.Tests/ReportsViewModelTests.cs`

**Checkpoint**: `dotnet test tests/Rentier.Desktop.Tests` green for `ReportsViewModelTests`.

---

## Phase 7: Polish & Cross-Cutting

- [ ] T027 [P] Verify `dotnet build` with `-warnaserror` produces zero warnings across all four projects
- [ ] T028 [P] Run full test suite `dotnet test` and confirm all tests pass on Windows; fix any flaky ordering issues in `ReportRepositoryTests`
- [ ] T029 [P] Confirm `DiRegistrationSmokeTests` in `tests/Rentier.Application.Tests/DiRegistrationSmokeTests.cs` still green after new DI registrations are added. **Extend the smoke test** to also assert: `sp.GetRequiredService<IReportRepository>()`, `sp.GetRequiredService<IMailboxSyncService>()`, and `sp.GetRequiredService<ICommandHandler<SyncMailboxCommand, Result<SyncResult, Error>>>()` — these will catch any DI misconfiguration in the new wiring.
- [ ] T030 Verify EF migration 0007 applies cleanly on a fresh database: delete local SQLite file, launch app, confirm `Reports` table created with correct schema and UNIQUE index

---

## Dependencies & Execution Order

### Phase Dependencies

```
Phase 1 (Domain) ──────────────────────────────────────────┐
                                                            │
Phase 2 (Persistence foundation) ◄── Phase 1 complete ─────┤
                                                            │
Phase 3 (US1 - Repository)  ◄── Phase 2 complete ──────────┤
Phase 4 (US2 - Command)     ◄── Phase 1 complete           │  ← US2 only needs Domain
Phase 5 (US3 - IMAP impl)   ◄── Phase 2+3+4 complete ──────┤  ← IMAP needs all repo interfaces
Phase 6 (US4 - Desktop)     ◄── Phase 4+5 complete         │  ← VM needs command + result types
Phase 7 (Polish)            ◄── All phases complete ────────┘
```

### Within-Phase Dependencies

- **Phase 1**: T001 → T002 → T003 → T004 → T005 (all touch `Report.cs`, must be sequential)
- **Phase 2**: T006 can be parallel with T001–T005; T007 depends on T006; T008 depends on T007
- **Phase 3**: T009 → T010 → T011 (interface before impl before tests); T012 parallel to T009–T011 (different file)
- **Phase 4**: T014/T015/T016/T017 all parallel (different files); T018 depends on all four; T019 depends on T018
- **Phase 5**: T020 → T021 → T022 (MailKit ref before impl before DI); T023 depends on T021; T024 is independent [P]
- **Phase 6**: T025 → T026

### User Story Independence

- **US1** (T009–T013): Independently testable after Phase 2. No dependency on US2/US3/US4.
- **US2** (T014–T019): Independently testable after Phase 1 (only needs domain types + mocks). No dependency on US1 infrastructure.
- **US3** (T020–T024): Depends on US1 (IReportRepository) and US2 (IMailboxSyncService interface). Cannot start until US1 interface (T009) is done.
- **US4** (T025–T026): Depends on US2 command/result types. Can start as soon as T014–T016 are done.

---

## Parallel Opportunities

```
After Phase 1 completes (T001-T005):
  ├── Thread A: T006 → T007 → T008 (migration)
  ├── Thread B: T014 + T015 + T016 + T017 (application DTOs/command/interface — all parallel)
  └── Thread C: T012 (ReportTests — domain tests)

After Phase 2 completes (T006-T008) and T009 done:
  ├── Thread A: T010 → T011 (ReportRepository impl + tests)
  └── Thread B: T013 (ReportRepositoryTests — can scaffold alongside T010)

After Phase 4 completes (T014-T019):
  ├── Thread A: T020 → T021 → T022 (IMAP impl)
  └── Thread B: T025 (ReportsViewModel — needs SyncMailboxCommand type from T016)

After T021 done:
  ├── Thread A: T023 (ImapMailboxSyncServiceTests)
  └── Thread B: T024 (ImapSyncIntegrationTests placeholder — fully independent)
```

---

## Implementation Strategy

### MVP First (US1 + US2 + US3 only — no Desktop)

1. Complete Phase 1 (T001–T005) — Domain
2. Complete Phase 2 (T006–T008) — Migration
3. Complete Phase 3 US1 (T009–T013) — Repository + Domain tests
4. Complete Phase 4 US2 (T014–T019) — Command pipeline + tests
5. Complete Phase 5 US3 (T020–T024) — IMAP + DI + tests
6. **VALIDATE**: `dotnet test` all green; manual smoke test with a real mailbox
7. Merge to develop — downstream feature 011 can start consuming `IReportRepository.GetByStatusAsync`

### Full Delivery (all phases)

8. Complete Phase 6 US4 (T025–T026) — Desktop UX
9. Complete Phase 7 (T027–T030) — Polish

---

## Summary

| Metric | Value |
|---|---|
| **Total tasks** | 30 |
| **US1 tasks** | 5 (T009–T013) |
| **US2 tasks** | 6 (T014–T019) |
| **US3 tasks** | 5 (T020–T024) |
| **US4 tasks** | 2 (T025–T026) |
| **Foundation tasks** | 8 (T001–T008) |
| **Polish tasks** | 4 (T027–T030) |
| **Parallelisable [P] tasks** | 10 |
| **Test tasks** | 7 (T012, T013, T019, T023, T024, T026, T029) |
| **Suggested MVP scope** | T001–T024 (US1 + US2 + US3) |
