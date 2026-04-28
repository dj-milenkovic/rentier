# Quickstart: Sync Per-Report Progress Log

**Feature**: 043-sync-per-report-log  
**Branch**: `043-sync-per-report-log`

## Prerequisites

- .NET 8 SDK
- Working Rentier solution (`dotnet build` passes)
- Familiarity with `IProgress<T>` pattern and existing `SyncAllCommandHandler` flow

## Implementation Order

### Step 1: Create `ReportProcessingDetail` DTO

**File**: `src/Rentier.Application/DTOs/ReportProcessingDetail.cs`

```csharp
namespace Rentier.Application.DTOs;

public sealed record ReportProcessingDetail(
    string ReportName,
    int FilingsCreated,
    int FilingsFailed,
    SyncProgressSeverity Severity)
{
    public static SyncProgressSeverity ClassifySeverity(int created, int failed)
        => (created, failed) switch
        {
            (_, 0)    => SyncProgressSeverity.Info,
            (> 0, > 0) => SyncProgressSeverity.Warning,
            (0, > 0)  => SyncProgressSeverity.Error,
            _         => SyncProgressSeverity.Info
        };

    public string ToLogMessage()
        => $"Report '{ReportName}': {FilingsCreated} filing(s) created, {FilingsFailed} failed.";
}
```

### Step 2: Add `Progress` to `ProcessReportsCommand`

**File**: `src/Rentier.Application/Commands/ProcessReportsCommand.cs`

Add optional `IProgress<SyncProgressEntry>?` parameter:

```csharp
public sealed record ProcessReportsCommand(
    IProgress<SyncProgressEntry>? Progress = null);
```

### Step 3: Add `ReportDetails` to `ProcessReportsResult`

**File**: `src/Rentier.Application/DTOs/ProcessReportsResult.cs`

```csharp
public sealed record ProcessReportsResult(
    int FilingsCreated,
    int ReportsProcessed,
    int ReportsErrored,
    IReadOnlyList<FilingCreationError> EventErrors,
    int ReportsPartialError = 0,
    IReadOnlyList<ReportProcessingDetail>? ReportDetails = null);
```

### Step 4: Emit per-report progress in `ProcessReportsCommandHandler`

**File**: `src/Rentier.Application/Handlers/ProcessReportsCommandHandler.cs`

Key changes inside the `foreach (var report in initReports)` loop:

1. After each report's success/failure determination, build a `ReportProcessingDetail`
2. Call `command.Progress?.Report(...)` with the formatted message and severity
3. Collect details into a list returned in the result

```csharp
// After determining created/failed counts for a report:
var severity = ReportProcessingDetail.ClassifySeverity(created, failed);
var detail = new ReportProcessingDetail(report.ReportName, created, failed, severity);
reportDetails.Add(detail);
command.Progress?.Report(new SyncProgressEntry(
    DateTimeOffset.Now, detail.ToLogMessage(), severity));
```

For error cases (no attachment, parse failure, etc.):
```csharp
var detail = new ReportProcessingDetail(report.ReportName, 0, 0, SyncProgressSeverity.Error);
reportDetails.Add(detail);
command.Progress?.Report(new SyncProgressEntry(
    DateTimeOffset.Now, $"Report '{report.ReportName}': processing error — {errorMessage}.",
    SyncProgressSeverity.Error));
```

### Step 5: Pass `IProgress` through from `SyncAllCommandHandler`

**File**: `src/Rentier.Application/Handlers/SyncAllCommandHandler.cs`

Change the ProcessReportsCommand instantiation:

```csharp
// BEFORE
var processResult = await _processReportsHandler.HandleAsync(
    new ProcessReportsCommand(), ct);

// AFTER
var processResult = await _processReportsHandler.HandleAsync(
    new ProcessReportsCommand(Progress: progress), ct);
```

### Step 6: Write tests

**Application tests** (priority):
- `ReportProcessingDetail.ClassifySeverity` with all severity combinations
- `ProcessReportsCommandHandler` emits correct progress entries per report
- `SyncAllCommandHandler` passes progress through to ProcessReportsCommand

**No Desktop tests needed** — existing UI pipeline handles new entries automatically.

## Build & Verify

```bash
dotnet build src/Rentier.Application/Rentier.Application.csproj
dotnet test tests/Rentier.Application.Tests/
```

## What NOT to Change

- **Domain layer**: No changes. Severity is an Application concern.
- **Infrastructure layer**: No changes. Repositories are unchanged.
- **Desktop layer**: No changes. Existing `SyncProgressEntryViewModel` + `SyncSeverityBrushConverter` already support Info/Warning/Error rendering.
- **SyncProgressEntry/SyncProgressSeverity**: Already sufficient. No new enum values needed.
