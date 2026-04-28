# Contract: IProgress<SyncProgressEntry> — Per-Report Progress

**Feature**: 043-sync-per-report-log  
**Date**: 2025-07-17

## Overview

This feature uses the existing `IProgress<SyncProgressEntry>` contract (standard .NET `System.IProgress<T>`) as the channel for delivering per-report log lines from the Application layer to the Desktop UI. No new interface is introduced.

## Contract: Progress Entry Per Report

### Emitter

`ProcessReportsCommandHandler` (Application layer)

### Consumer

`SyncViewModel` via `Progress<SyncProgressEntry>` callback → `ObservableCollection<SyncProgressEntryViewModel>` (Desktop layer)

### Message Format

For each report processed during sync, exactly one `SyncProgressEntry` is emitted:

```csharp
new SyncProgressEntry(
    Timestamp: DateTimeOffset.Now,
    Message:   $"Report '{report.ReportName}': {created} filing(s) created, {failed} failed.",
    Severity:  ReportProcessingDetail.ClassifySeverity(created, failed)
)
```

### Severity Rules

| Condition | Severity | UI Colour |
|-----------|----------|-----------|
| `failed == 0` | `SyncProgressSeverity.Info` | Green (secondary text) |
| `created > 0 && failed > 0` | `SyncProgressSeverity.Warning` | Amber |
| `created == 0 && failed > 0` | `SyncProgressSeverity.Error` | Red |
| Processing exception (before filing attempt) | `SyncProgressSeverity.Error` | Red |

### Ordering Guarantees

- Per-report entries are emitted in the order reports are iterated (sequential `foreach`).
- Each entry is emitted **immediately** after that report finishes processing (before next report starts).
- The existing aggregate summary line (`"Processed N report(s), created M filing(s)."`) is emitted **after** all per-report lines.

### Error Cases

| Scenario | Message Format | Severity |
|----------|---------------|----------|
| Report has no attachment | `"Report '{name}': processing error — no attachment content."` | Error |
| Importer not found | `"Report '{name}': processing error — importer not found."` | Error |
| Parse failure | `"Report '{name}': processing error — {parseError}."` | Error |
| Unexpected exception | `"Report '{name}': processing error — {ex.Message}."` | Error |
| Empty report (0 events) | `"Report '{name}': 0 filing(s) created, 0 failed."` | Info |

### Passthrough Contract

`SyncAllCommandHandler` passes `IProgress<SyncProgressEntry>` to `ProcessReportsCommandHandler` via `ProcessReportsCommand.Progress`:

```csharp
// SyncAllCommandHandler.HandleAsync
var processResult = await _processReportsHandler.HandleAsync(
    new ProcessReportsCommand(Progress: progress), ct);
```

`ProcessReportsCommandHandler` reads from the command:

```csharp
// ProcessReportsCommandHandler.HandleAsync
var progress = command.Progress;
// ... after each report:
progress?.Report(new SyncProgressEntry(...));
```

The null-conditional `?.` ensures backward compatibility when `Progress` is null.
