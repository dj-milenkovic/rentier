# Application Contracts: 018 Sync Replay Controls

**Feature Branch**: `feature/018-sync-replay-controls`
**Date**: 2025-07-15

## Modified Commands

### SyncMailboxCommand

**File**: `src/Rentier.Application/Commands/SyncMailboxCommand.cs`

```csharp
// BEFORE:
public sealed record SyncMailboxCommand(IProgress<SyncProgress>? Progress = null);

// AFTER:
public sealed record SyncMailboxCommand(
    SyncParameters Parameters,
    IProgress<SyncProgress>? Progress = null
);
```

**Handler Changes** (`SyncMailboxCommandHandler`):
- Passes `Parameters` to `IMailboxSyncService.SyncAsync`
- When `Parameters.ScopeImporterId` is set (Full Replay for specific importer):
  - Filters importers to only the specified one
  - Other importers on the same mailbox are NOT synced

---

### SyncAllCommand

**File**: `src/Rentier.Application/Commands/SyncAllCommand.cs`

```csharp
// BEFORE:
public sealed record SyncAllCommand(IProgress<SyncProgress>? Progress = null);

// AFTER:
public sealed record SyncAllCommand(
    SyncParameters Parameters,
    IProgress<SyncProgress>? Progress = null
);
```

**Handler Changes** (`SyncAllCommandHandler`):
- Phase 1: Passes `Parameters` to `SyncMailboxCommandHandler`
- Phase 2: Passes `Parameters.Strategy` to `ProcessReportsCommandHandler` (for duplicate-aware filing creation)

---

### AddMailboxCommand

**File**: `src/Rentier.Application/Commands/AddMailboxCommand.cs`

```csharp
// BEFORE:
public sealed record AddMailboxCommand(
    string Host, int Port, string Username, string Password, DateOnly InitialSyncDate
);

// AFTER:
public sealed record AddMailboxCommand(
    string Host, int Port, string Username, string Password
);
```

**Handler Changes**: Calls `Mailbox.Create(host, port, username)` without InitialSyncDate.

---

### UpdateMailboxCommand

**File**: `src/Rentier.Application/Commands/UpdateMailboxCommand.cs`

```csharp
// BEFORE:
public sealed record UpdateMailboxCommand(
    Guid Id, string Host, int Port, string Username, string? Password, DateOnly InitialSyncDate
);

// AFTER:
public sealed record UpdateMailboxCommand(
    Guid Id, string Host, int Port, string Username, string? Password
);
```

**Handler Changes**: Calls `mailbox.UpdateDetails(host, port, username)` without InitialSyncDate.

---

## Modified Interfaces

### IMailboxSyncService

**File**: `src/Rentier.Application/Interfaces/IMailboxSyncService.cs`

```csharp
// BEFORE:
Task<Result<SyncResult, Error>> SyncAsync(
    Mailbox mailbox,
    IReadOnlyList<Importer> importers,
    IProgress<SyncProgress>? progress,
    CancellationToken ct);

// AFTER:
Task<Result<SyncResult, Error>> SyncAsync(
    Mailbox mailbox,
    IReadOnlyList<Importer> importers,
    SyncParameters parameters,
    IProgress<SyncProgress>? progress,
    CancellationToken ct);
```

**Implementation Contract** (`ImapMailboxSyncService`):
1. **Query construction** uses `parameters.GetEffectiveStartDate(mailbox.Cursor)`:
   - If result is non-null and `LastUid` is null → `DeliveredAfter(date)`
   - If result is non-null and `LastUid` is non-null AND mode is Incremental → `Uid > LastUid`
   - If result is non-null AND mode is NOT Incremental → `DeliveredAfter(date)` (ignore UID for replay)
   - If result is null (FullReplay) → no date filter
2. **Duplicate handling** based on `parameters.Strategy` (only when `Mode != Incremental`):
   - SkipExisting → current behavior (skip, log)
   - CreateNewRevision → create Report via `Report.CreateRevision(original, content)`
   - ReprocessInPlace → check filing safety, then delete filings + update report
3. **Cursor update** uses `max(current, new)` to prevent regression
4. **Logging**: Every cursor transition logged with `[CursorTransition] Mailbox={id} Before={old} After={new}`

