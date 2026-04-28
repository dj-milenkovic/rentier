# Tasks: Sync Per-Report Progress Log

**Feature**: `043-sync-per-report-log`  
**Input**: `.specify/specs/043-sync-per-report-log/` — spec.md, plan.md, research.md, data-model.md, contracts/, quickstart.md  
**Branch**: `043-sync-per-report-log`

**Tests**: Include test tasks (constitution quality gate: Application layer ≥ 90% coverage on changed handlers).

**Scope summary**: Emit one `SyncProgressEntry` per report processed during sync with format `"Report '{filename}': N filing(s) created, M failed."` and severity colour-coding (Info/Warning/Error). Application layer only — Desktop layer (`SyncProgressEntryViewModel`, `SyncSeverityBrushConverter`) already supports all severity levels with zero changes.

---

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: Which user story this task belongs to (US1, US2, US3)

---

## Phase 1: Setup

**Purpose**: Confirm starting state before any changes.

- [X] T001 Verify feature branch `043-sync-per-report-log` is checked out and `dotnet build src/Rentier.Application/Rentier.Application.csproj` passes with zero errors (baseline gate before any edits)

---

## Phase 2: Foundational — New Application Types

**Purpose**: New DTO and type-level changes that BLOCK all user story implementation. No handler logic here — just type definitions that compile cleanly.

**⚠️ CRITICAL**: T005–T010 cannot start until this phase is complete.

- [X] T002 [P] Create `ReportProcessingDetail` sealed record with `ClassifySeverity(int created, int failed)` static method (switch expression: `(_, 0)→Info`, `(>0, >0)→Warning`, `(0, >0)→Error`) and `ToLogMessage()` returning `"Report '{ReportName}': {FilingsCreated} filing(s) created, {FilingsFailed} failed."` in `src/Rentier.Application/DTOs/ReportProcessingDetail.cs`
- [X] T003 [P] Add optional `IProgress<SyncProgressEntry>? Progress = null` property to `ProcessReportsCommand` record in `src/Rentier.Application/Commands/ProcessReportsCommand.cs` (change `public sealed record ProcessReportsCommand;` to `public sealed record ProcessReportsCommand(IProgress<SyncProgressEntry>? Progress = null);`)
- [X] T004 Add optional `IReadOnlyList<ReportProcessingDetail>? ReportDetails = null` trailing parameter to `ProcessReportsResult` record after `ReportsPartialError = 0` in `src/Rentier.Application/DTOs/ProcessReportsResult.cs` (existing callers are backward-compatible via default value)

**Checkpoint**: `dotnet build src/Rentier.Application/Rentier.Application.csproj` must pass before proceeding.

---

## Phase 3: User Story 1 + User Story 2 — Per-Report Log Lines with Severity (Priority: P1) 🎯 MVP

**US1 Goal**: `ProcessReportsCommandHandler` emits exactly one `SyncProgressEntry` per report as it finishes processing with the correct `"Report '{filename}': N filing(s) created, M failed."` message.

**US2 Goal**: Each emitted entry carries `SyncProgressSeverity.Info` / `.Warning` / `.Error` so the existing `SyncSeverityBrushConverter` colour-codes it automatically — no Desktop changes needed.

**Why co-delivered**: Severity is computed at the same moment the log line is emitted; separating them would require two passes over the same handler code.

**Independent Test**: Trigger a sync with three reports — one all-success (3 filings), one partial (2 created / 1 failed), one all-fail (0 created / 2 failed). Verify three log lines appear in real time with Info / Warning / Error severity badges respectively, plus the existing aggregate "Processed N report(s)…" line.

### Tests for US1 + US2 ⚠️ (write first — verify they FAIL before T008)

