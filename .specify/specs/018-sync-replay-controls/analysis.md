# Analysis: Existing Sync Pipeline — 018 Sync Replay Controls

**Feature Branch**: `feature/018-sync-replay-controls`
**Date**: 2025-07-15

## Current Sync Architecture

### End-to-End Flow

```text
┌─────────────────────┐
│  SyncView (Desktop)  │  User clicks "Start Sync"
│  SyncViewModel       │  Creates CancellationTokenSource
└──────────┬──────────┘
           │ SyncCommand (ReactiveCommand.CreateFromTask)
           ▼
┌─────────────────────────────┐
│  SyncAllCommandHandler      │  Orchestrates two phases
│  (Application layer)        │
└──────────┬──────────────────┘
           │ Phase 1: Mailbox Sync
           ▼
┌─────────────────────────────┐
│  SyncMailboxCommandHandler  │  Groups importers by MailboxId
│  (Application layer)        │  Iterates mailboxes
└──────────┬──────────────────┘
           │ For each mailbox:
           ▼
┌─────────────────────────────┐
│  ImapMailboxSyncService     │  IMAP connection, query, download
│  (Infrastructure layer)     │  Cursor read → query → process → cursor update
└──────────┬──────────────────┘
           │ Phase 2: Report Processing
           ▼
┌─────────────────────────────┐
│  ProcessReportsCommandHandler│ Parse CSV → create Filings
│  (Application layer)        │
└─────────────────────────────┘
```

### Key Components

#### 1. SyncViewModel (`Rentier.Desktop`)
- **Location**: `src/Rentier.Desktop/ViewModels/SyncViewModel.cs`
- **Responsibilities**: UI state (IsRunning, LogEntries, ErrorMessage), progress subscription, cancel support
- **Commands**: `SyncCommand` (start), `CancelCommand` (cancel via CTS)
- **Pattern**: `ReactiveCommand.CreateFromTask` with `outputScheduler: _scheduler`

**Current limitations for feature 018**:
- No mode selection — always runs incremental
- No duplicate strategy selection
- No impact preview
- No date input

#### 2. SyncAllCommandHandler (`Rentier.Application`)
- **Location**: `src/Rentier.Application/Handlers/SyncAllCommandHandler.cs`
- **Input**: `SyncAllCommand` (currently takes only `IProgress<SyncProgress>?`)
- **Two-phase**: Phase 1 = `SyncMailboxCommandHandler`, Phase 2 = `ProcessReportsCommandHandler`
- **Returns**: `Result<SyncAllResult, Error>`

**Impact for feature 018**:
- Must accept `SyncParameters` to pass mode/strategy through to the sync service
- Phase 2 (report processing) needs duplicate strategy awareness

#### 3. SyncMailboxCommandHandler (`Rentier.Application`)
- **Location**: `src/Rentier.Application/Handlers/SyncMailboxCommandHandler.cs`
- **Input**: `SyncMailboxCommand` (currently takes only `IProgress<SyncProgress>?`)
- **Logic**: Gets all importers, groups by MailboxId, calls `IMailboxSyncService.SyncAsync`

**Impact for feature 018**:
- Must pass `SyncParameters` to `IMailboxSyncService.SyncAsync`
- For per-importer Full Replay, must filter importers by scope

#### 4. ImapMailboxSyncService (`Rentier.Infrastructure`)
- **Location**: `src/Rentier.Infrastructure/Sync/ImapMailboxSyncService.cs`
- **Core flow**:
  1. Connect to IMAP server with credentials from OS credential store
  2. Build IMAP search query from cursor position
  3. For each importer: apply FROM/SUBJECT filters, match attachments
  4. Check duplicates via `ExistsByImporterAndNameAsync` (skip if exists)
  5. Create Report entities for new attachments
  6. Update cursor to latest position

**Impact for feature 018** (MOST AFFECTED COMPONENT):
- Query construction must branch on `SyncMode`:
  - Incremental: current behavior (from cursor)
  - ReplayFromDate: `DeliveredAfter(replayDate)` regardless of cursor
  - FullReplay: no date filter (fetch all)
- Duplicate handling must branch on `DuplicateStrategy` instead of always skipping
- Cursor update must use `max()` logic to prevent regression

#### 5. Mailbox Entity (`Rentier.Domain`)
- **Location**: `src/Rentier.Domain/Entities/Mailbox.cs`
- **Fields**: Id, Host, Port, Username, InitialSyncDate, Cursor (owned VO)
- **Factory**: `Mailbox.Create(host, port, username, initialSyncDate)` seeds cursor from InitialSyncDate
- **Cursor update**: `UpdateCursor(MailboxCursor)` — simple property setter

**Impact for feature 018**:
- Remove `InitialSyncDate` property
- Update `Create()` factory — new mailboxes get default cursor (90 days ago)
- Update `UpdateDetails()` — no longer accepts initialSyncDate parameter