---

## Modified Repository Interfaces

### IReportRepository — New Methods

```csharp
// ADD to existing interface:

/// <summary>
/// Finds an existing report by importer and IMAP message UID.
/// Used during replay to find the original report for duplicate handling.
/// </summary>
Task<Report?> GetByImporterAndMessageIdAsync(
    Guid importerId, long mailboxMessageId, CancellationToken ct = default);

/// <summary>
/// Finds an existing report by importer and report name.
/// Used during replay when UID is not available.
/// </summary>
Task<Report?> GetByImporterAndNameAsync(
    Guid importerId, string reportName, CancellationToken ct = default);

/// <summary>
/// Gets all revisions of a report (reports where OriginalReportId = reportId).
/// </summary>
Task<IReadOnlyList<Report>> GetRevisionsAsync(
    Guid originalReportId, CancellationToken ct = default);
```

### IFilingRepository — New Methods

```csharp
// ADD to existing interface:

/// <summary>
/// Checks if any filing linked to the given report has been advanced past Init status.
/// Used by ReprocessInPlace safety check.
/// </summary>
Task<bool> HasAdvancedFilingsAsync(Guid reportId, CancellationToken ct = default);
```

---

## New DTOs

### SyncResult — Extended

**File**: `src/Rentier.Application/DTOs/SyncResult.cs`

```csharp
// BEFORE:
public sealed record SyncResult(int ReportsCreated, IReadOnlyList<string> Errors);

// AFTER:
public sealed record SyncResult(
    int ReportsCreated,
    int ReportsSkipped,          // NEW: count of duplicates skipped
    int RevisionsCreated,        // NEW: count of revisions created
    int ReportsReprocessed,      // NEW: count of reports reprocessed in place
    IReadOnlyList<string> Errors
);
```

### SyncAllResult — Extended

```csharp
// BEFORE:
public sealed record SyncAllResult(
    int MailboxesSynced, int AttachmentsDownloaded,
    int ReportsProcessed, int FilingsCreated);

// AFTER:
public sealed record SyncAllResult(
    int MailboxesSynced, int AttachmentsDownloaded,
    int ReportsProcessed, int FilingsCreated,
    int ReportsSkipped,          // NEW
    int RevisionsCreated,        // NEW
    int ReportsReprocessed       // NEW
);
```

### SyncProgressEntry — Extended

```csharp
// Add new entry types for duplicate handling:
public enum SyncProgressSeverity
{
    Info,
    Warning,
    Error,
    CursorTransition,    // NEW: cursor state change
    DuplicateHandled     // NEW: duplicate detection result
}
```

---

## Cursor Transition Contract

### Rules

1. **Cursor MUST advance monotonically**: `new.LastSyncDate ≥ old.LastSyncDate` AND `new.LastUid ≥ old.LastUid`
2. **Cursor MUST NOT update on failure**: If sync throws or is cancelled, cursor remains unchanged
3. **Cursor MUST NOT update on partial success**: If any message processing fails, cursor is not advanced (user must retry)
4. **Cursor transition MUST be logged**: Every `UpdateCursor` call emits a `SyncProgressEntry` with severity `CursorTransition` containing before and after values

### Transition Matrix

| Scenario | Cursor Before | Cursor After | Notes |
|----------|--------------|--------------|-------|
| Incremental (success) | (2024-06-01, 500) | (2024-07-15, 650) | Advanced to latest |
| Incremental (failure) | (2024-06-01, 500) | (2024-06-01, 500) | Unchanged |
| ReplayFromDate (success) | (2024-06-01, 500) | (2024-07-15, 650) | Advanced past replay date |
| ReplayFromDate (failure) | (2024-06-01, 500) | (2024-06-01, 500) | Unchanged |
| FullReplay (success) | (2024-06-01, 500) | (2024-07-15, 650) | Advanced to latest |
| FullReplay (failure) | (2024-06-01, 500) | (2024-06-01, 500) | Unchanged |
| Replay with older latest msg | (2024-06-01, 500) | (2024-06-01, 500) | max() prevents regression |

### Cursor Update Implementation

