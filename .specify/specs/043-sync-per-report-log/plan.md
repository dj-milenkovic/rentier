# Implementation Plan: Sync Per-Report Progress Log

**Branch**: `043-sync-per-report-log` | **Date**: 2025-07-17 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `.specify/specs/043-sync-per-report-log/spec.md`

## Summary

During sync, emit one `SyncProgressEntry` per report processed with format `"Report '{ReportName}': N filing(s) created, M failed."` and severity colour-coding (Info=green, Warning=amber, Error=red). The existing aggregate progress line is preserved. Implementation requires:

1. A new `ReportProcessingDetail` record in Application DTOs to capture per-report filename/counts
2. `ProcessReportsCommandHandler` emits progress entries per report via `IProgress<SyncProgressEntry>` (plumbed through from `SyncAllCommandHandler`)
3. No Domain or Infrastructure changes — severity logic is pure arithmetic on integer counts
4. Existing `SyncProgressEntryViewModel` and `SyncSeverityBrushConverter` already support Info/Warning/Error rendering

## Technical Context

**Language/Version**: C# 12 / .NET 8  
**Primary Dependencies**: Avalonia UI 11+, ReactiveUI, CommunityToolkit.Mvvm, EF Core 8  
**Storage**: SQLite (no schema changes for this feature — log lines are ephemeral)  
**Testing**: xUnit + FluentAssertions + NSubstitute  
**Target Platform**: Windows + macOS (cross-platform desktop)  
**Project Type**: Desktop application (Clean Architecture)  
**Performance Goals**: UI must not freeze during sync; per-report log lines appear in real time  
**Constraints**: All I/O async; no blocking UI thread; no disk persistence for log lines  
**Scale/Scope**: Typical sync: 1–50 reports; UI ObservableCollection can handle this volume

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- [x] Clean Architecture boundary is preserved (`Desktop -> Application -> Domain`; Infrastructure implements Application contracts only).
  - Application emits structured progress data; Desktop renders it. No cross-layer violations.
- [x] All monetary/rate/percentage values are modeled as `decimal`.
  - No new monetary fields. Filing counts are `int`.
- [x] All business dates are modeled as `DateOnly`; boundary conversions are identified.
  - No new date fields introduced.
- [x] Security/privacy constraints hold: local-first data, OS credential store for secrets, no telemetry.
  - Log lines contain only filenames and integer counts. No secrets. No network calls.
- [x] External network usage is limited to approved endpoints (IMAP and NBS) or explicitly justified.
  - No new network usage.
- [x] All I/O paths are async; UI work avoids blocking calls and uses reactive async command flow.
  - `IProgress<T>.Report()` is synchronous by design but delivers to UI via `Progress<T>` callback on captured SynchronizationContext. Existing pattern preserved.
- [x] Tests and coverage impact are defined for Domain (rule/state coverage) and Application (>=90%).
  - Domain: no changes needed. Application: unit tests for severity classification logic and per-report progress emission (>=90%).
- [x] Feature work is mapped to an approved spec task under `.specify/tasks/`.
  - Spec at `.specify/specs/043-sync-per-report-log/spec.md`.

## Project Structure

### Documentation (this feature)

```text
specs/043-sync-per-report-log/
├── plan.md              # This file
├── research.md          # Phase 0: research findings
├── data-model.md        # Phase 1: DTO and type definitions
├── quickstart.md        # Phase 1: implementation quickstart
├── contracts/           # Phase 1: IProgress contract
└── tasks.md             # Phase 2 output (created by /speckit.tasks)
```

### Source Code (repository root)

```text
src/
├── Rentier.Application/
│   ├── DTOs/
│   │   ├── ProcessReportsResult.cs        # MODIFY: add PerReportDetails list
│   │   ├── ReportProcessingDetail.cs      # NEW: per-report filename + counts + severity
│   │   └── SyncProgressEntry.cs           # UNCHANGED: already has Info/Warning/Error
│   └── Handlers/
│       ├── ProcessReportsCommandHandler.cs # MODIFY: accept IProgress, emit per-report entries
│       └── SyncAllCommandHandler.cs        # MODIFY: pass IProgress to ProcessReportsCommand
├── Rentier.Desktop/
│   ├── ViewModels/
│   │   ├── SyncViewModel.cs               # UNCHANGED: already wired to IProgress<SyncProgressEntry>
│   │   └── SyncProgressEntryViewModel.cs  # UNCHANGED: already maps severity to icons
│   └── Converters/
│       └── SyncSeverityBrushConverter.cs   # UNCHANGED: already maps severity to colours

tests/
├── Rentier.Application.Tests/
│   └── Handlers/
│       ├── ProcessReportsCommandHandlerTests.cs  # MODIFY: add per-report progress tests
│       └── SyncAllCommandHandlerTests.cs         # MODIFY: verify IProgress passthrough
└── Rentier.Desktop.Tests/
    └── ViewModels/
        └── SyncViewModelTests.cs                 # EXISTING: verify log entries appear
```

**Structure Decision**: Existing Clean Architecture 4-project layout. Changes are concentrated in Application layer (new DTO, handler modifications). Desktop layer requires zero changes — existing `SyncProgressEntryViewModel` and `SyncSeverityBrushConverter` already support all three severity levels with correct colour-coding.

## Complexity Tracking

> No constitution violations. No complexity justifications needed.