#### 6. MailboxCursor Value Object (`Rentier.Domain`)
- **Location**: `src/Rentier.Domain/ValueObjects/MailboxCursor.cs`
- **Definition**: `record MailboxCursor(DateOnly? LastSyncDate, long? LastUid)`
- **Semantics**: null LastSyncDate = never synced; null LastUid = first sync (date-based query)

**Impact for feature 018**:
- No structural changes needed — cursor remains as-is
- Override is external via `SyncParameters`, not internal to cursor

#### 7. Report Entity (`Rentier.Domain`)
- **Location**: `src/Rentier.Domain/Entities/Report.cs`
- **Fields**: Id, ImportDate, ImporterId, Status, ReportName, AttachmentContent, MailboxMessageId
- **Duplicate key**: Unique index on (ImporterId, ReportName)

**Impact for feature 018**:
- Add optional `OriginalReportId` (Guid?) for revision tracking
- Status machine unchanged

---

## Modification Points Summary

| Component | Layer | Change Type | Scope |
|-----------|-------|-------------|-------|
| `SyncMode` enum | Domain | **New** | 3 values |
| `DuplicateStrategy` enum | Domain | **New** | 3 values |
| `SyncParameters` record | Domain | **New** | Mode + Strategy + optional date/scope |
| `Mailbox` entity | Domain | **Modify** | Remove InitialSyncDate, update factory |
| `Report` entity | Domain | **Modify** | Add OriginalReportId for revisions |
| `SyncMailboxCommand` | Application | **Modify** | Add SyncParameters parameter |
| `SyncAllCommand` | Application | **Modify** | Add SyncParameters parameter |
| `SyncMailboxCommandHandler` | Application | **Modify** | Pass SyncParameters, filter importers for scope |
| `SyncAllCommandHandler` | Application | **Modify** | Pass SyncParameters through |
| `IMailboxSyncService` | Application | **Modify** | Add SyncParameters to SyncAsync signature |
| `IReportRepository` | Application | **Modify** | Add GetByImporterAndMessageIdAsync for revision lookup |
| `ImapMailboxSyncService` | Infrastructure | **Modify** | Branch query/duplicate logic by mode/strategy |
| `MailboxConfiguration` (EF) | Infrastructure | **Modify** | Remove InitialSyncDate column mapping |
| `ReportConfiguration` (EF) | Infrastructure | **Modify** | Add OriginalReportId FK mapping |
| `Migration 0010` | Infrastructure | **New** | Drop InitialSyncDate, add OriginalReportId |
| `SyncViewModel` | Desktop | **Modify** | Add mode/strategy/date selection, impact preview |
| `SyncView.axaml` | Desktop | **Modify** | Add mode selector, date picker, strategy selector, impact panel |
| `SyncModeDisplayConverter` | Desktop | **New** | Localized display for SyncMode enum |
| `DuplicateStrategyDisplayConverter` | Desktop | **New** | Localized display for DuplicateStrategy enum |
| `MailboxSettingsViewModel` | Desktop | **Modify** | Remove InitialSyncDate property and binding |
| `MailboxSettingsView.axaml` | Desktop | **Modify** | Remove InitialSyncDate DatePicker |

---

## Risk Assessment

| Risk | Impact | Mitigation |
|------|--------|------------|
| Cursor regression during replay | HIGH — data could be re-synced on every incremental sync | `max()` logic on cursor update; log before/after values |
| InitialSyncDate removal breaks un-synced mailboxes | MEDIUM — first sync could miss emails | Migration preserves value in cursor; tested with integration test |
| "Reprocess in Place" deletes exported filings | HIGH — data loss | Safety check: fall back to Create New Revision if filing status ≠ Init |
| IMAP query without date filter (Full Replay) fetches entire mailbox | MEDIUM — performance | Confirmation dialog with warning; progress reporting shows count |
| Concurrent UI state changes during mode selection | LOW — cosmetic | Reactive subscriptions handle visibility toggling atomically |

---

## Dependency Graph

```text
Phase 1 (Domain):
  SyncMode enum ──┐
  DuplicateStrategy enum ──┤
  SyncParameters record ───┤
  Mailbox.RemoveInitialSyncDate ──┤
  Report.AddOriginalReportId ─────┘
                                   │
Phase 2 (Application):             ▼
  SyncMailboxCommand.AddParams ──┐
  SyncAllCommand.AddParams ──────┤
  IMailboxSyncService.UpdateSig ─┤
  SyncMailboxCommandHandler ─────┤
  SyncAllCommandHandler ─────────┘
                                   │
Phase 3 (Infrastructure):         ▼
  Migration 0010 ──┐
  ImapMailboxSyncService ──┤
  EF Configurations ───────┘
                                   │
Phase 4 (Desktop):                ▼
  SyncViewModel.AddModeSelection ──┐
  SyncView.axaml.AddControls ──────┤
  Converters ──────────────────────┤
  MailboxSettings.RemoveDateField ─┘
```