```csharp
// In ImapMailboxSyncService, after successful processing:
var newDate = DateOnly.FromDateTime(DateTime.UtcNow);
var newUid = messagesProcessed.Max(m => m.Uid);

var safeCursor = new MailboxCursor(
    LastSyncDate: MaxDate(mailbox.Cursor.LastSyncDate, newDate),
    LastUid: MaxUid(mailbox.Cursor.LastUid, newUid)
);

progress?.Report(new SyncProgress(new SyncProgressEntry(
    $"Cursor transition: ({mailbox.Cursor.LastSyncDate}, {mailbox.Cursor.LastUid}) → ({safeCursor.LastSyncDate}, {safeCursor.LastUid})",
    SyncProgressSeverity.CursorTransition)));

mailbox.UpdateCursor(safeCursor);
await _mailboxRepository.UpdateAsync(mailbox, ct);
```

---

## Duplicate Handling Contract

### Per-Report Decision Flow

```text
Is this a replay mode? (Mode != Incremental)
  │
  ├─ No → Process normally (cursor prevents duplicates)
  │
  └─ Yes → Check: does report already exist?
       │
       ├─ No → Create new report (same as incremental)
       │
       └─ Yes → Apply DuplicateStrategy:
            │
            ├─ SkipExisting:
            │   Log "Report {name} — skipped (already exists)"
            │   Increment ReportsSkipped counter
            │
            ├─ CreateNewRevision:
            │   Create Report.CreateRevision(original, content)
            │   Log "Report {name} — revision created (linked to {originalId})"
            │   Increment RevisionsCreated counter
            │
            └─ ReprocessInPlace:
                 Check: HasAdvancedFilingsAsync(existingReport.Id)?
                 │
                 ├─ Yes (unsafe):
                 │   Fall back to CreateNewRevision
                 │   Log "Report {name} — reprocess blocked (filings exported), creating revision instead"
                 │   Increment RevisionsCreated counter
                 │
                 └─ No (safe):
                      Delete filings via DeleteByReportIdAsync
                      Update report content
                      Set report status to Init (for re-processing)
                      Log "Report {name} — reprocessed in place"
                      Increment ReportsReprocessed counter
```

---

## ViewModel Contract (SyncViewModel)

### New Properties

```csharp
// Mode selection
public SyncMode[] AvailableSyncModes { get; }
public SyncMode SelectedSyncMode { get; set; }    // Default: Incremental

// Date picker (visible only in ReplayFromDate mode)
public DateOnly? ReplayFromDate { get; set; }
public DateTimeOffset? ReplayFromDateOffset { get; set; }  // Binding proxy
public bool IsReplayFromDateMode { [ObservableAsProperty] get; }

// Strategy selection (visible only in non-Incremental modes)
public DuplicateStrategy[] AvailableDuplicateStrategies { get; }
public DuplicateStrategy SelectedDuplicateStrategy { get; set; }  // Default: SkipExisting
public bool IsReplayMode { [ObservableAsProperty] get; }

// Scope selection (visible only in FullReplay mode)
public bool IsFullReplayMode { [ObservableAsProperty] get; }
// Scope options populated from importers

// Impact preview (read-only, computed)
public string ImpactSummary { [ObservableAsProperty] get; }

// Validation
public string? ValidationError { get; }
```

### Reactive Chains

```csharp
// Visibility toggles
this.WhenAnyValue(x => x.SelectedSyncMode)
    .Select(m => m == SyncMode.ReplayFromDate)
    .ToPropertyEx(this, x => x.IsReplayFromDateMode);

this.WhenAnyValue(x => x.SelectedSyncMode)
    .Select(m => m != SyncMode.Incremental)
    .ToPropertyEx(this, x => x.IsReplayMode);

this.WhenAnyValue(x => x.SelectedSyncMode)
    .Select(m => m == SyncMode.FullReplay)
    .ToPropertyEx(this, x => x.IsFullReplayMode);

// Impact summary
this.WhenAnyValue(
        x => x.SelectedSyncMode,
        x => x.ReplayFromDate,
        x => x.SelectedDuplicateStrategy)
    .Select(BuildImpactSummary)
    .ToPropertyEx(this, x => x.ImpactSummary);
```
