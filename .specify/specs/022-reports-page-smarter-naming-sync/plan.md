# Implementation Plan: Reports Page – Smarter Naming and Sync Clarification

**Branch**: `feature/021-024-qa-fixes` | **Date**: 2025-07-09 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `.specify/specs/022-reports-page-smarter-naming-sync/spec.md`

## Summary

Replace raw file-path report names on the Reports page with friendly display names following the pattern **"\<ImporterDisplayName\> – \<EarliestIncomeDate\>"** (en dash U+2013), and add a sync clarification subtitle near the "Sync Mailboxes" button. Display names are computed at query time in `GetReportsQueryHandler` via a new `IFilingRepository.GetEarliestIncomeDateByReportIdAsync` method — no schema changes required. The original file name is preserved in a tooltip. All new user-visible text is sourced from `Strings.resx`.

## Technical Context

**Language/Version**: C# 12 / .NET 8  
**Primary Dependencies**: Avalonia UI 11+ (FluentTheme), ReactiveUI, EF Core 8 (SQLite), CommunityToolkit.Mvvm  
**Storage**: SQLite (local, existing schema — no migrations needed)  
**Testing**: xUnit + FluentAssertions + NSubstitute  
**Target Platform**: Windows + macOS (cross-platform desktop via Avalonia)  
**Project Type**: Desktop application (Clean Architecture, CQRS)  
**Performance Goals**: Reports page loads within existing UX budget; new `MIN(IncomeDate)` query adds one scalar SQL per report row (< 1ms each on indexed `ReportId` FK)  
**Constraints**: Offline-capable (local-first), no telemetry, no cloud sync  
**Scale/Scope**: Typical user has 1–50 reports; display name derivation is O(n) with n = report count

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- [x] Clean Architecture boundary is preserved (`Desktop -> Application -> Domain`; Infrastructure implements Application contracts only).
  - Application defines `IFilingRepository` contract with new method → Infrastructure implements via EF Core → Desktop consumes `ReportRowDto` through ViewModel. No Domain changes needed.
- [x] All monetary/rate/percentage values are modeled as `decimal`.
  - No monetary fields introduced or modified. Existing `decimal` fields in Filing are untouched.
- [x] All business dates are modeled as `DateOnly`; boundary conversions are identified.
  - `EarliestIncomeDate` is `DateOnly?` (from `Filing.IncomeDate`). `Report.ImportDate` is `DateOnly`. Formatted as `yyyy-MM-dd` at the Application layer in the display name string.
- [x] Security/privacy constraints hold: local-first data, OS credential store for secrets, no telemetry.
  - No new data stored; display name is derived at query time. All data remains local-only.
- [x] External network usage is limited to approved endpoints (IMAP and NBS) or explicitly justified as a constitution amendment.
  - No network calls introduced. Sync clarification text is purely informational UI — it does not change sync behaviour.
- [x] All I/O paths are async; UI work avoids blocking calls and uses reactive async command flow.
  - New `GetEarliestIncomeDateByReportIdAsync` returns `Task<DateOnly?>`. The query handler already operates asynchronously. No blocking UI operations introduced.
- [x] Tests and coverage impact are defined for Domain (rule/state coverage) and Application (>=90%).
  - No Domain changes → Domain coverage unaffected. Application tests add 3 new display-name test cases + update 1 existing mapping test. Target: maintain ≥90% Application coverage.
- [x] Feature work is mapped to an approved spec task under `.specify/tasks/`.
  - Tasks T001–T018 defined in `.specify/specs/022-reports-page-smarter-naming-sync/tasks.md`.

**Pre-design gate**: ✅ PASS (all 8 checks satisfied, 0 violations)

## Project Structure

### Documentation (this feature)

```text
.specify/specs/022-reports-page-smarter-naming-sync/
├── plan.md              # This file
├── spec.md              # Feature specification (approved)
├── research.md          # Phase 0: 7 research decisions (all resolved)
├── data-model.md        # Phase 1: Entity survey, DTO changes, new repo method
├── quickstart.md        # Phase 1: Implementation order + build commands
├── contracts/
│   └── ui-reports-page.md  # Phase 1: 4 UI contracts (DTO mapping, grid columns, sync text, display name format)
├── checklists/
│   └── requirements.md  # Spec quality checklist (16/16 passed)
└── tasks.md             # 18 tasks across 6 phases
```

### Source Code (repository root)

```text
src/
├── Rentier.Domain/                    # Innermost — no changes for this feature
├── Rentier.Application/
│   ├── Repositories/
│   │   └── IFilingRepository.cs       # ← ADD GetEarliestIncomeDateByReportIdAsync
│   ├── DTOs/
│   │   └── ReportRowDto.cs            # ← ADD DisplayName, EarliestIncomeDate params
│   └── Handlers/
│       └── GetReportsQueryHandler.cs  # ← MODIFY HandleAsync: derive display name
├── Rentier.Infrastructure/
│   └── Repositories/
│       └── FilingRepository.cs        # ← IMPLEMENT GetEarliestIncomeDateByReportIdAsync
└── Rentier.Desktop/
    ├── ViewModels/
    │   ├── ReportsViewModel.cs        # (no changes expected)
    │   └── ReportRowViewModel.cs      # ← ADD DisplayName property, map from DTO
    ├── Views/
    │   ├── ReportsView.axaml          # ← MODIFY column 1 → DataGridTemplateColumn + tooltip; ADD sync subtitle
    │   └── ReportsView.axaml.cs       # (no changes expected)
    └── Resources/
        ├── Strings.resx               # ← ADD Reports_Sync_Subtitle, Reports_Col_DisplayName
        └── Strings.Designer.cs        # ← REGENERATE

tests/
├── Rentier.Application.Tests/
│   └── GetReportsQueryHandlerTests.cs # ← ADD 3 new tests, UPDATE 1 existing test
├── Rentier.Domain.Tests/              # (no changes)
├── Rentier.Infrastructure.Tests/      # (no changes)
├── Rentier.Desktop.Tests/             # (no changes)
└── Rentier.Tests.Common/              # (no changes)
```

**Structure Decision**: Existing Clean Architecture four-project layout. This feature modifies files across Application, Infrastructure, and Desktop layers plus Application tests. No new projects, packages, or database migrations required.

## Complexity Tracking

> No constitution violations. All 5 principles pass without exceptions.

*No entries — no violations to justify.*

## Post-Design Constitution Re-Check

*Re-evaluated after Phase 1 design artifacts (data-model.md, contracts/, research.md) are complete.*

- [x] **I. Clean Architecture Dependency Rule**: Confirmed. `IFilingRepository` (Application) → `FilingRepository` (Infrastructure). `ReportRowDto` (Application) → `ReportRowViewModel` (Desktop). No Domain changes. No reverse dependencies.
- [x] **II. Local-First Security and Privacy**: Confirmed. No new data storage, no network calls, no credentials involved.
- [x] **III. Financial and Temporal Correctness**: Confirmed. `EarliestIncomeDate` is `DateOnly?`. `ImportDate` is `DateOnly`. No monetary fields touched. No `double`/`float` usage.
- [x] **IV. Async and UI Responsiveness**: Confirmed. `GetEarliestIncomeDateByReportIdAsync` returns `Task<DateOnly?>`. Handler is already async. No `.Result` or `.Wait()`.
- [x] **V. Specification-Driven Quality Gates**: Confirmed. Feature has approved spec, 18 tasks in tasks.md, 3 new unit tests + 1 updated test for Application layer coverage.

**Post-design gate**: ✅ PASS (all 5 principles verified against concrete design artifacts)
