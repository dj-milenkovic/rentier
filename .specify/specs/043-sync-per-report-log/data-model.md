# Data Model: Sync Per-Report Progress Log

**Feature**: 043-sync-per-report-log  
**Date**: 2025-07-17

## New Types

### ReportProcessingDetail (Application DTO)

**Location**: `src/Rentier.Application/DTOs/ReportProcessingDetail.cs`  
**Kind**: Immutable record (DTO)

```csharp
namespace Rentier.Application.DTOs;

/// <summary>
/// Per-report outcome emitted during sync report processing.
/// Captures the filename, filing counts, and computed severity for a single report.
/// </summary>
public sealed record ReportProcessingDetail(
    string ReportName,
    int FilingsCreated,
    int FilingsFailed,
    SyncProgressSeverity Severity)
{
    /// <summary>
    /// Determines severity from filing outcome counts.
    /// </summary>
    public static SyncProgressSeverity ClassifySeverity(int created, int failed)
        => (created, failed) switch
        {
            (_, 0)              => SyncProgressSeverity.Info,    // All success or empty report
            (> 0, > 0)          => SyncProgressSeverity.Warning, // Mixed results
            (0, > 0)            => SyncProgressSeverity.Error,   // Total failure
            _                   => SyncProgressSeverity.Info     // Unreachable, defensive
        };

    /// <summary>
    /// Formats the standard log message for this report.
    /// </summary>
    public string ToLogMessage()
        => $"Report '{ReportName}': {FilingsCreated} filing(s) created, {FilingsFailed} failed.";
}
```

| Field | Type | Description | Validation |
|-------|------|-------------|------------|
| `ReportName` | `string` | Attachment filename from `Report.ReportName` | Non-null (guaranteed by entity) |
| `FilingsCreated` | `int` | Count of filings successfully created for this report | >= 0 |
| `FilingsFailed` | `int` | Count of filings that failed for this report | >= 0 |
| `Severity` | `SyncProgressSeverity` | Computed: Info/Warning/Error based on counts | Enum value |

**Severity Classification Rules** (from spec FR-003/004/005):

| created | failed | Severity | Example |
|---------|--------|----------|---------|
| ≥ 0 | 0 | Info | All filings created, or empty report |
| > 0 | > 0 | Warning | Partial success |
| 0 | > 0 | Error | Total failure |

## Modified Types

### ProcessReportsResult (Application DTO)

**Location**: `src/Rentier.Application/DTOs/ProcessReportsResult.cs`  
**Change**: Add `ReportDetails` collection parameter.

```csharp
// BEFORE
public sealed record ProcessReportsResult(
    int FilingsCreated,
    int ReportsProcessed,
    int ReportsErrored,
    IReadOnlyList<FilingCreationError> EventErrors,
    int ReportsPartialError = 0);

// AFTER
public sealed record ProcessReportsResult(
    int FilingsCreated,
    int ReportsProcessed,
    int ReportsErrored,
    IReadOnlyList<FilingCreationError> EventErrors,
    int ReportsPartialError = 0,
    IReadOnlyList<ReportProcessingDetail>? ReportDetails = null);
```

### ProcessReportsCommand (Application Command)

**Location**: `src/Rentier.Application/Commands/ProcessReportsCommand.cs`  
**Change**: Add optional `IProgress<SyncProgressEntry>?` property.

```csharp
// BEFORE
public sealed record ProcessReportsCommand();

// AFTER
public sealed record ProcessReportsCommand(
    IProgress<SyncProgressEntry>? Progress = null);
```

## Unchanged Types (leveraged as-is)

| Type | Location | Why Unchanged |
|------|----------|---------------|
| `SyncProgressEntry` | `Application/DTOs/` | Already has `Message` + `SyncProgressSeverity` — per-report entries are just more instances |
| `SyncProgressSeverity` | `Application/DTOs/` | Already defines `Info`, `Warning`, `Error` (plus `CursorTransition`, `DuplicateHandled`) |
| `SyncProgressEntryViewModel` | `Desktop/ViewModels/` | Already maps severity to icons (•, ⚠, ✕) |
| `SyncSeverityBrushConverter` | `Desktop/Converters/` | Already maps severity to colours (green, amber, red) |
| `Report` | `Domain/Entities/` | `ReportName` property already available |
| `SyncAllResult` | `Application/DTOs/` | Aggregate counts remain unchanged |

## Entity Relationships

```text
SyncAllCommandHandler
    │
    ├── IProgress<SyncProgressEntry>  ─── real-time UI log lines
    │       ▲
    │       │ .Report() per report
    │       │
    └── ProcessReportsCommandHandler
            │
            ├── yields: ReportProcessingDetail (per report)
            │     ├── .ReportName     ← Report.ReportName
            │     ├── .FilingsCreated ← per-report count
            │     ├── .FilingsFailed  ← per-report count
            │     └── .Severity       ← ClassifySeverity()
            │
            └── returns: ProcessReportsResult
                    └── .ReportDetails: IReadOnlyList<ReportProcessingDetail>
```

## State Transitions

No new state transitions. The existing `Report.Status` state machine (`Init → Processed | PartialError | Error`) is unchanged. Severity classification is derived from filing counts, not from report status.
