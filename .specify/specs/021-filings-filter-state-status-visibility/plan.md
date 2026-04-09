# Implementation Plan: Filings Page Filter State & Status Visibility

**Branch**: `feature/021-024-qa-fixes` | **Date**: 2025-07-15 | **Spec**: `.specify/specs/021-filings-filter-state-status-visibility/spec.md`
**Input**: Feature specification from `.specify/specs/021-filings-filter-state-status-visibility/spec.md`

## Summary

Fix two QA-reported usability issues on the Filings page: (1) add visible active-state styling to the "All" / "Unpaid" filter ToggleButtons using scoped `:checked` pseudo-class styles with FluentTheme accent colours, and (2) add a read-only colour-coded status badge (pill) on each filing row — amber (#D4A017) for Init, blue (#0063B1) for Filed, green (#107C10) for Paid — rendered as a `Border` + `TextBlock` via a new `FilingStatusToBadgeBrushConverter`. All changes are confined to the Desktop presentation layer; no Domain, Application, or Infrastructure modifications.

## Technical Context

**Language/Version**: C# 12, .NET 8  
**Primary Dependencies**: Avalonia UI 11+ (FluentTheme), ReactiveUI, CommunityToolkit.Mvvm  
**Storage**: N/A — no storage changes; existing SQLite via EF Core is untouched  
**Testing**: xUnit + FluentAssertions + NSubstitute (`dotnet test`)  
**Target Platform**: Windows + macOS (cross-platform desktop via Avalonia)  
**Project Type**: Desktop application (Clean Architecture, MVVM)  
**Performance Goals**: N/A — presentation-only changes; existing 60fps UI responsiveness maintained  
**Constraints**: Presentation layer only (CA-001); no Domain/Application/Infrastructure changes; WCAG AA colour contrast (4.5:1 minimum)  
**Scale/Scope**: 2 modified files, 2 new files, 2 new test files; ~150 LOC net addition

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- [x] Clean Architecture boundary is preserved (`Desktop -> Application -> Domain`; Infrastructure implements Application contracts only).
  - ✅ All changes confined to `Rentier.Desktop` (Views, ViewModels, Converters). No modifications to Domain, Application, or Infrastructure projects. The new `FilingStatusToBadgeBrushConverter` lives in `Rentier.Desktop/Converters/`. The new `StatusDisplayText` property on `FilingRowViewModel` delegates to the existing `FilingStatusExtensions.ToDisplayString()` which lives in Desktop/Extensions.
- [x] All monetary/rate/percentage values are modeled as `decimal`.
  - ✅ No monetary or rate values added or modified. Existing `TaxPayable` (decimal) display remains unchanged.
- [x] All business dates are modeled as `DateOnly`; boundary conversions are identified.
  - ✅ No date values added or modified. Existing `FilingDeadline` (DateOnly) display remains unchanged.
- [x] Security/privacy constraints hold: local-first data, OS credential store for secrets, no telemetry.
  - ✅ No data storage changes. No new user data collected, persisted, or transmitted. All changes are purely visual rendering of existing in-memory data.
- [x] External network usage is limited to approved endpoints (IMAP and NBS) or explicitly justified as a constitution amendment.
  - ✅ No network calls added. Feature is entirely offline presentation logic.
- [x] All I/O paths are async; UI work avoids blocking calls and uses reactive async command flow.
  - ✅ No new I/O operations. Badge rendering is synchronous property binding. Filter toggle already triggers async `LoadPageCommand` via existing reactive pipeline.
- [x] Tests and coverage impact are defined for Domain (rule/state coverage) and Application (>=90%).
  - ✅ No Domain or Application changes → no coverage impact on those layers. Two new Desktop test files: `FilingRowViewModelTests.cs` (ViewModel property tests) and `FilingStatusToBadgeBrushConverterTests.cs` (converter colour mapping tests).
- [x] Feature work is mapped to an approved spec task under `.specify/tasks/`.
  - ✅ Feature spec exists at `.specify/specs/021-filings-filter-state-status-visibility/spec.md`. Tasks defined in `tasks.md` (T001–T012).

## Project Structure

### Documentation (this feature)

```text
.specify/specs/021-filings-filter-state-status-visibility/
├── plan.md              # This file
├── spec.md              # Feature specification
├── research.md          # Phase 0 output — styling/converter research
├── data-model.md        # Phase 1 output — ViewModel + converter model
├── quickstart.md        # Phase 1 output — build/run/verify guide
├── contracts/
│   └── application-contracts.md  # Presentation-layer interface contracts
└── tasks.md             # Implementation tasks (T001–T012)
```

### Source Code (repository root)

```text
src/
├── Rentier.Desktop/
│   ├── Converters/
│   │   ├── FilingStatusDisplayConverter.cs      # EXISTING — reused for badge text
│   │   ├── FilingStatusToBadgeBrushConverter.cs  # NEW — Status → IBrush for badge bg/fg
│   │   ├── InvertBoolConverter.cs                # EXISTING — used by filter ToggleButtons
│   │   └── ...                                   # Other existing converters (unchanged)
│   ├── Extensions/
│   │   └── FilingStatusExtensions.cs             # EXISTING — ToDisplayString() reused
│   ├── ViewModels/
│   │   ├── FilingsViewModel.cs                   # EXISTING — ShowAll/IsLoading (unchanged)
│   │   └── FilingRowViewModel.cs                 # MODIFIED — add StatusDisplayText property
│   └── Views/
│       └── FilingsView.axaml                     # MODIFIED — ToggleButton styles + badge column

tests/
├── Rentier.Desktop.Tests/
│   ├── Converters/
│   │   └── FilingStatusToBadgeBrushConverterTests.cs  # NEW — colour mapping tests
│   ├── ViewModels/
│   │   └── FilingRowViewModelTests.cs                  # NEW — StatusDisplayText tests
│   └── FilingsViewModelTests.cs                        # MODIFIED — add filter toggle tests
```

**Structure Decision**: Clean Architecture with four projects under `src/` (Domain, Application, Infrastructure, Desktop) and matching test projects under `tests/`. This feature touches only `Rentier.Desktop` (source) and `Rentier.Desktop.Tests` (tests), consistent with the presentation-only scope.

## Complexity Tracking

> No Constitution Check violations. All changes are within the Desktop presentation layer, using existing Avalonia primitives and converter patterns already established in the codebase.

*No entries — all gates pass without exception.*
