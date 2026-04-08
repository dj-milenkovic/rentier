# Tasks: 018 Sync Replay Controls

**Input**: Design documents from `.specify/specs/018-sync-replay-controls/`
**Branch**: `feature/018-sync-replay-controls`
**Prerequisites**: plan.md ✅ | spec.md ✅ | research.md ✅ | analysis.md ✅ | data-model.md ✅ | contracts/application-contracts.md ✅

**Tests**: Included per constitution quality gates — Domain 100% rule/state coverage, Application ≥90% handler branching.

**Organization**: Tasks grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story?] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: Which user story this task belongs to (US1–US6)
- Exact file paths included in every implementation task

---

## Phase 1: Setup

**Purpose**: Confirm branch state and orient to modification scope before touching production code.

- [ ] T001 Verify feature branch `feature/018-sync-replay-controls` is active; confirm `.specify/specs/018-sync-replay-controls/` artifacts (spec.md, plan.md, data-model.md, contracts/application-contracts.md, research.md, analysis.md) are all readable and consistent

---

## Phase 2: Foundational — Domain, Application Contracts & Infrastructure Schema

**Purpose**: Cross-cutting primitives that MUST be complete before any user-story phase can begin. All layers depend on the new enums, value object, modified entity shapes, updated command/interface signatures, and migrated database schema.

**⚠️ CRITICAL**: No user-story work (Phase 3+) can begin until this phase is complete.

### Domain Types

- [ ] T002 [P] Create `SyncMode` enum (Incremental = 0, ReplayFromDate = 1, FullReplay = 2) with XML doc comments describing IMAP query behavior per value in `src/Rentier.Domain/Enums/SyncMode.cs`
- [ ] T003 [P] Create `DuplicateStrategy` enum (SkipExisting = 0, CreateNewRevision = 1, ReprocessInPlace = 2) with XML doc comments describing behavior and safety constraints per value in `src/Rentier.Domain/Enums/DuplicateStrategy.cs`
- [ ] T004 Create `SyncParameters` sealed record with properties `Mode`, `Strategy`, `DateOnly? ReplayFromDate`, `Guid? ScopeImporterId`; implement `GetEffectiveStartDate(MailboxCursor cursor)` switch returning cursor.LastSyncDate / ReplayFromDate / null per mode; add domain validation throwing `DomainException` when ReplayFromDate is null or in the future for ReplayFromDate mode, and when ReplayFromDate is non-null for non-ReplayFromDate modes in `src/Rentier.Domain/ValueObjects/SyncParameters.cs`
- [ ] T005 Modify `Mailbox` entity — remove `InitialSyncDate` property; update `Create(host, port, username)` factory to seed cursor with `new MailboxCursor(DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-90), null)` (no InitialSyncDate param); update `UpdateDetails(host, port, username)` to remove InitialSyncDate param; remove all references to InitialSyncDate within the class in `src/Rentier.Domain/Entities/Mailbox.cs`
- [ ] T006 Modify `Report` entity — add `public Guid? OriginalReportId { get; private set; }` property; add `public static Report CreateRevision(Report original, byte[]? newContent)` factory that creates a new Report with OriginalReportId = original.Id, ReportName = `$"{original.ReportName}_rev{DateTime.UtcNow:yyyyMMddHHmmss}"`, Status = Init, same ImporterId and MailboxMessageId, new Id and ImportDate in `src/Rentier.Domain/Entities/Report.cs`

### Application Commands & Contracts

- [ ] T007 [P] Remove `DateOnly InitialSyncDate` parameter from `AddMailboxCommand` record in `src/Rentier.Application/Commands/AddMailboxCommand.cs`
- [ ] T008 [P] Remove `DateOnly InitialSyncDate` parameter from `UpdateMailboxCommand` record in `src/Rentier.Application/Commands/UpdateMailboxCommand.cs`
- [ ] T009 Add `SyncParameters Parameters` as first positional parameter to `SyncMailboxCommand` record in `src/Rentier.Application/Commands/SyncMailboxCommand.cs`
- [ ] T010 Add `SyncParameters Parameters` as first positional parameter to `SyncAllCommand` record in `src/Rentier.Application/Commands/SyncAllCommand.cs`
- [ ] T011 Add `SyncParameters parameters` argument to `IMailboxSyncService.SyncAsync` signature (after `importers`, before `progress`) in `src/Rentier.Application/Interfaces/IMailboxSyncService.cs`
- [ ] T012 [P] Add three new method signatures to `IReportRepository`: `Task<Report?> GetByImporterAndMessageIdAsync(Guid importerId, long mailboxMessageId, CancellationToken ct = default)`, `Task<Report?> GetByImporterAndNameAsync(Guid importerId, string reportName, CancellationToken ct = default)`, `Task<IReadOnlyList<Report>> GetRevisionsAsync(Guid originalReportId, CancellationToken ct = default)` in `src/Rentier.Application/Repositories/IReportRepository.cs`
- [ ] T013 [P] Add method signature `Task<bool> HasAdvancedFilingsAsync(Guid reportId, CancellationToken ct = default)` to `IFilingRepository` (returns true if any Filing for the report has Status != Init) in `src/Rentier.Application/Repositories/IFilingRepository.cs`
- [ ] T014 [P] Extend `SyncResult` record with three new properties: `int ReportsSkipped`, `int RevisionsCreated`, `int ReportsReprocessed` (maintain backward-compatible constructor defaults of 0) in `src/Rentier.Application/DTOs/SyncResult.cs`
- [ ] T015 [P] Extend `SyncAllResult` record with three new properties: `int ReportsSkipped`, `int RevisionsCreated`, `int ReportsReprocessed` in `src/Rentier.Application/DTOs/SyncAllResult.cs`
- [ ] T016 [P] Add `CursorTransition` and `DuplicateHandled` values to `SyncProgressSeverity` enum in `src/Rentier.Application/DTOs/SyncProgressEntry.cs`

