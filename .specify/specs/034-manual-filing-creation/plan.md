# Implementation Plan: Manual Filing Creation (034)

**Branch**: `feature/032-033-034-column-xml-manual` | **Date**: 2025-07-22 | **Spec**: `.specify/specs/034-manual-filing-creation/spec.md`
**Input**: Feature specification from `.specify/specs/034-manual-filing-creation/spec.md`

---

## Summary

Add a manual filing creation flow allowing users to create PP-OPO tax filings without
importing a brokerage statement. The user fills a form (ticker, income type, date, currency,
gross amount, optional net received), triggers calculation (exchange rate fetch + tax
computation + deadline calculation), reviews a preview, and saves. This feature reuses all
existing domain services (`TaxCalculationService`, `FilingDeadlineCalculator`,
`ExchangeRateResolver`) and the `Filing.CreateFromIncome()` factory. New code is limited to
two Application command handlers and one Desktop ViewModel+View.

## Technical Context

**Language/Version**: C# 12 / .NET 8
**Primary Dependencies**: Avalonia UI 11+ (FluentTheme), ReactiveUI, CommunityToolkit.Mvvm, EF Core 8 (SQLite), Microsoft.Extensions.DependencyInjection
**Storage**: SQLite (local file, `rentier.db`)
**Testing**: xUnit + FluentAssertions + NSubstitute
**Target Platform**: Windows + macOS (cross-platform desktop via Avalonia)
**Project Type**: Desktop application (Clean Architecture, 4 projects)
**Performance Goals**: Filing creation < 60 seconds including NBS rate fetch (1–5s network)
**Constraints**: Offline-capable for cached rates; single-user; all monetary values `decimal`; all dates `DateOnly`
**Scale/Scope**: Single form view, 2 new command handlers, 1 new ViewModel

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- [x] Clean Architecture boundary is preserved (`Desktop -> Application -> Domain`; Infrastructure implements Application contracts only).
  - ManualFilingViewModel (Desktop) → CalculateManualFilingCommandHandler / CreateManualFilingCommandHandler (Application) → TaxCalculationService, FilingDeadlineCalculator (Domain). No upward references.
- [x] All monetary/rate/percentage values are modeled as `decimal`.
  - GrossAmount, NetReceived, all RSD values, ExchangeRate.RateToRsd — all `decimal`. No float/double anywhere.
- [x] All business dates are modeled as `DateOnly`; boundary conversions are identified.
  - IncomeDate, FilingDeadline, ExchangeRateSourceDate — all `DateOnly`. Avalonia DatePicker returns `DateTimeOffset?` — converted to `DateOnly` at ViewModel boundary.
- [x] Security/privacy constraints hold: local-first data, OS credential store for secrets, no telemetry.
  - All data stored locally in SQLite. No credentials involved. No telemetry.
- [x] External network usage is limited to approved endpoints (IMAP and NBS) or explicitly justified as a constitution amendment.
  - Only outbound call is NBS exchange rate fetch — already in approved list.
- [x] All I/O paths are async; UI work avoids blocking calls and uses reactive async command flow.
  - CalculateCommand and SaveCommand use `ReactiveCommand.CreateFromTask`. All handler methods are `async Task`. Loading indicator shown during async ops.
- [x] Tests and coverage impact are defined for Domain (rule/state coverage) and Application (>=90%).
  - Domain: no new logic — existing tests cover TaxCalculationService and FilingDeadlineCalculator.
  - Application: unit tests for both new handlers covering happy path, validation failures, duplicate detection, rate failure.
  - Desktop: ViewModel tests for command enablement, preview state, error display, navigation.
- [x] Feature work is mapped to an approved spec task under `.specify/tasks/`.
  - Spec at `.specify/specs/034-manual-filing-creation/spec.md`.

## Project Structure

### Documentation (this feature)

```text
.specify/specs/034-manual-filing-creation/
├── spec.md              # Feature specification (input)
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/
│   └── ui-contract.md   # Phase 1 output — UI layout and localization contract
└── tasks.md             # Phase 2 output (/speckit.tasks command)
```

### Source Code (repository root)

```text
src/
├── Rentier.Domain/                    # No changes — all types exist
│   ├── Entities/Filing.cs             # Reused: CreateFromIncome() factory
│   ├── Services/TaxCalculationService.cs       # Reused
│   ├── Services/FilingDeadlineCalculator.cs    # Reused
│   ├── ValueObjects/ExchangeRate.cs            # Reused
│   ├── ValueObjects/FilingInfo.cs              # Reused
│   ├── ValueObjects/HolidayConf.cs             # Reused
│   └── Enums/ExchangeRateSourceType.cs         # Reused
│
├── Rentier.Application/               # 2 new handlers + 3 new records
│   ├── Commands/
│   │   ├── CalculateManualFilingCommand.cs     # NEW
│   │   └── CreateManualFilingCommand.cs        # NEW
│   ├── DTOs/
│   │   └── ManualFilingPreviewDto.cs           # NEW
│   ├── Handlers/
│   │   ├── CalculateManualFilingCommandHandler.cs  # NEW
│   │   └── CreateManualFilingCommandHandler.cs     # NEW
│   └── Services/ExchangeRateResolver.cs        # Reused (no changes)
│
├── Rentier.Infrastructure/            # No new code — existing repos reused
│   └── InfrastructureServiceExtensions.cs  # MODIFIED: register new handlers
│
└── Rentier.Desktop/                   # 1 new ViewModel + 1 new View
    ├── ViewModels/
    │   └── ManualFilingViewModel.cs            # NEW
    ├── Views/
    │   ├── ManualFilingView.axaml              # NEW
    │   └── ManualFilingView.axaml.cs           # NEW
    ├── Resources/Strings.resx                  # MODIFIED: add ManualFiling_* keys
    ├── Composition/CompositionRoot.cs          # MODIFIED: register ViewModel + handlers
    └── ViewModels/
        ├── MainWindowViewModel.cs              # MODIFIED: wire navigation delegate
        └── FilingsViewModel.cs                 # MODIFIED: add NewFilingCommand

tests/
├── Rentier.Application.Tests/
│   ├── CalculateManualFilingCommandHandlerTests.cs  # NEW
│   └── CreateManualFilingCommandHandlerTests.cs     # NEW
└── Rentier.Desktop.Tests/
    └── ManualFilingViewModelTests.cs                # NEW
```

**Structure Decision**: The existing 4-project Clean Architecture structure
(Domain → Application → Infrastructure ← Desktop) is preserved. No new projects needed.
All new code fits cleanly into existing project boundaries.

## Complexity Tracking

> No constitution violations. All gates pass.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| *None* | — | — |
