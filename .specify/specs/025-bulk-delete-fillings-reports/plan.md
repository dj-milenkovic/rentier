# Implementation Plan: Bulk Delete for Filings and Reports

**Branch**: `025-bulk-delete-fillings-reports` | **Date**: 2025-07-15 | **Spec**: [spec.md](../../.specify/specs/025-bulk-delete-fillings-reports/spec.md)
**Input**: Feature specification from `.specify/specs/025-bulk-delete-fillings-reports/spec.md`

## Summary

Add bulk-delete capability to both the Filings and Reports pages. Each DataGrid gains a checkbox column with two-way selection binding. A reactive toolbar shows "Select All" / "Clear Selection" when items exist and a destructive "Delete Selected (N)" button when ≥1 item is selected. Confirmation dialogs summarise the operation (with cascade-warning for Reports). Deletion runs fully async via new CQRS commands (`BulkDeleteFilingsCommand`, `BulkDeleteReportsCommand`) that delegate to existing repository methods. After deletion, selection is cleared and the list reloads. All user-facing strings are externalised in `Strings.resx`.

## Technical Context

**Language/Version**: C# 12 / .NET 8  
**Primary Dependencies**: Avalonia UI 11+, ReactiveUI, CommunityToolkit.Mvvm, EF Core 8 (SQLite)  
**Storage**: SQLite (local-first, EF Core migrations)  
**Testing**: xUnit + FluentAssertions + NSubstitute  
**Target Platform**: Windows + macOS (cross-platform desktop via Avalonia)  
**Project Type**: Desktop application (Clean Architecture: Domain → Application → Infrastructure / Desktop)  
**Performance Goals**: Bulk delete ≤100 items in <15s; toolbar count updates <200ms; UI never frozen  
**Constraints**: Offline-capable, local-only data, no telemetry, all monetary values `decimal`, all dates `DateOnly`  
**Scale/Scope**: Filings page is paginated (20/page), Reports page loads all records

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- [x] Clean Architecture boundary is preserved (`Desktop -> Application -> Domain`; Infrastructure implements Application contracts only).
  - Desktop: new selection properties, toolbar commands, and confirmation dialogs in ViewModels/Views.
  - Application: new `BulkDeleteFilingsCommand`/`BulkDeleteReportsCommand` + handlers.
  - Infrastructure: new `DeleteManyAsync` repository methods using existing `RemoveRange` pattern.
  - Domain: **no changes** — entities are unchanged; deletion has no domain rules.
- [x] All monetary/rate/percentage values are modeled as `decimal`.
  - No new monetary fields introduced. Existing `decimal` fields in Filing remain untouched.
- [x] All business dates are modeled as `DateOnly`; boundary conversions are identified.
  - No new date fields introduced. Existing `DateOnly` fields remain untouched.
- [x] Security/privacy constraints hold: local-first data, OS credential store for secrets, no telemetry.
  - All deletion is local SQLite. No network, credentials, or telemetry involved.
- [x] External network usage is limited to approved endpoints (IMAP and NBS) or explicitly justified.
  - No network usage. Bulk delete is purely local database operations.
- [x] All I/O paths are async; UI work avoids blocking calls and uses reactive async command flow.
  - All delete commands return `Task<Result<…>>`. ViewModel commands use `ReactiveCommand.CreateFromTask`. Button disabled during execution to prevent double-submission.
- [x] Tests and coverage impact are defined for Domain (rule/state coverage) and Application (>=90%).
  - Domain: no changes → existing coverage maintained.
  - Application: new handler tests for both bulk-delete commands (empty list, partial IDs, all valid, cancellation). Target ≥90%.
  - Desktop: new ViewModel tests for selection state, toolbar reactivity, command execution flow.
- [x] Feature work is mapped to an approved spec task under `.specify/tasks/`.
  - Spec exists at `.specify/specs/025-bulk-delete-fillings-reports/spec.md`.

## Project Structure

### Documentation (this feature)

```text
specs/025-bulk-delete-fillings-reports/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
└── tasks.md             # Phase 2 output (created by /speckit.tasks)
```

### Source Code (repository root)

```text
src/
├── Rentier.Domain/                          # NO CHANGES for this feature
│
├── Rentier.Application/
│   ├── Commands/
│   │   ├── BulkDeleteFilingsCommand.cs      # NEW — record(IReadOnlyList<Guid> FilingIds)
│   │   └── BulkDeleteReportsCommand.cs      # NEW — record(IReadOnlyList<Guid> ReportIds)
│   ├── Handlers/
│   │   ├── BulkDeleteFilingsCommandHandler.cs   # NEW
│   │   └── BulkDeleteReportsCommandHandler.cs   # NEW
│   └── Repositories/
│       ├── IFilingRepository.cs             # MODIFIED — add DeleteManyAsync
│       └── IReportRepository.cs             # MODIFIED — add DeleteManyAsync
│
├── Rentier.Infrastructure/
│   └── Repositories/
│       ├── FilingRepository.cs              # MODIFIED — implement DeleteManyAsync
│       └── ReportRepository.cs              # MODIFIED — implement DeleteManyAsync
│
└── Rentier.Desktop/
    ├── ViewModels/
    │   ├── FilingsViewModel.cs              # MODIFIED — add selection + bulk delete
    │   ├── ReportsViewModel.cs              # MODIFIED — add selection + bulk delete
    │   ├── FilingRowViewModel.cs            # MODIFIED — add IsSelected property
    │   └── ReportRowViewModel.cs            # MODIFIED — add IsSelected property
    ├── Views/
    │   ├── FilingsView.axaml                # MODIFIED — add checkbox column + toolbar buttons
    │   └── ReportsView.axaml                # MODIFIED — add checkbox column + toolbar buttons
    ├── Composition/
    │   └── CompositionRoot.cs               # MODIFIED — register bulk delete handlers
    └── Resources/
        └── Strings.resx                     # MODIFIED — add bulk delete strings

tests/
├── Rentier.Application.Tests/
│   ├── BulkDeleteFilingsCommandHandlerTests.cs   # NEW
│   └── BulkDeleteReportsCommandHandlerTests.cs   # NEW
└── Rentier.Desktop.Tests/
    ├── FilingsViewModelBulkDeleteTests.cs         # NEW
    └── ReportsViewModelBulkDeleteTests.cs         # NEW
```

**Structure Decision**: Existing Clean Architecture 4-project layout is preserved. No new projects. New files follow established conventions — commands in `Commands/`, handlers in `Handlers/`, tests in per-project test assemblies.

## Complexity Tracking

> No Constitution Check violations. All changes align with existing architecture boundaries and patterns.
