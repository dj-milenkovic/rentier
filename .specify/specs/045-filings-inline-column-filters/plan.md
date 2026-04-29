# Implementation Plan: Filings Inline Column Filters

**Branch**: `045-filings-inline-column-filters` | **Date**: 2025-07-15 | **Spec**: `.specify/specs/045-filings-inline-column-filters/spec.md`  
**Input**: Feature specification from `.specify/specs/045-filings-inline-column-filters/spec.md`

## Summary

Add inline filter controls below column headers on the Filings DataGrid. Five columns are filterable: Status (dropdown), Income Type (dropdown), Payer (text search), Filing Deadline (date picker), and Payment Reference (text search). Filters combine with AND logic and are applied server-side through extended query parameters. Text inputs are debounced at 300ms. A "Clear filters" button resets all active filters. Navigation from the Reports page clears inline filters to guarantee target filing visibility.

## Technical Context

**Language/Version**: C# 12 / .NET 8  
**Primary Dependencies**: Avalonia UI 11+, ReactiveUI, System.Reactive, EF Core 8 (SQLite)  
**Storage**: SQLite via EF Core (existing — query extension only, no schema changes)  
**Testing**: xUnit + FluentAssertions + NSubstitute  
**Target Platform**: Windows + macOS desktop (cross-platform Avalonia)  
**Project Type**: Desktop application (Clean Architecture — 4 projects)  
**Performance Goals**: Filter results visible within 1 second of value change (SC-001)  
**Constraints**: Local-first, no network calls, reactive non-blocking UI  
**Scale/Scope**: ~5 filterable columns, server-side paginated queries (30 items/page)

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- [x] Clean Architecture boundary is preserved (`Desktop -> Application -> Domain`; Infrastructure implements Application contracts only).
  - ✅ Filter state lives in Desktop ViewModel. `FilingColumnFilter` record defined in Application (query parameter). Repository interface extended in Application, implemented in Infrastructure. No new cross-layer violations.
- [x] All monetary/rate/percentage values are modeled as `decimal`.
  - ✅ No monetary fields are filtered. `TaxPayableRsd` (decimal) is display-only. No new decimal fields introduced.
- [x] All business dates are modeled as `DateOnly`; boundary conversions are identified.
  - ✅ `FilingDeadline` filter uses `DateOnly` in `FilingColumnFilter` and repository queries. ViewModel converts from Avalonia's `DateTimeOffset?` at the Desktop→Application boundary.
- [x] Security/privacy constraints hold: local-first data, OS credential store for secrets, no telemetry.
  - ✅ No new data storage, no network calls, no credential handling. Filter state is ephemeral in-memory.
- [x] External network usage is limited to approved endpoints (IMAP and NBS) or explicitly justified.
  - ✅ No network usage. All filtering operates on local SQLite database.
- [x] All I/O paths are async; UI work avoids blocking calls and uses reactive async command flow.
  - ✅ `LoadPageAsync` is already async. Filter changes trigger it via reactive pipeline. Text debounce uses `Throttle(300ms)` on `RxApp.TaskpoolScheduler`.
- [x] Tests and coverage impact are defined for Domain (rule/state coverage) and Application (>=90%).
  - ✅ Domain: no changes, no test impact. Application: handler test for ColumnFilter forwarding. Infrastructure: integration tests for filtered queries. Desktop: ViewModel tests for filter state/debounce/clear/interaction.
- [x] Feature work is mapped to an approved spec task under `.specify/tasks/`.
  - ⏳ Tasks to be generated via `/speckit.tasks` after plan approval.

## Project Structure

### Documentation (this feature)

```text
.specify/specs/045-filings-inline-column-filters/
├── spec.md              # Feature specification
├── plan.md              # This file
├── research.md          # Phase 0: research decisions
├── data-model.md        # Phase 1: entity/type changes
├── quickstart.md        # Phase 1: build & change guide
├── contracts/
│   └── ui-filter-contract.md  # Phase 1: UI binding contract
└── tasks.md             # Phase 2: task breakdown (via /speckit.tasks)
```

### Source Code (repository root)

