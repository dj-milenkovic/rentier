# Implementation Plan: 050 — Filings Filter Header Flyouts

**Branch**: `050-filings-filter-flyouts` | **Date**: 2025-07-15 | **Spec**: `.specify/specs/050-filings-filter-header-flyouts/spec.md`
**Input**: Feature specification — replace inline filter row with Excel-style column header filter flyouts

## Summary

Replace the existing inline filter row (feature 045) with Excel-style flyout popups anchored in DataGrid column headers. Each filterable column (Status, Income Type, Paying Entity, Deadline, Payment Reference) gets a funnel icon next to the existing sort arrow. Clicking the funnel opens a `Popup` with either checkbox list (enum columns) or text search (text/date columns). The existing backend filtering pipeline (`FilingColumnFilter` → query handler → repository WHERE clauses) is extended minimally to support multi-select enums and text-based deadline search.

## Technical Context

**Language/Version**: C# 12, .NET 8  
**Primary Dependencies**: Avalonia UI 11+, ReactiveUI, FluentTheme  
**Storage**: SQLite via EF Core 8 (local-first)  
**Testing**: xUnit + FluentAssertions + NSubstitute  
**Target Platform**: Windows + macOS desktop (cross-platform)  
**Project Type**: Desktop application (Avalonia MVVM)  
**Performance Goals**: Filter apply → table reload < 200ms perceived  
**Constraints**: Local-first, no network calls, no telemetry  
**Scale/Scope**: Single Filings page, 5 filterable columns, ~3 new ViewModel classes

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- [x] Clean Architecture boundary is preserved (`Desktop -> Application -> Domain`; Infrastructure implements Application contracts only).
  - Desktop: new flyout ViewModels + AXAML changes
  - Application: additive fields on `FilingColumnFilter` record (no new interfaces)
  - Infrastructure: 3 additional WHERE clause lines in existing `FilingRepository.GetPagedAsync`
  - Domain: **no changes**
- [x] All monetary/rate/percentage values are modeled as `decimal`.
  - No monetary values involved in this feature (filtering only — `TaxPayable` column has no filter)
- [x] All business dates are modeled as `DateOnly`; boundary conversions are identified.
  - `FilingDeadline` remains `DateOnly` in Domain/Application/Infrastructure
  - ViewModel `FilterDeadline` changes from `DateTimeOffset?` to `string?` (UI boundary)
  - Text-based deadline filter uses `LIKE` on SQLite's ISO text representation — no `DateOnly` conversion needed
- [x] Security/privacy constraints hold: local-first data, OS credential store for secrets, no telemetry.
  - Purely UI changes, no network or credential access
- [x] External network usage is limited to approved endpoints (IMAP and NBS) or explicitly justified.
  - No network calls in this feature
- [x] All I/O paths are async; UI work avoids blocking calls and uses reactive async command flow.
  - Flyout ViewModels are synchronous state holders (no I/O)
  - Apply triggers existing `LoadPageCommand` which is `ReactiveCommand.CreateFromTask`
  - Filter-to-reload pipeline already uses `InvokeCommand` pattern with proper scheduling
- [x] Tests and coverage impact are defined for Domain (rule/state coverage) and Application (>=90%).
  - Domain: no changes → no new tests needed
  - Application: `FilingColumnFilter` is a record with no logic → covered by existing query handler tests
  - Desktop: new unit tests for `EnumFilterFlyoutViewModel<T>` and `TextFilterFlyoutViewModel` (state transitions, Apply/dismiss/SelectAll/Clear)
  - Infrastructure: integration test for multi-select WHERE and deadline text LIKE
- [x] Feature work is mapped to an approved spec task under `.specify/tasks/`.
  - Spec: `.specify/specs/050-filings-filter-header-flyouts/spec.md`

## Constitution Re-Check (Post-Design)

- [x] `FilingColumnFilter` extension is additive (new optional fields with `= null` defaults) — no breaking change to existing callers
- [x] Repository WHERE clause additions follow existing pattern (`if (field is not null) query = query.Where(...)`)
- [x] Flyout ViewModels are pure Desktop-layer classes — no Domain or Application leakage
- [x] `FilterDeadline` type change from `DateTimeOffset?` to `string?` is a UI-boundary concern only — `DateOnly` preserved in Application/Infrastructure layers
- [x] No new `decimal` or `Money` values introduced

## Project Structure

### Documentation (this feature)

```text
specs/050-filings-filter-flyouts/
├── plan.md              # This file
├── research.md          # Phase 0: design decisions and alternatives
├── data-model.md        # Phase 1: entity changes and new ViewModel models
├── quickstart.md        # Phase 1: implementation overview and file map
├── contracts/
│   └── ui-contracts.md  # Phase 1: flyout interaction and binding contracts
└── tasks.md             # Phase 2 output (created by /speckit.tasks)
```

### Source Code (repository root)

```text
src/
├── Rentier.Application/
│   └── Queries/
│       └── FilingColumnFilter.cs          # MODIFIED: +3 optional fields (Statuses, IncomeTypes, FilingDeadlineText)
├── Rentier.Infrastructure/
│   └── Repositories/
│       └── FilingRepository.cs            # MODIFIED: +3 WHERE clauses for multi-select/text fields
└── Rentier.Desktop/
    ├── Assets/
    │   └── Icons.axaml                    # MODIFIED: +FilterIcon StreamGeometry
    ├── Converters/
    │   └── FilterActiveConverter.cs       # NEW: bool → Brush (active/inactive funnel icon)
    ├── Models/
    │   └── CheckableItem.cs               # NEW: generic checkbox item for enum flyouts
    ├── Resources/
    │   └── Strings.resx                   # MODIFIED: +4 keys (Filter_Search, Filter_Apply, Filter_SelectAll, Filter_Clear)
    ├── ViewModels/
    │   ├── EnumFilterFlyoutViewModel.cs   # NEW: generic enum checkbox flyout state
    │   ├── TextFilterFlyoutViewModel.cs   # NEW: text search flyout state
    │   └── FilingsViewModel.cs            # MODIFIED: add flyout VMs, change FilterDeadline type, update query mapping
    └── Views/
        └── FilingsView.axaml              # MODIFIED: remove filter row, add funnel+Popup in column headers

tests/
└── Rentier.UnitTests/
    └── Desktop/
        ├── EnumFilterFlyoutViewModelTests.cs  # NEW
        ├── TextFilterFlyoutViewModelTests.cs  # NEW
        └── FilingsViewModelTests.cs           # MODIFIED: update filter-related tests
```

**Structure Decision**: Existing Clean Architecture 4-project layout. Changes span Desktop (primary), Application (minimal extension), and Infrastructure (minimal WHERE clauses). No new projects.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| `FilingColumnFilter` extension (adds 3 fields) | Multi-select enum filters require `IReadOnlySet<T>` — single `FilingStatus?` cannot represent "Init OR Filed" | Client-side post-filtering is incompatible with server-side pagination |
| `FilterDeadline` type change (`DateTimeOffset?` → `string?`) | Text-based deadline search on formatted strings per spec FR-006 | CalendarDatePicker in flyout violates spec; keeping DateTimeOffset prevents partial-date search like "2025-07" |