- [X] T005 [P] [US1] Write `ReportProcessingDetailTests` covering all `ClassifySeverity` rules: `(created:3, failed:0)→Info`, `(created:2, failed:1)→Warning`, `(created:0, failed:2)→Error`, `(created:0, failed:0)→Info` (empty report), plus `ToLogMessage()` output format `"Report 'foo.csv': 3 filing(s) created, 0 failed."` in `tests/Rentier.Application.Tests/DTOs/ReportProcessingDetailTests.cs`
- [X] T006 [P] [US1] Write `ProcessReportsCommandHandlerTests` for per-report progress emission: (a) all-success report emits exactly one `SyncProgressEntry` with `Severity=Info` and correct `ToLogMessage()` text; (b) partial-error report emits `Severity=Warning`; (c) total-failure report emits `Severity=Error`; (d) `null` progress does not throw (null-safe `?.Report()`) in `tests/Rentier.Application.Tests/Handlers/ProcessReportsCommandHandlerTests.cs`
- [X] T007 [P] [US2] Add error-branch severity tests to `ProcessReportsCommandHandlerTests`: no-attachment report, importer-not-found report, parse-failure report, and unexpected-exception report each emit one Error-severity `SyncProgressEntry` whose message matches `"Report 'X': processing error — {detail}."` format in `tests/Rentier.Application.Tests/Handlers/ProcessReportsCommandHandlerTests.cs`

### Implementation for US1 + US2

- [X] T008 [US1] Modify `ProcessReportsCommandHandler.HandleAsync` in `src/Rentier.Application/Handlers/ProcessReportsCommandHandler.cs`:
  1. Add `var reportDetails = new List<ReportProcessingDetail>();` before the `foreach` loop
  2. In the **success / partial-error / total-failure** branch: after computing `created` and `failed`, call `var severity = ReportProcessingDetail.ClassifySeverity(created, failed);`, create `var detail = new ReportProcessingDetail(report.ReportName, created, failed, severity);`, call `command.Progress?.Report(new SyncProgressEntry(DateTimeOffset.Now, detail.ToLogMessage(), severity));`, and `reportDetails.Add(detail);`
  3. In **each** existing error branch (no attachment, importer not found, parse failure, unexpected exception): emit `command.Progress?.Report(new SyncProgressEntry(DateTimeOffset.Now, $"Report '{report.ReportName}': processing error — {errorMessage}.", SyncProgressSeverity.Error));` and add matching `ReportProcessingDetail` (0/0/Error) to `reportDetails`
  4. Update the `return` statement to pass `ReportDetails: reportDetails` to `ProcessReportsResult`

**Checkpoint**: `dotnet test tests/Rentier.Application.Tests/` with T005–T007 tests passing confirms US1 + US2 are fully functional.

---

## Phase 4: User Story 3 — Aggregate Progress Remains (Priority: P2)

**Goal**: `SyncAllCommandHandler` threads the `IProgress<SyncProgressEntry>` it already receives through to `ProcessReportsCommand`, so per-report lines appear in real time. The existing aggregate "Processed N report(s), created M filing(s)." line must continue to appear **after** the per-report lines.

**Independent Test**: Run sync and verify in the UI log that per-report lines appear as each report finishes (real time, FR-009) and the aggregate summary line appears last.

### Tests for US3 ⚠️ (write first — verify they FAIL before T010)

- [X] T009 [P] [US3] Write `SyncAllCommandHandlerTests` verifying: (a) the `ProcessReportsCommand` constructed inside `HandleAsync` has its `Progress` property set to the same `IProgress<SyncProgressEntry>` instance passed to `HandleAsync`; (b) after a successful report-processing run the aggregate "Processed N report(s), created M filing(s)." entry is still reported via `progress` after all per-report entries in `tests/Rentier.Application.Tests/Handlers/SyncAllCommandHandlerTests.cs`

### Implementation for US3

- [X] T010 [US3] Change `new ProcessReportsCommand()` to `new ProcessReportsCommand(Progress: progress)` on the single call-site in `SyncAllCommandHandler.HandleAsync` in `src/Rentier.Application/Handlers/SyncAllCommandHandler.cs`

**Checkpoint**: `dotnet test tests/Rentier.Application.Tests/` with T009 test passing confirms US3 is complete and the aggregate line is preserved.

---

## Phase 5: Polish & Cross-Cutting Concerns