### Infrastructure Schema

- [ ] T017 [P] Update `MailboxConfiguration` — remove `builder.Property(m => m.InitialSyncDate).IsRequired()` mapping (and any related HasColumnName/HasDefaultValue calls); ensure owned cursor VO mapping remains intact in `src/Rentier.Infrastructure/Persistence/Configurations/MailboxConfiguration.cs`
- [ ] T018 [P] Update `ReportConfiguration` — add `builder.Property(r => r.OriginalReportId)`, `builder.HasIndex(r => r.OriginalReportId)`, and self-referencing FK `builder.HasOne<Report>().WithMany().HasForeignKey(r => r.OriginalReportId).OnDelete(DeleteBehavior.SetNull)` in `src/Rentier.Infrastructure/Persistence/Configurations/ReportConfiguration.cs`
- [ ] T019 Create EF Core migration `0010_SyncReplayControls` with Up() performing: (1) SQL `UPDATE Mailboxes SET Cursor_LastSyncDate = InitialSyncDate WHERE Cursor_LastSyncDate IS NULL` to preserve un-synced mailbox start dates; (2) drop `InitialSyncDate` column via EF table rebuild for SQLite; (3) add nullable `OriginalReportId TEXT` column to `Reports`; (4) create `IX_Reports_OriginalReportId` index; (5) add self-referencing FK via table rebuild for SQLite — Down() reverses all steps in `src/Rentier.Infrastructure/Persistence/Migrations/0010_SyncReplayControls.cs`

**Checkpoint**: Foundation complete — Domain types, Application contracts, and DB schema are ready. All user-story phases can now begin.

---

## Phase 3: User Story 1 — Incremental Sync Reliability (Priority: P1) 🎯 MVP

**Goal**: The default incremental sync path works end-to-end with the new `SyncParameters` plumbing, monotonic cursor advancement, and cursor-transition logging. All application handlers are wired to pass parameters through.

**Independent Test**: Run sync twice on a configured mailbox; confirm the second run only fetches messages newer than the first run's cursor position; confirm cursor log entries appear with before/after values.

### Tests for User Story 1 ⚠️

> **Write these tests FIRST — ensure they FAIL before implementation**

- [ ] T020 [P] [US1] Write `SyncParametersTests` — `GetEffectiveStartDate` with Incremental mode returns `cursor.LastSyncDate`; default construction yields `Mode=Incremental`, `Strategy=SkipExisting`; `SyncParameters` with null cursor and Incremental mode returns null start date in `tests/Rentier.Domain.Tests/SyncParametersTests.cs`
- [ ] T021 [P] [US1] Extend `MailboxTests` — `Create()` without InitialSyncDate: `Cursor.LastSyncDate` is within 1 day of 90 days ago; `Cursor.LastUid` is null; `UpdateDetails()` no longer accepts or changes InitialSyncDate in `tests/Rentier.Domain.Tests/MailboxTests.cs`
- [ ] T022 [P] [US1] Extend `SyncMailboxCommandHandlerTests` — handler passes `SyncParameters` through to `IMailboxSyncService.SyncAsync`; incremental sync with default parameters calls service with `Mode=Incremental` and `Strategy=SkipExisting` in `tests/Rentier.Application.Tests/SyncMailboxCommandHandlerTests.cs`

### Implementation for User Story 1

