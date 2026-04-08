# Data Model: One-Click Sync Workflow (015)

**Feature**: `015-one-click-sync-workflow`  
**Created**: 2025-07-14  
**Layers affected**: Application · Desktop (no Domain or Infrastructure changes)

---

## 1. Domain Entity Changes

**None.** This is an orchestration-only feature. No domain entities, value objects, or state machines
are added or modified. All domain mutations are delegated to existing handlers
(`SyncMailboxCommandHandler`, `ProcessReportsCommandHandler`).

---

## 2. Application DTOs

### `SyncProgressSeverity` (enum)

| Value | Int | Meaning |
|-------|-----|---------|
| `Info` | 0 | Normal progress step |
| `Warning` | 1 | Non-fatal issue (e.g., one mailbox unreachable) |
| `Error` | 2 | Fatal step failure |

### `SyncProgressEntry` (ephemeral log entry)

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| `Timestamp` | `DateTimeOffset` | required | Point-in-time for logging; not a business date → `DateTimeOffset` is correct (not `DateOnly`) |
| `Message` | `string` | required, non-empty | Human-readable progress description |
| `Severity` | `SyncProgressSeverity` | required | Drives UI icon and colour |

**Lifecycle**: Exists only in memory during the application session. Not persisted to database.
Not part of any domain aggregate. Created by `SyncAllCommandHandler` and consumed by
`SyncViewModel` via `IProgress<SyncProgressEntry>`.

**Distinction from existing `SyncProgress`**: The existing `SyncProgress(int Total, int Processed,
string? CurrentFile, bool IsComplete)` is a counter-style DTO used internally by
`SyncMailboxCommandHandler`. `SyncProgressEntry` is a log-entry-style DTO used by the orchestration
layer. The two are bridged inside `SyncAllCommandHandler` via a `Progress<SyncProgress>` adapter —
the existing DTO is **not modified**.

### `SyncAllResult` (aggregated sync outcome)

| Field | Type | Source |
|-------|------|--------|
| `MailboxesSynced` | `int` | Count of mailbox groups processed (approximation: 1 on success, 0 on failure) |
| `AttachmentsDownloaded` | `int` | `SyncResult.ReportsCreated` |
| `ReportsProcessed` | `int` | `ProcessReportsResult.ReportsProcessed` |
| `FilingsCreated` | `int` | `ProcessReportsResult.FilingsCreated` |
| `Errors` | `IReadOnlyList<string>` | Aggregated from `SyncResult.Errors` + `ProcessReportsResult.Errors` |

**Invariant**: `Errors` is never null — empty list when no errors occurred.

---

## 3. Application Commands

### `SyncAllCommand`

| Field | Type | Default |
|-------|------|---------|
| *(none)* | — | — |

**Parameterless record**: `public sealed record SyncAllCommand();`  
Targets all configured mailboxes and importers. No filtering or targeting parameters.

---

## 4. Application Interfaces

### `ISyncAllCommandHandler` (dedicated — not `ICommandHandler<TCmd, TResult>`)

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
| `command` | `SyncAllCommand` | Parameterless; identifies intent |
| `progress` | `IProgress<SyncProgressEntry>` | Callback for real-time progress entries; must not be null |
| `ct` | `CancellationToken` | Respected by all async operations; checked between steps |
| Returns | `Result<SyncAllResult, Error>` | Always `Success` with aggregated result (errors in `SyncAllResult.Errors`); `Failure` only for catastrophic/unexpected errors |

**Why not `ICommandHandler<SyncAllCommand, Result<SyncAllResult, Error>>`**: The standard interface
signature is `HandleAsync(TCommand, CancellationToken)` — it cannot carry `IProgress<T>`. Adding
progress to the command record is rejected because records should contain data, not callbacks.

---

## 5. Database Schema Changes

**None.** No new tables, columns, indexes, or EF Core migrations. All database writes are performed
by the existing `SyncMailboxCommandHandler` and `ProcessReportsCommandHandler`.

---

## 6. Desktop View-Model State

### `SyncProgressEntryViewModel` (display-only snapshot)

| Property | Type | Derived from |
|----------|------|-------------|
| `Icon` | `string` | `Severity` → `"✓"` (Info), `"⚠"` (Warning), `"✗"` (Error) |
| `Message` | `string` | `SyncProgressEntry.Message` |
| `Timestamp` | `string` | `SyncProgressEntry.Timestamp.ToString("HH:mm:ss")` |
| `ForegroundColor` | `string` | `Severity` → `"Green"` (Info), `"Orange"` (Warning), `"Red"` (Error) |

### `SyncViewModel` reactive state

| Property | Type | Default | Notes |
|----------|------|---------|-------|
| `LogEntries` | `ObservableCollection<SyncProgressEntryViewModel>` | empty | Bound to ItemsControl; cleared on each sync start |
| `IsRunning` | `bool` | `false` | Bound to `SyncCommand.IsExecuting`; controls button visibility |
| `SummaryMessage` | `string?` | `null` | Shows completion/error summary after sync ends |

### Commands

| Command | Type | canExecute |
|---------|------|------------|
| `SyncCommand` | `ReactiveCommand<Unit, Unit>` | default (auto-disabled while executing) |
| `CancelCommand` | `ReactiveCommand<Unit, Unit>` | always (delegates to `_cts?.Cancel()`) |

### Constructor parameters

| Parameter | Type | Source |
|-----------|------|--------|
| `syncHandler` | `ISyncAllCommandHandler` | DI (registered in CompositionRoot) |
| `navigateToFilings` | `Action` | Closure from `MainWindowViewModel` |
| `scheduler` | `IScheduler?` | Optional; defaults to `RxApp.MainThreadScheduler` |

### Auto-navigation rule

After `HandleAsync` returns successfully:
- **Navigate** to Filings pane if `result.FilingsCreated > 0 && result.Errors.Count == 0`
- **Stay** on Sync pane otherwise (zero filings, errors present, or cancellation)

---

## 7. Existing Types Referenced (unchanged)

| Type | Location | Used by |
|------|----------|---------|
| `SyncMailboxCommand(IProgress<SyncProgress>? Progress = null)` | `Application/Commands/` | `SyncAllCommandHandler` constructs with adapter |
| `SyncResult(int ReportsCreated, IReadOnlyList<string> Errors)` | `Application/DTOs/` | `SyncAllCommandHandler` reads to populate `SyncAllResult` |
| `SyncProgress(int Total, int Processed, string? CurrentFile, bool IsComplete)` | `Application/DTOs/` | Adapter converts to `SyncProgressEntry` |
| `ProcessReportsCommand` | `Application/Commands/` | `SyncAllCommandHandler` constructs |
| `ProcessReportsResult(int FilingsCreated, int ReportsProcessed, int ReportsErrored, IReadOnlyList<string> Errors)` | `Application/DTOs/` | `SyncAllCommandHandler` reads to populate `SyncAllResult` |
| `Result<TValue, TError>` | `Application/Common/` | Return type wrapper |
| `Error(string Code, string Message)` | `Application/Common/` | Error representation |
| `ICommandHandler<TCommand, TResult>` | `Application/Interfaces/` | Standard handler interface — NOT used by `SyncAllCommandHandler` |
