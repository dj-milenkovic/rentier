# Application Layer Contracts: One-Click Sync Workflow (015)

**Feature**: `015-one-click-sync-workflow`  
**Created**: 2025-07-14  
**Scope**: Public interfaces exposed by the Application layer consumed by Desktop

---

## Contract Overview

The Desktop layer (`SyncViewModel`) communicates with the Application layer through a single
dedicated handler interface. No direct repository access or infrastructure calls are permitted
from the Desktop.

```
SyncViewModel
  └── ISyncAllCommandHandler
        ├── internally delegates to:
        │   ├── ICommandHandler<SyncMailboxCommand, Result<SyncResult, Error>>
        │   └── ICommandHandler<ProcessReportsCommand, Result<ProcessReportsResult, Error>>
        └── reports progress via: IProgress<SyncProgressEntry>
```

**Key architectural decision**: `ISyncAllCommandHandler` is a dedicated interface — it does NOT
inherit from or implement `ICommandHandler<TCmd, TResult>`. The standard interface signature is
`HandleAsync(TCommand, CancellationToken)` and cannot carry the `IProgress<SyncProgressEntry>`
parameter required for real-time progress reporting.

---

## Command Contract

### `SyncAllCommand` → `SyncAllResult` (via `ISyncAllCommandHandler`)

**Interface**: `ISyncAllCommandHandler`  
**Handler**: `SyncAllCommandHandler`  
**Namespace**: `Rentier.Application.Handlers`

#### Input

```csharp
public sealed record SyncAllCommand();
// Parameterless — targets all configured mailboxes and importers
```

#### Handler signature

```csharp
public interface ISyncAllCommandHandler
{
    Task<Result<SyncAllResult, Error>> HandleAsync(
        SyncAllCommand command,
        IProgress<SyncProgressEntry> progress,
        CancellationToken ct = default);
}
```

| Parameter | Type | Contract |
|-----------|------|----------|
| `command` | `SyncAllCommand` | Parameterless marker record |
| `progress` | `IProgress<SyncProgressEntry>` | Must not be null; receives entries throughout execution |
| `ct` | `CancellationToken` | Checked between major steps; `OperationCanceledException` propagates |

#### Output (success)

```csharp
public sealed record SyncAllResult(
    int MailboxesSynced,           // number of mailbox groups processed
    int AttachmentsDownloaded,     // reports created from downloaded attachments
    int ReportsProcessed,          // Init-status reports successfully processed
    int FilingsCreated,            // tax filings created from processed reports
    IReadOnlyList<string> Errors); // aggregated non-fatal errors from both steps
```

**Invariants**:
- `Errors` is never null — empty list when no errors occurred
- All integer counts are ≥ 0
- Result is `Success` even when `Errors.Count > 0` (partial success)
- `Failure` only for truly unexpected/catastrophic errors

#### Output (failure)

```csharp
Result<SyncAllResult, Error>.Failure(new Error("INFRASTRUCTURE_ERROR", "..."))
// Only for unexpected exceptions that prevent any meaningful result
// Partial success (some steps fail, others succeed) returns Success with Errors populated
```

#### Behaviour

1. **Step 1 — Mailbox sync**:
   - Reports `"Starting mailbox sync..."` (Info)
   - Creates `Progress<SyncProgress>` adapter that converts to `SyncProgressEntry` (Info)
   - Calls `_syncMailboxHandler.HandleAsync(new SyncMailboxCommand(adapter), ct)`
   - On success: extracts `ReportsCreated` → `AttachmentsDownloaded`, collects errors as warnings
   - On failure: records error, **does not abort** — proceeds to Step 2

2. **Step 2 — Report processing**:
   - Reports `"Processing reports..."` (Info)
   - Calls `_processReportsHandler.HandleAsync(new ProcessReportsCommand(), ct)`
   - On success: extracts `ReportsProcessed`, `FilingsCreated`, collects errors
   - On failure: records error

3. **Step 3 — Aggregation**:
   - Combines counts and errors from both steps into `SyncAllResult`
   - Returns `Success` with the aggregated result

**Cancellation**: `CancellationToken` is threaded through both handler calls. Each inner handler
checks the token at natural checkpoints (e.g., between mailboxes, between reports). Cancellation
throws `OperationCanceledException`, which propagates to the caller (`SyncViewModel`).

**No rollback**: Work completed before cancellation or error is retained. Emails already downloaded,
reports already processed, and filings already created are not rolled back.

---

## Progress Contract

### `IProgress<SyncProgressEntry>` callback

**Direction**: Application → Desktop (push model)  
**Threading**: `SyncViewModel` creates the `Progress<SyncProgressEntry>` instance on the UI thread
so callbacks are automatically marshalled via `SynchronizationContext`.