- [ ] T023 [P] [US1] Update `AddMailboxCommandHandler` — call `Mailbox.Create(command.Host, command.Port, command.Username)` without InitialSyncDate parameter in `src/Rentier.Application/Handlers/AddMailboxCommandHandler.cs`
- [ ] T024 [P] [US1] Update `UpdateMailboxCommandHandler` — call `mailbox.UpdateDetails(command.Host, command.Port, command.Username)` without InitialSyncDate parameter in `src/Rentier.Application/Handlers/UpdateMailboxCommandHandler.cs`
- [ ] T025 [US1] Update `SyncMailboxCommandHandler` — accept `SyncParameters` from command; pass it to `IMailboxSyncService.SyncAsync`; when `Parameters.ScopeImporterId` is set, filter importers list to only that importer before calling sync service (other importers on same mailbox are skipped); aggregate `ReportsSkipped`, `RevisionsCreated`, `ReportsReprocessed` into result in `src/Rentier.Application/Handlers/SyncMailboxCommandHandler.cs`
- [ ] T026 [US1] Update `SyncAllCommandHandler` — accept `SyncParameters` from command; pass it to `SyncMailboxCommandHandler` Phase 1; pass `Parameters.Strategy` through to `ProcessReportsCommandHandler` Phase 2; aggregate new skip/revision/reprocess counts into `SyncAllResult` in `src/Rentier.Application/Handlers/SyncAllCommandHandler.cs`
- [ ] T027 [P] [US1] Implement new `IReportRepository` methods in `ReportRepository` — `GetByImporterAndMessageIdAsync`: query by `ImporterId == importerId && MailboxMessageId == mailboxMessageId`; `GetByImporterAndNameAsync`: query by `ImporterId == importerId && ReportName == reportName`; `GetRevisionsAsync`: query by `OriginalReportId == originalReportId` in `src/Rentier.Infrastructure/Repositories/ReportRepository.cs`
- [ ] T028 [P] [US1] Implement `HasAdvancedFilingsAsync` in `FilingRepository` — return `true` if any `Filing` with `ReportId == reportId` has `Status != FilingStatus.Init` (i.e., has been Filed or Paid) in `src/Rentier.Infrastructure/Repositories/FilingRepository.cs`
- [ ] T029 [US1] Update `ImapMailboxSyncService.SyncAsync` — add `SyncParameters parameters` argument; compute effective start date via `parameters.GetEffectiveStartDate(mailbox.Cursor)` for IMAP query construction (Incremental path: UID-based or date-based per existing logic); after successful processing: compute `safeCursor = new MailboxCursor(MaxDate(old, new), MaxUid(old, new))`; emit `SyncProgressEntry` with severity `CursorTransition` and message `$"Cursor transition: ({old.LastSyncDate}, {old.LastUid}) → ({safeCursor.LastSyncDate}, {safeCursor.LastUid})"` before calling `mailbox.UpdateCursor(safeCursor)`; do NOT update cursor on `OperationCanceledException` or any exception in `src/Rentier.Infrastructure/Sync/ImapMailboxSyncService.cs`
- [ ] T030 [P] [US1] Extend `ImapMailboxSyncServiceTests` — incremental mode uses cursor for IMAP query; cursor advances monotonically after success; cursor is NOT advanced when sync throws; cursor is NOT advanced on `OperationCanceledException`; `CursorTransition` log entry emitted with correct before/after values in `tests/Rentier.Infrastructure.Tests/ImapMailboxSyncServiceTests.cs`

**Checkpoint**: US1 complete — default incremental sync works end-to-end with new parameters structure, logging, and monotonic cursor.

---

## Phase 4: User Story 2 — Replay from Date (Priority: P2)

**Goal**: User can select "Replay from Date", provide a valid past date, and the system fetches all emails from that date onward regardless of cursor position. Cursor advances to the latest processed message after success — never regresses.

**Independent Test**: Run incremental sync, then run "Replay from Date" with a past date; confirm emails from that date are fetched; confirm cursor is at least as advanced as before replay.

### Tests for User Story 2 ⚠️

> **Write these tests FIRST — ensure they FAIL before implementation**

- [ ] T031 [P] [US2] Extend `SyncParametersTests` — `GetEffectiveStartDate` with `ReplayFromDate` mode returns `ReplayFromDate` value; validation throws `DomainException` when `ReplayFromDate` is null and mode is `ReplayFromDate`; validation throws `DomainException` when `ReplayFromDate` is tomorrow or later; validation throws `DomainException` when `ReplayFromDate` is non-null and mode is `Incremental`; valid construction with today's date succeeds in `tests/Rentier.Domain.Tests/SyncParametersTests.cs`
- [ ] T032 [P] [US2] Extend `ImapMailboxSyncServiceTests` — replay-from-date mode builds IMAP query with `DeliveredAfter(replayDate)`, not UID filter; cursor advances past replay date after successful run; max() prevents regression when replay processes only messages older than current cursor in `tests/Rentier.Infrastructure.Tests/ImapMailboxSyncServiceTests.cs`