- [X] T011 [P] Run `dotnet build` on the full solution (`dotnet build Rentier.sln`) and resolve any compilation errors from type-signature changes to `ProcessReportsCommand` and `ProcessReportsResult` in `src/`
- [X] T012 [P] Run `dotnet test tests/Rentier.Application.Tests/` and confirm all 13+ new tests pass with zero failures; review coverage report to confirm Application handler coverage ≥ 90%
- [ ] T013 Validate quickstart.md scenarios end-to-end: trigger a real sync with all-success, partial-error, and all-fail report files; confirm (a) per-report log lines appear in real time as each report finishes, (b) Info lines show green, Warning lines show amber, Error lines show red via `SyncSeverityBrushConverter`, (c) aggregate "Processed N report(s)…" line appears last

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1** (Setup): No dependencies — start immediately
- **Phase 2** (Foundational): Depends on Phase 1 — **BLOCKS all user story phases**
- **Phase 3** (US1 + US2): Depends on Phase 2 completion
- **Phase 4** (US3): Depends on Phase 2 completion; can run **in parallel** with Phase 3
- **Phase 5** (Polish): Depends on Phases 3 + 4 completion

### User Story Dependencies

| Story | Depends on | Can be independent? |
|-------|-----------|-------------------|
| US1 (P1) | Phase 2 Foundational | Yes — testable without US2 or US3 |
| US2 (P1) | US1 (same handler) | Co-delivered with US1 |
| US3 (P2) | Phase 2 Foundational | Yes — testable without US1/US2 at unit-test level |

### Within Each Phase

- Tests (T005–T007, T009) MUST be written first and **fail** before implementation
- T002 and T003 can run in parallel (different files)
- T004 depends on T002 (references `ReportProcessingDetail` type)
- T008 depends on T002 + T003 + T004 (uses all three new/modified types)
- T010 depends on T003 (uses `ProcessReportsCommand.Progress`)

### Parallel Opportunities

- T002, T003 can run in parallel (new file vs. single-line record change)
- T005, T006, T007 can run in parallel (all in different or same test file, non-overlapping test classes)
- T009 and T005–T007 can run in parallel (different handler under test)
- T011, T012 can run in parallel (build then test; or on CI together)

---

## Parallel Execution Examples

### Phase 2 — Parallel type creation

```
Task T002: Create ReportProcessingDetail.cs (new file)
Task T003: Add Progress property to ProcessReportsCommand.cs (1-line change)
```
→ Then T004 sequentially (needs T002 type).

### Phase 3 — Parallel test writing

```
Task T005: ReportProcessingDetailTests — ClassifySeverity + ToLogMessage
Task T006: ProcessReportsCommandHandlerTests — success/partial/total-failure emission
Task T007: ProcessReportsCommandHandlerTests — error branch severity
```
→ Then T008 sequentially (single handler modification).

### Phase 3 + 4 — Parallel story phases

```
Phase 3 track: T005 → T006 → T007 → T008 (US1/US2)
Phase 4 track: T009 → T010               (US3)
```
Both tracks can run concurrently after Phase 2 completes.

---

## Implementation Strategy

### MVP (US1 + US2 only — Phases 1–3)

1. Complete Phase 1: Verify baseline
2. Complete Phase 2: New types (T002–T004)
3. Complete Phase 3: Per-report emission with severity (T005–T008)
4. **STOP and VALIDATE**: Three log lines with correct severity in real sync run
5. Merge or demo — aggregate line (US3) already works via existing code; just not threaded through yet

### Full Delivery (add US3 — Phase 4)

6. Complete Phase 4: Thread IProgress through `SyncAllCommandHandler` (T009–T010)
7. Complete Phase 5: Polish, build gate, quickstart validation (T011–T013)

---

## Notes

- **No Domain or Infrastructure changes**: All changes are in `Rentier.Application`. Domain entity `Report.ReportName` is read as-is.
- **No Desktop changes**: `SyncViewModel`, `SyncProgressEntryViewModel`, and `SyncSeverityBrushConverter` already handle Info/Warning/Error severity — per-report entries are just more `SyncProgressEntry` instances fed through the existing pipeline.
- **Backward compatibility**: `ProcessReportsCommand` and `ProcessReportsResult` use optional/default parameters — existing callers and tests that construct them without the new parameters continue to compile.
- **Thread safety**: `IProgress<T>.Report()` is safe to call from the background thread; `Progress<T>` captures the UI `SynchronizationContext` at construction and dispatches callbacks on the UI thread automatically.
- Commit after T004 (types compile), after T008 (US1+US2 green), after T010 (US3 green), and after T013 (full quickstart pass).