#### Progress entry shape

```csharp
public enum SyncProgressSeverity { Info, Warning, Error }

public sealed record SyncProgressEntry(
    DateTimeOffset Timestamp,      // point-in-time when event occurred
    string Message,                // human-readable description
    SyncProgressSeverity Severity  // drives visual styling
);
```

#### Expected progress entry sequence

| Order | Message pattern | Severity | Source |
|-------|----------------|----------|--------|
| 1 | `"Starting mailbox sync..."` | Info | Handler start |
| 2..N | `"{CurrentFile}"` or `"Processing {Processed}/{Total}"` | Info | `SyncProgress` adapter |
| N+1 | `"Mailbox sync complete: {N} attachment(s) downloaded"` | Info | Post-sync summary |
| N+1..M | Per-error messages from sync step | Warning | `SyncResult.Errors` |
| M+1 | `"Processing reports..."` | Info | Report step start |
| M+2 | `"Processed {N} report(s), created {M} filing(s)"` or `"No new reports to process."` | Info | Post-process summary |
| M+2..K | Per-error messages from process step | Warning | `ProcessReportsResult.Errors` |
| K+1 | (on fatal error only) Error message | Error | Handler failure |
| K+1 | (on cancel only) `"Sync cancelled by user"` | Info | ViewModel catch |

---

## Desktop ↔ Application Boundary

### `SyncViewModel` consumption pattern

```csharp
// SyncViewModel.RunSyncAsync (simplified)
var progress = new Progress<SyncProgressEntry>(entry =>
    scheduler.Schedule(() => LogEntries.Add(SyncProgressEntryViewModel.From(entry))));

var result = await _syncHandler.HandleAsync(
    new SyncAllCommand(), progress, _cts.Token);

if (result.IsSuccess)
{
    var r = result.Value;
    // Build summary message from r.AttachmentsDownloaded, r.ReportsProcessed, etc.
    // Auto-navigate if: r.FilingsCreated > 0 && r.Errors.Count == 0
}
```

### Auto-navigation contract

| Condition | Action |
|-----------|--------|
| `result.FilingsCreated > 0 && result.Errors.Count == 0` | Call `_navigateToFilings()` → sets `MainWindowViewModel.SelectedEntry` to Filings |
| `result.FilingsCreated == 0` | Stay on Sync pane |
| `result.Errors.Count > 0` (regardless of filings) | Stay on Sync pane |
| `OperationCanceledException` caught | Stay on Sync pane |

---

## DI Registration Contract

### Application handler registration

```csharp
// In CompositionRoot.AddDesktopServices()
services.AddTransient<ISyncAllCommandHandler, SyncAllCommandHandler>();
```

### Dependencies resolved by DI

| Interface | Implementation | Registered in |
|-----------|---------------|---------------|
| `ISyncAllCommandHandler` | `SyncAllCommandHandler` | `CompositionRoot` |
| `ICommandHandler<SyncMailboxCommand, Result<SyncResult, Error>>` | `SyncMailboxCommandHandler` | `InfrastructureServiceExtensions` |
| `ICommandHandler<ProcessReportsCommand, Result<ProcessReportsResult, Error>>` | `ProcessReportsCommandHandler` | `InfrastructureServiceExtensions` |

### `SyncViewModel` creation (not DI-registered)

```csharp
// In MainWindowViewModel constructor:
var syncVm = ActivatorUtilities.CreateInstance<SyncViewModel>(
    provider, navigateToFilings_sync);
// navigateToFilings_sync : Action (not Action<Guid>)
```

---

## Adapter Pattern: `SyncProgress` → `SyncProgressEntry`

The orchestrator bridges the existing `SyncProgress` counter-style DTO to the new `SyncProgressEntry`
log-entry-style DTO:

```csharp
// Inside SyncAllCommandHandler.HandleAsync
var internalProgress = new Progress<SyncProgress>(p =>
{
    var entry = new SyncProgressEntry(
        DateTimeOffset.Now,
        p.CurrentFile ?? $"Processing {p.Processed}/{p.Total}",
        SyncProgressSeverity.Info);
    progress.Report(entry);
});

var syncResult = await _syncMailboxHandler.HandleAsync(
    new SyncMailboxCommand(internalProgress), ct);
```

**Key constraints**:
- The existing `SyncMailboxCommand` receives `IProgress<SyncProgress>?` via its **constructor**
  parameter (`Progress`), not as a method argument on `HandleAsync`
- The existing `SyncProgress` DTO is **not modified**
- The existing `SyncMailboxCommandHandler` is **not modified**
- The adapter is created fresh on each `HandleAsync` invocation (no shared state)