### Implementation for User Story 2

- [ ] T033 [US2] Add `ReplayFromDate` validation to `SyncParameters` constructor/factory — throw `DomainException("ReplayFromDate is required for ReplayFromDate mode")` when null; throw `DomainException("ReplayFromDate cannot be in the future")` when > today; throw `DomainException("ReplayFromDate must be null for non-ReplayFromDate modes")` when set for Incremental/FullReplay in `src/Rentier.Domain/ValueObjects/SyncParameters.cs`
- [ ] T034 [US2] Extend `ImapMailboxSyncService` with `ReplayFromDate` query branch — when `parameters.Mode == ReplayFromDate`: use `SearchQuery.DeliveredAfter(replayDate.ToDateTime(TimeOnly.MinValue))` as IMAP query (ignore UID-based filter); log `$"[ReplayFromDate] Starting replay from {replayDate} for mailbox {mailbox.Id}"` at sync start in `src/Rentier.Infrastructure/Sync/ImapMailboxSyncService.cs`

**Checkpoint**: US2 complete — replay-from-date fetches correct email range and cursor never regresses.

---

## Phase 5: User Story 3 — Duplicate Handling Strategy Selection (Priority: P2)

**Goal**: Replay modes apply the selected `DuplicateStrategy` per report: skip, create revision linked to original, or reprocess in place (with automatic fallback to revision when filings are advanced). Each decision is logged as a `DuplicateHandled` progress entry.

**Independent Test**: Run normal sync, then replay with each strategy; verify: SkipExisting leaves DB unchanged (idempotent), CreateNewRevision adds new Report with OriginalReportId set, ReprocessInPlace updates report and re-queues for processing; verify unsafe ReprocessInPlace falls back to revision with warning.

### Tests for User Story 3 ⚠️

> **Write these tests FIRST — ensure they FAIL before implementation**

- [ ] T035 [P] [US3] Extend `ReportTests` — `CreateRevision`: `OriginalReportId` equals original's Id; `ReportName` contains `_rev` suffix; `Status` is `Init`; `ImporterId` matches original; new `Id` is different from original in `tests/Rentier.Domain.Tests/ReportTests.cs`
- [ ] T036 [P] [US3] Extend `ImapMailboxSyncServiceTests` — SkipExisting: duplicate report is not persisted, `ReportsSkipped` count incremented, `DuplicateHandled` log entry emitted; CreateNewRevision: `Report.CreateRevision()` called, new report saved with `OriginalReportId` set, `RevisionsCreated` incremented; ReprocessInPlace (safe): filings deleted, report re-queued, `ReportsReprocessed` incremented; ReprocessInPlace (unsafe — filing has Status != Init): falls back to CreateNewRevision, warning log entry emitted in `tests/Rentier.Infrastructure.Tests/ImapMailboxSyncServiceTests.cs`

### Implementation for User Story 3

- [ ] T037 [US3] Add duplicate strategy dispatch to `ImapMailboxSyncService` — wrap existing duplicate check in replay-mode branch (`parameters.Mode != Incremental`): **SkipExisting** → log `$"Report {name} — skipped (already exists)"` with severity `DuplicateHandled`, increment skip counter; **CreateNewRevision** → call `Report.CreateRevision(existing, content)`, save via repository, log with severity `DuplicateHandled`, increment revision counter; **ReprocessInPlace** → call `HasAdvancedFilingsAsync(existing.Id)`: if true fall back to CreateNewRevision path + log warning, if false delete filings via `DeleteByReportIdAsync`, reset report status to Init, log `$"Report {name} — reprocessed in place"` with severity `DuplicateHandled`, increment reprocess counter in `src/Rentier.Infrastructure/Sync/ImapMailboxSyncService.cs`
- [ ] T038 [P] [US3] Extend `SyncMailboxCommandHandlerTests` — verify `SyncResult.ReportsSkipped`, `RevisionsCreated`, `ReportsReprocessed` are correctly aggregated when service returns non-zero counts in `tests/Rentier.Application.Tests/SyncMailboxCommandHandlerTests.cs`
- [ ] T039 [P] [US3] Extend `SyncAllCommandHandlerTests` — verify `SyncAllResult` aggregates skip/revision/reprocess totals across multiple mailboxes in `tests/Rentier.Application.Tests/SyncAllCommandHandlerTests.cs`

**Checkpoint**: US3 complete — all three duplicate strategies work correctly with proper fallback and logging.

---

## Phase 6: User Story 5 — Sync Mode Selection UI (Priority: P2)

**Goal**: The sync screen shows a mode selector, conditional date picker, conditional strategy selector, and a read-only impact preview panel. Mode defaults to Incremental. Changing mode updates the UI reactively. Invalid configurations show validation errors. Sync command builds and passes `SyncParameters` from current VM state.

