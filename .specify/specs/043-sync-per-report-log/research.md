# Research: Sync Per-Report Progress Log

**Feature**: 043-sync-per-report-log  
**Date**: 2025-07-17

## R-001: How to plumb IProgress into ProcessReportsCommandHandler

**Decision**: Add an optional `IProgress<SyncProgressEntry>?` parameter to the `ProcessReportsCommand` record and pass it through from `SyncAllCommandHandler`.

**Rationale**: 
- `ProcessReportsCommandHandler` implements `ICommandHandler<ProcessReportsCommand, Result<ProcessReportsResult, Error>>` which has a fixed `HandleAsync(TCommand, CancellationToken)` signature.
- Adding `IProgress<T>` to the command record (as a property, not constructor-serializable state) is the simplest approach since commands are in-process only (never serialized).
- Alternative: extend `ICommandHandler<T,R>` to accept `IProgress` — rejected because it would affect all handlers and violate ISP.
- Alternative: inject `IProgress<SyncProgressEntry>` via DI — rejected because the progress instance is per-invocation (scoped to a single sync run), not a singleton service.

**Alternatives considered**:
1. ~~New `IProgressAwareCommandHandler<T,R>` interface~~ — over-engineering for one handler.
2. ~~DI-scoped IProgress~~ — IProgress is created per-sync-run in SyncViewModel, not a DI concern.
3. **Chosen**: `ProcessReportsCommand` gains an `IProgress<SyncProgressEntry>?` property. Handler reads it from the command. Null means "no progress reporting" (backward compatible).

## R-002: Per-report detail — new DTO vs enriching progress entries

**Decision**: Both. Create `ReportProcessingDetail` record for structured data in `ProcessReportsResult`, AND emit `SyncProgressEntry` per report for real-time UI display.

**Rationale**:
- `SyncProgressEntry` is the real-time progress channel (fire-and-forget via `IProgress<T>`). The UI needs these during processing.
- `ProcessReportsResult` is the final result DTO. Adding `IReadOnlyList<ReportProcessingDetail>` lets `SyncAllCommandHandler` build accurate aggregate messages and enables future features (e.g., retry-per-report).
- The dual approach matches the existing pattern: `SyncMailboxCommand` both reports progress entries AND returns `SyncResult` with aggregate counts.

**Alternatives considered**:
1. ~~Only IProgress entries, no DTO~~ — loses structured data in the result; aggregation in `SyncAllCommandHandler` becomes harder.
2. ~~Only DTO, generate progress entries in SyncAllCommandHandler~~ — delays log lines until handler returns; violates FR-009 (real-time appearance).
3. **Chosen**: Emit per-report progress in `ProcessReportsCommandHandler` for real-time display + return `ReportProcessingDetail` list in result for structured aggregation.

## R-003: Severity classification — where to compute

**Decision**: Compute severity in `ProcessReportsCommandHandler` using a pure static helper method on `ReportProcessingDetail`.

**Rationale**:
- Severity is determined by `(created, failed)` counts — pure arithmetic, no I/O.
- Placing the logic on the DTO record as a static factory or computed property keeps it testable without mocking.
- Not a Domain concern (it's presentation-level classification of Application-layer results).
- Matches existing `SyncProgressSeverity` enum which is already in the Application DTOs namespace.

**Rules** (from spec FR-003/004/005):
- `failed == 0` → `Info` (even if `created == 0`, e.g., empty report)
- `created > 0 && failed > 0` → `Warning` (mixed results)
- `created == 0 && failed > 0` → `Error` (total failure)
- Processing exception before filing attempt → `Error`

**Alternatives considered**:
1. ~~Domain service~~ — severity of a progress log line is not a domain concept.
2. ~~ViewModel logic~~ — would duplicate logic in UI layer and is untestable without UI context.
3. **Chosen**: Static method or computed property on `ReportProcessingDetail` record.

## R-004: Report filename — available data

**Decision**: Use `Report.ReportName` property (already exists on the Domain entity).

**Rationale**:
- `Report` entity (Domain) has `public string ReportName { get; private set; }` which contains the attachment filename (e.g., `"U1234567_20240101_20240131.csv"`).
- This is already loaded when `ProcessReportsCommandHandler` fetches reports via `_reportRepository.GetByStatusAsync(ReportStatus.Init, ct)`.
- No additional DB queries or entity changes needed.

**Alternatives considered**:
1. ~~Report.Id (Guid)~~ — not human-readable.
2. ~~Composite of ImportDate + ImporterId~~ — less recognisable than the filename.
3. **Chosen**: `report.ReportName` — the actual attachment filename.

## R-005: Message format string

**Decision**: Use exact format from spec: `"Report '{ReportName}': N filing(s) created, M failed."`

**Rationale**:
- Matches FR-002 exactly.
- Report name in single quotes provides visual delimiter for filenames with spaces or special characters.
- `filing(s)` with parenthesised plural handles N=1 case without extra logic.
- Edge case (empty report, 0 created, 0 failed): `"Report 'foo.csv': 0 filing(s) created, 0 failed."` → Info severity per spec.

## R-006: Desktop layer changes — are any needed?

**Decision**: No Desktop layer code changes required.

**Rationale**:
- `SyncViewModel` already creates a `Progress<SyncProgressEntry>` callback and adds each entry to `ObservableCollection<SyncProgressEntryViewModel>`.
- `SyncProgressEntryViewModel` already maps `Info` → "•", `Warning` → "⚠", `Error` → "✕".
- `SyncSeverityBrushConverter` already maps `Error` → red, `Warning` → amber, default → secondary text.
- New per-report entries are just more `SyncProgressEntry` objects — the existing pipeline handles them automatically.

**Alternatives considered**: None — the existing UI pipeline is fully sufficient.

## R-007: Thread safety of IProgress reports

**Decision**: No additional thread safety measures needed.

**Rationale**:
- `ProcessReportsCommandHandler` processes reports sequentially in a `foreach` loop (not parallel).
- `IProgress<T>.Report()` is safe to call from any thread; `Progress<T>` captures the `SynchronizationContext` at construction time and dispatches callbacks to the UI thread.
- The existing pattern in `SyncAllCommandHandler` already works this way.

## R-008: Backward compatibility of ProcessReportsResult change

**Decision**: Add optional parameter with default to `ProcessReportsResult` record constructor.

**Rationale**:
- Current: `ProcessReportsResult(int FilingsCreated, int ReportsProcessed, int ReportsErrored, IReadOnlyList<FilingCreationError> EventErrors, int ReportsPartialError = 0)`
- New: add `IReadOnlyList<ReportProcessingDetail> ReportDetails` with default `Array.Empty<ReportProcessingDetail>()`.
- Using a default value preserves backward compatibility for any existing callers or tests that construct the record without the new parameter.
