# Implementation Plan: Reports — Icon-Only Action Column

**Branch**: `feat/027-031-ux-improvements` | **Date**: 2025-07-17 | **Spec**: [spec.md](../../.specify/specs/031-reports-icon-only-action-column/spec.md)
**Input**: Feature specification from `.specify/specs/031-reports-icon-only-action-column/spec.md`

## Summary

Convert the Reports DataGrid action buttons ("View Filings" and "Delete") from text-labelled buttons to icon-only buttons with tooltip discoverability. The delete button uses a red foreground for destructive-action signalling. This is a purely cosmetic Desktop-layer change — no ViewModel, command binding, Domain, Application, or Infrastructure changes are required. The implementation follows the icon-button pattern established by feature 030 (Filings Action Column Consolidation) and reuses its icon resources (TrashIcon) and resource dictionary infrastructure.

## Technical Context

**Language/Version**: C# 12 / .NET 8  
**Primary Dependencies**: Avalonia UI 11+ (FluentTheme), ReactiveUI, CommunityToolkit.Mvvm  
**Storage**: N/A (no data changes)  
**Testing**: xUnit + FluentAssertions + NSubstitute + Avalonia.Headless.XUnit  
**Target Platform**: Windows + macOS (desktop)  
**Project Type**: Desktop application (Avalonia UI)  
**Performance Goals**: N/A (cosmetic change, no new I/O)  
**Constraints**: Must follow feature 030 icon-button pattern exactly for cross-page visual consistency  
**Scale/Scope**: 1 AXAML view, 1 resource file, 1 headless test file; ~30 lines changed

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- [x] Clean Architecture boundary is preserved (`Desktop -> Application -> Domain`; Infrastructure implements Application contracts only).
  - *Only `Rentier.Desktop` view markup and string resources change. No Application or Domain code is touched. No new project references are added.*
- [x] All monetary/rate/percentage values are modeled as `decimal`.
  - *N/A — no monetary, rate, or percentage fields are introduced or modified.*
- [x] All business dates are modeled as `DateOnly`; boundary conversions are identified.
  - *N/A — no date fields are introduced or modified.*
- [x] Security/privacy constraints hold: local-first data, OS credential store for secrets, no telemetry.
  - *N/A — no data storage, credential handling, or network changes.*
- [x] External network usage is limited to approved endpoints (IMAP and NBS) or explicitly justified as a constitution amendment.
  - *N/A — no new network calls.*
- [x] All I/O paths are async; UI work avoids blocking calls and uses reactive async command flow.
  - *Existing `ReactiveCommand` bindings for `ViewFilingsCommand` and `DeleteCommand` are preserved unchanged. No new I/O introduced.*
- [x] Tests and coverage impact are defined for Domain (rule/state coverage) and Application (>=90%).
  - *No Domain or Application code changes. Desktop headless UI tests updated to verify icon-button rendering and tooltip binding. Existing ViewModel tests remain untouched.*
- [x] Feature work is mapped to an approved spec task under `.specify/tasks/`.
  - *Will be mapped when tasks.md is generated via `/speckit.tasks`.*

## Project Structure

### Documentation (this feature)

```text
specs/031-reports-icon-only-action-column/
├── plan.md              # This file
├── research.md          # Phase 0: icon approach, tooltip pattern, dependency on 030
├── data-model.md        # Phase 1: resource keys, AXAML structure
├── quickstart.md        # Phase 1: step-by-step implementation guide
└── tasks.md             # Phase 2 output (created by /speckit.tasks, not /speckit.plan)
```

### Source Code (repository root)

```text
src/Rentier.Desktop/
├── Resources/
│   ├── Icons.axaml              # [FROM 030] Shared icon StreamGeometry resources
│   ├── Strings.resx             # MODIFY: add tooltip string resources
│   └── Strings.Designer.cs      # AUTO-GENERATED from Strings.resx
├── Views/
│   └── ReportsView.axaml        # MODIFY: replace text buttons with icon buttons
└── App.axaml                    # [FROM 030] Already includes Icons.axaml resource

tests/Rentier.UnitTests/Desktop/
└── Views/
    └── ReportsViewHeadlessTests.cs  # MODIFY: add icon-button rendering tests
```

**Structure Decision**: Clean Architecture four-project layout. Only the `Rentier.Desktop` project and its headless test file are impacted. The icon resource dictionary (`Icons.axaml`) and its `App.axaml` inclusion are established by feature 030 — this feature adds one new icon resource to the existing dictionary and references it.

## Complexity Tracking

> No constitution violations. This feature is a purely cosmetic Desktop-layer change with zero architectural risk.