**Independent Test**: Navigate to sync screen; verify mode ComboBox defaults to Incremental; select "Replay from Date" — date picker and strategy selector appear; select "Full Replay" — scope selector and warning appear; enter a future date — validation error shows; start sync — progress log shows active mode.

### Tests for User Story 5 ⚠️

> **Write these tests FIRST — ensure they FAIL before implementation**

- [ ] T040 [P] [US5] Extend `SyncViewModelTests` — `SelectedSyncMode` defaults to `Incremental`; `IsReplayFromDateMode` is true only when mode is `ReplayFromDate`; `IsReplayMode` is true for `ReplayFromDate` and `FullReplay`, false for `Incremental`; `IsFullReplayMode` is true only for `FullReplay`; `ImpactSummary` updates reactively when mode, date, or strategy changes; `ReplayFromDate` set to tomorrow yields non-null `ValidationError`; `ReplayFromDate` null with `ReplayFromDate` mode yields non-null `ValidationError`; valid configuration yields null `ValidationError`; `SyncParameters` built from VM has correct Mode, Strategy, ReplayFromDate, ScopeImporterId in `tests/Rentier.Desktop.Tests/SyncViewModelTests.cs`

### Implementation for User Story 5

- [ ] T041 [P] [US5] Create `SyncModeDisplayConverter` as `FuncValueConverter<SyncMode, string>` static instance returning localized display strings (`Strings.Sync_Mode_Incremental`, `Sync_Mode_ReplayFromDate`, `Sync_Mode_FullReplay`) in `src/Rentier.Desktop/Converters/SyncModeDisplayConverter.cs`
- [ ] T042 [P] [US5] Create `DuplicateStrategyDisplayConverter` as `FuncValueConverter<DuplicateStrategy, string>` static instance returning localized display strings (`Strings.Sync_Strategy_SkipExisting`, `Sync_Strategy_CreateNewRevision`, `Sync_Strategy_ReprocessInPlace`) in `src/Rentier.Desktop/Converters/DuplicateStrategyDisplayConverter.cs`
- [ ] T043 [P] [US5] Add localized resource strings to `Strings.resx`: mode display names (`Sync_Mode_Incremental`, `Sync_Mode_ReplayFromDate`, `Sync_Mode_FullReplay`); strategy display names (`Sync_Strategy_SkipExisting`, `Sync_Strategy_CreateNewRevision`, `Sync_Strategy_ReprocessInPlace`); watermarks (`Sync_ReplayDate_Watermark`); impact template strings; validation messages (`Sync_Validation_DateRequired`, `Sync_Validation_DateNotFuture`); full-replay warning text in `src/Rentier.Desktop/Resources/Strings.resx`
- [ ] T044 [US5] Update `SyncViewModel` — add `SyncMode[] AvailableSyncModes { get; }` and `DuplicateStrategy[] AvailableDuplicateStrategies { get; }` (via `Enum.GetValues<T>()`); add `SelectedSyncMode` (default `Incremental`), `ReplayFromDate`/`ReplayFromDateOffset` (DateTimeOffset? proxy following existing MailboxSettings pattern), `SelectedDuplicateStrategy` (default `SkipExisting`), `TodayOffset`; add `[ObservableAsProperty]` computed properties `IsReplayFromDateMode`, `IsReplayMode`, `IsFullReplayMode`, `ImpactSummary` driven by `WhenAnyValue` reactive chains; add `ValidationError` string property; add `BuildImpactSummary(SyncMode, DateOnly?, DuplicateStrategy)` helper in `src/Rentier.Desktop/ViewModels/SyncViewModel.cs`
- [ ] T045 [US5] Update `SyncCommand` in `SyncViewModel` to build `SyncParameters` from current VM state before dispatching `SyncAllCommand`; add `canExecute` observable that returns false when `ValidationError` is non-null in `src/Rentier.Desktop/ViewModels/SyncViewModel.cs`
- [ ] T046 [US5] Update `SyncView.axaml` — insert configuration panel above the progress log: (1) `ComboBox` bound to `AvailableSyncModes`/`SelectedSyncMode` with `SyncModeDisplayConverter` DataTemplate; (2) `CalendarDatePicker` bound to `ReplayFromDateOffset` with `IsVisible="{Binding IsReplayFromDateMode}"` and `DisplayDateEnd="{Binding TodayOffset}"`; (3) `ComboBox` bound to `AvailableDuplicateStrategies`/`SelectedDuplicateStrategy` with `DuplicateStrategyDisplayConverter` DataTemplate and `IsVisible="{Binding IsReplayMode}"`; (4) read-only impact preview `TextBlock` bound to `ImpactSummary`; (5) validation error `TextBlock` bound to `ValidationError` (collapsed when null/empty) in `src/Rentier.Desktop/Views/SyncView.axaml`