```text
src/
├── Rentier.Domain/                    # NO CHANGES
│   └── Enums/
│       ├── FilingStatus.cs            # Existing: Init, Filed, Paid
│       └── IncomeType.cs              # Existing: Dividend, Interest
│
├── Rentier.Application/
│   ├── Queries/
│   │   ├── FilingColumnFilter.cs      # NEW: column filter record
│   │   └── GetFilingsQuery.cs         # MODIFIED: add ColumnFilter parameter
│   ├── Handlers/
│   │   └── GetFilingsQueryHandler.cs  # MODIFIED: pass ColumnFilter to repository
│   └── Repositories/
│       └── IFilingRepository.cs       # MODIFIED: add columnFilter to GetPagedAsync
│
├── Rentier.Infrastructure/
│   └── Repositories/
│       └── FilingRepository.cs        # MODIFIED: apply WHERE clauses from ColumnFilter
│
└── Rentier.Desktop/
    ├── ViewModels/
    │   └── FilingsViewModel.cs        # MODIFIED: filter properties + reactive pipeline
    ├── Views/
    │   └── FilingsView.axaml          # MODIFIED: add filter row panel
    └── Resources/
        └── Strings.resx               # MODIFIED: add filter-related strings

tests/
├── Rentier.Application.Tests/
│   └── Handlers/
│       └── GetFilingsQueryHandlerTests.cs  # MODIFIED: ColumnFilter forwarding tests
├── Rentier.Infrastructure.Tests/
│   └── Repositories/
│       └── FilingRepositoryTests.cs        # MODIFIED: filtered query tests
└── Rentier.Desktop.Tests/
    └── ViewModels/
        └── FilingsViewModelTests.cs        # MODIFIED: filter state + interaction tests
```

**Structure Decision**: Existing Clean Architecture 4-project structure. No new projects needed. Changes are distributed across Application (query contract), Infrastructure (query implementation), and Desktop (ViewModel + View). Domain layer is untouched.

## Complexity Tracking

> No constitution violations. No complexity justifications needed.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| None | — | — |

## Design Summary

### Layer-by-Layer Changes

#### Application Layer

1. **`FilingColumnFilter`** (new record) — Immutable record carrying optional filter values for each column (Status?, IncomeType?, PayingEntity string, FilingDeadline DateOnly?, PaymentReference string). All nullable — null means "no filter".

2. **`GetFilingsQuery`** — Add `FilingColumnFilter? ColumnFilter = null` as final parameter. Backward compatible (default null).

3. **`GetFilingsQueryHandler`** — When `ReportIdFilter` is not set, pass `query.ColumnFilter` to `_filings.GetPagedAsync()`.

4. **`IFilingRepository.GetPagedAsync`** — Add `FilingColumnFilter? columnFilter = null` parameter. Backward compatible.

#### Infrastructure Layer

5. **`FilingRepository.GetPagedAsync`** — Chain `Where()` clauses for each non-null field in `columnFilter`. Applied after the existing `FilingFilterMode` filter (Unpaid/All) and before pagination (skip/take). Uses `EF.Functions.Like` for text contains matching.

#### Desktop Layer

6. **`FilingsViewModel`** — Add 5 filter properties, `HasActiveFilters` derived property, `IsFilterRowEnabled` derived property, `ClearFiltersCommand`. Reactive pipeline merges filter changes (instant for dropdowns/date, throttled for text) into page reload. Setting `ReportIdFilter` clears all inline filters. All filter changes reset page to 1.

7. **`FilingsView.axaml`** — Add a `Grid`-based filter row between the column header section and the DataGrid. Contains ComboBoxes (Status, IncomeType), TextBoxes (Payer, PaymentRef), CalendarDatePicker (Deadline), and Clear button. Visually aligned with DataGrid columns.

8. **`Strings.resx`** — Add keys: `Filter_All`, `Filter_Placeholder`, `Filter_ClearAll`, `Filter_NoResults`, `Filter_StatusInit`, `Filter_StatusFiled`, `Filter_StatusPaid`, `Filter_IncomeDividend`, `Filter_IncomeInterest`.

### Key Interaction Flows

```
User types in text filter
  → ViewModel property change (FilterPayingEntity)
  → Throttle(300ms, TaskpoolScheduler)
  → Reset CurrentPage to 1
  → LoadPageCommand.Execute()
  → GetFilingsQuery(ColumnFilter: new FilingColumnFilter(PayingEntity: "..."))
  → GetFilingsQueryHandler → IFilingRepository.GetPagedAsync(columnFilter: ...)
  → FilingRepository applies WHERE f.PayingEntity LIKE '%...%'
  → Results returned → ViewModel.Rows updated → View refreshes

User navigates from Reports page
  → MainWindowViewModel sets FilingsViewModel.ReportIdFilter = guid
  → ReportIdFilter setter clears all inline filter properties
  → LoadPageAsync bypasses pagination (existing behavior)
  → Filter row controls disabled (IsFilterRowEnabled = false)

User clicks Clear Filters
  → ClearFiltersCommand resets all 5 filter properties to null
  → Reactive pipeline detects changes → single LoadPageCommand execution
  → Full unfiltered page displayed
```

### Testing Strategy

| Layer | Test Scope | Count Est. |
|-------|-----------|------------|
| Application | Handler passes ColumnFilter to repository mock | 2-3 tests |
| Infrastructure | EF Core queries with various filter combinations | 6-8 tests |
| Desktop VM | Filter state, debounce, clear, ReportIdFilter interaction, HasActiveFilters | 8-12 tests |

**Total estimated new tests**: 16-23