**Checkpoint**: US5 complete — sync screen shows full mode/strategy/impact UI; reactive state is correct; SyncParameters flows from UI to command.

---

## Phase 7: User Story 4 — Full Replay for Selected Importer or Mailbox (Priority: P3)

**Goal**: User can select "Full Replay" scoped to a specific importer or the entire mailbox. System issues no date filter IMAP query, applies the chosen duplicate strategy, shows a confirmation dialog before execution, and advances the cursor to the latest processed message.

**Independent Test**: Select "Full Replay" for a single importer; confirm only that importer's emails are reprocessed; confirm other importers on the mailbox are unaffected; confirm cursor advances after completion.

### Tests for User Story 4 ⚠️

> **Write these tests FIRST — ensure they FAIL before implementation**

- [ ] T047 [P] [US4] Extend `SyncParametersTests` — `GetEffectiveStartDate` with `FullReplay` mode returns `null`; `ScopeImporterId` set does not affect `GetEffectiveStartDate`; valid construction of full-replay params with and without ScopeImporterId succeeds in `tests/Rentier.Domain.Tests/SyncParametersTests.cs`
- [ ] T048 [P] [US4] Extend `ImapMailboxSyncServiceTests` — full-replay mode builds IMAP query with no date filter (fetch all messages); per-importer scope: when `ScopeImporterId` is set in Parameters, only messages matching that importer's FROM/SUBJECT filters are processed; cursor advances to latest message after full-replay success in `tests/Rentier.Infrastructure.Tests/ImapMailboxSyncServiceTests.cs`
- [ ] T049 [P] [US4] Extend `SyncMailboxCommandHandlerTests` — when `Parameters.ScopeImporterId` is set, importers list is filtered to only that importer; other importers on same mailbox receive no sync call in `tests/Rentier.Application.Tests/SyncMailboxCommandHandlerTests.cs`

### Implementation for User Story 4

- [ ] T050 [US4] Extend `ImapMailboxSyncService` with `FullReplay` query branch — when `parameters.Mode == FullReplay`: use `SearchQuery.All` (no date filter) as IMAP query; log `$"[FullReplay] Starting full replay for mailbox {mailbox.Id}, scope: {parameters.ScopeImporterId?.ToString() ?? "all importers"}"` at sync start in `src/Rentier.Infrastructure/Sync/ImapMailboxSyncService.cs`
- [ ] T051 [US4] Confirm `SyncMailboxCommandHandler` importer scope filtering — when `Parameters.ScopeImporterId` is non-null, reduce importers passed to `IMailboxSyncService.SyncAsync` to the single matching importer; if the importer is not found on the mailbox, return a domain error rather than silently doing nothing in `src/Rentier.Application/Handlers/SyncMailboxCommandHandler.cs`
- [ ] T052 [US4] Update `SyncViewModel` — add `IReadOnlyList<ImporterDto> AvailableImporters` (populated via GetImportersQuery on mailbox selection or page load); add `ImporterDto? SelectedScope` (null = entire mailbox); add `IsFullReplayMode`-gated reactive chain populating scope options; set `SyncParameters.ScopeImporterId` from `SelectedScope?.Id` when building parameters in `src/Rentier.Desktop/ViewModels/SyncViewModel.cs`
- [ ] T053 [US4] Update `SyncView.axaml` — add scope selector `ComboBox` bound to `AvailableImporters`/`SelectedScope` with `IsVisible="{Binding IsFullReplayMode}"`; add full-replay warning `Border`/`TextBlock` with `IsVisible="{Binding IsFullReplayMode}"` displaying `Strings.Sync_FullReplay_Warning` in `src/Rentier.Desktop/Views/SyncView.axaml`
- [ ] T054 [US4] Add full-replay confirmation dialog to `SyncViewModel` — before executing sync when `IsFullReplayMode`: show dialog displaying mailbox name, scope (specific importer or "all importers"), selected duplicate strategy, and warning text; only proceed if user confirms; use the existing dialog pattern from the codebase in `src/Rentier.Desktop/ViewModels/SyncViewModel.cs`

**Checkpoint**: US4 complete — full replay works for both entire mailbox and per-importer scope, with confirmation dialog and correct cursor behavior.

---

## Phase 8: User Story 6 — Remove Start Date from Mailbox Settings (Priority: P3)

**Goal**: The mailbox settings form no longer shows a "Start Date" / "Initial Sync Date" field. Existing mailboxes continue to function via the migration (un-synced mailboxes have their InitialSyncDate preserved as cursor). New mailboxes get a 90-day default cursor.

**Independent Test**: Open mailbox settings (add and edit); confirm no date field is visible; add a new mailbox without a start date; run sync; confirm it starts from ~90 days ago; open an existing mailbox that was un-synced — verify it still functions.

### Tests for User Story 6 ⚠️

> **Write these tests FIRST — ensure they FAIL before implementation**

- [ ] T055 [P] [US6] Extend `MailboxSettingsViewModelTests` — no `InitialSyncDate` or `InitialSyncDateOffset` property present on VM; `AddMailboxCommand` constructed without InitialSyncDate; `UpdateMailboxCommand` constructed without InitialSyncDate; form validates correctly without a date field in `tests/Rentier.Desktop.Tests/MailboxSettingsViewModelTests.cs`
- [ ] T056 [P] [US6] Extend `MailboxTests` — `Create()` without InitialSyncDate: `Cursor.LastSyncDate` is approximately `DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-90)`; `UpdateDetails()` signature does not include InitialSyncDate; cursor is unchanged after `UpdateDetails()` in `tests/Rentier.Domain.Tests/MailboxTests.cs`

### Implementation for User Story 6

- [ ] T057 [US6] Update `MailboxSettingsViewModel` — remove `InitialSyncDate` property, `InitialSyncDateOffset` binding proxy, and all InitialSyncDate validation logic; update `AddMailboxCommand` construction to omit InitialSyncDate; update `UpdateMailboxCommand` construction to omit InitialSyncDate in `src/Rentier.Desktop/ViewModels/MailboxSettingsViewModel.cs`
- [ ] T058 [US6] Update `MailboxSettingsView.axaml` — remove the `CalendarDatePicker` for InitialSyncDate, its `Label`, any `TextBlock` validation display, and the associated `Grid.Row` / layout spacer so the form re-flows cleanly in `src/Rentier.Desktop/Views/MailboxSettingsView.axaml`

**Checkpoint**: US6 complete — mailbox settings form is simplified; existing data is preserved via migration; new mailboxes use the 90-day default.

---

## Phase 9: Polish & Cross-Cutting Concerns

**Purpose**: Final validation, log format consistency, coverage gate confirmation, and architecture compliance check.

- [ ] T059 [P] Run quickstart.md validation scenarios — `dotnet build` (zero errors/warnings); `dotnet ef database update` applies migration cleanly; run incremental, replay-from-date, full-replay, and duplicate-strategy scenarios per quickstart.md; confirm all sync modes produce expected log output
- [ ] T060 [P] Verify Domain and Application coverage gates — `dotnet test --filter "FullyQualifiedName~Rentier.Domain.Tests"` shows 100% pass; `dotnet test --filter "FullyQualifiedName~Rentier.Application.Tests"` shows ≥90% pass; all new SyncParameters, cursor, and strategy tests are green
- [ ] T061 Audit all `CursorTransition` and `DuplicateHandled` log entries in `ImapMailboxSyncService` — confirm `CursorTransition` entries follow the format `[CursorTransition] Mailbox={id} Before=({date},{uid}) After=({date},{uid})`; confirm `DuplicateHandled` entries follow `Report {name} — {outcome}` pattern consistent with the duplicate handling contract in `src/Rentier.Infrastructure/Sync/ImapMailboxSyncService.cs`
- [ ] T062 [P] Verify Clean Architecture boundary compliance — no Domain→Application references; no Application→Infrastructure references; no Infrastructure→Desktop references; no Domain→Infrastructure references; confirm SyncParameters is imported only from Domain layer in application/infrastructure/desktop code; run `dotnet build` as final confirmation

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Phase 1 — **BLOCKS all user stories**
- **US1 (Phase 3 — P1)**: Depends on Phase 2 — blocks all downstream stories
- **US2 (Phase 4 — P2)**: Depends on Phase 2 (Domain/Application) + Phase 3 (ImapMailboxSyncService baseline)
- **US3 (Phase 5 — P2)**: Depends on Phase 2 + Phase 3 (duplicate check infrastructure)
- **US5 (Phase 6 — P2)**: Depends on Phase 3 (SyncParameters wired end-to-end); benefits from US2+US3 complete for full UI
- **US4 (Phase 7 — P3)**: Depends on Phase 3 + Phase 5 (duplicate strategy dispatch already in place)
- **US6 (Phase 8 — P3)**: Depends on Phase 2 (Mailbox entity + migration); largely independent of US2–US5
- **Polish (Phase 9)**: Depends on all desired user stories being complete

### User Story Dependencies

- **US1 (P1)**: Start after Phase 2 — no dependency on other stories
- **US2 (P2)**: Start after Phase 2 + US1 complete (needs ImapMailboxSyncService baseline); US2 and US3 can proceed in parallel with each other after US1
- **US3 (P2)**: Start after Phase 2 + US1 complete; parallel with US2
- **US5 (P2)**: Start after US1 (needs SyncParameters to flow through to SyncAllCommand); can start UI work in parallel with US2/US3 once US1 is done
- **US4 (P3)**: Start after US2 + US3 (needs both query branch and strategy dispatch); US6 independent
- **US6 (P3)**: Start after Phase 2 (only needs Mailbox entity + migration); can proceed in parallel with US2/US3/US5

### Within Each User Story

1. Write tests FIRST — confirm they FAIL (red)
2. Models / Domain types before services
3. Interfaces before implementations
4. Core implementation before integration (UI)
5. Confirm tests pass (green) before moving to next story

---

## Parallel Execution Examples

### Phase 2 — Parallel Foundation Tasks

```
# Run simultaneously (different files, no cross-dependencies):
Task T002: Create SyncMode enum
Task T003: Create DuplicateStrategy enum
Task T007: Remove InitialSyncDate from AddMailboxCommand
Task T008: Remove InitialSyncDate from UpdateMailboxCommand
Task T012: Add methods to IReportRepository
Task T013: Add HasAdvancedFilingsAsync to IFilingRepository
Task T014: Extend SyncResult DTO
Task T015: Extend SyncAllResult DTO
Task T016: Add CursorTransition/DuplicateHandled to SyncProgressSeverity
Task T017: Update MailboxConfiguration
Task T018: Update ReportConfiguration

# After T002+T003 complete:
Task T004: Create SyncParameters (depends on SyncMode + DuplicateStrategy)
Task T005: Modify Mailbox entity
Task T006: Modify Report entity
```

### Phase 3 (US1) — Parallel Tasks

```
# Run simultaneously (all different files):
Task T020: SyncParametersTests (new file)
Task T021: Extend MailboxTests
Task T022: Extend SyncMailboxCommandHandlerTests
Task T023: Update AddMailboxCommandHandler
Task T024: Update UpdateMailboxCommandHandler
Task T027: Implement ReportRepository new methods
Task T028: Implement FilingRepository.HasAdvancedFilingsAsync

# After T023+T024+T027+T028 complete:
Task T025: Update SyncMailboxCommandHandler (depends on repo implementations)
Task T026: Update SyncAllCommandHandler (depends on T025)
Task T029: Update ImapMailboxSyncService (depends on T025 interface)
Task T030: ImapMailboxSyncServiceTests (parallels T029)
```

### Phase 5 (US3) + Phase 6 (US5) in Parallel

```
# US3 (infrastructure-level) and US5 (UI-level) can run in parallel:
Team A: T035 → T036 → T037 → T038 → T039   (US3 duplicate strategy)
Team B: T040 → T041 → T042 → T043 → T044 → T045 → T046  (US5 sync UI)
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001)
2. Complete Phase 2: Foundational (T002–T019) — CRITICAL, blocks everything
3. Complete Phase 3: US1 (T020–T030)
4. **STOP and VALIDATE**: Run `dotnet test`, confirm incremental sync end-to-end
5. Demo: existing sync behavior fully preserved with new parameter structure + cursor logging

### Incremental Delivery

1. Setup + Foundational → Foundation ready (T001–T019)
2. US1 → Incremental sync wired → validate → ✅ MVP
3. US2 + US3 (parallel) → Replay modes + duplicate handling → validate → ✅ Core replay
4. US5 → Sync mode UI → validate → ✅ Full user-facing replay feature
5. US4 → Full replay + scope → validate → ✅ Power-user replay
6. US6 → Remove start date field → validate → ✅ Simplified mailbox setup
7. Polish (T059–T062) → Final gate checks → ✅ Merge-ready

### Parallel Team Strategy

With two developers after Phase 2 completes:
- **Developer A**: US1 → US2 → US4 (infrastructure pipeline)
- **Developer B**: US1 (shared) → US3 → US5 → US6 (strategy dispatch + UI)

---

## Notes

- `[P]` tasks = different files, no dependencies on incomplete tasks in same phase
- `[Story]` label maps every task to a specific user story for traceability
- Tests marked `⚠️` MUST be written before implementation and must FAIL first
- Cursor MUST NOT advance on cancellation (`OperationCanceledException`) or any exception — this is verified in T030
- `ReprocessInPlace` safety check (T037) is the most critical correctness gate — a failed filing must never be silently overwritten
- `max()` monotonic cursor logic (T029) prevents the most dangerous regression scenario where a replay resets incremental sync position
- Run `dotnet ef database update --startup-project src/Rentier.Desktop` after T019 to verify migration applies cleanly
- The EF migration (T019) must be tested against a copy of a production-like DB to confirm the `Cursor_LastSyncDate` preservation SQL runs correctly before committing
