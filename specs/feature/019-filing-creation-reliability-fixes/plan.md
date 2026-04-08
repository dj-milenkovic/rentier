# Implementation Plan: Filing Creation Reliability Fixes

**Branch**: `feature/019-filing-creation-reliability-fixes` | **Date**: 2025-07-16 | **Spec**: [spec.md](../../.specify/specs/019-filing-creation-reliability-fixes/spec.md)  
**Input**: Feature specification from `.specify/specs/019-filing-creation-reliability-fixes/spec.md`

## Summary

Fix filing generation failures caused by missing NBS exchange rates on weekends and Serbian public holidays. The solution introduces a layered rate resolution pipeline: a Domain-level business day resolver walks backward from income dates to find valid rate dates; an Application-level exchange rate resolver orchestrates date fallback with provenance tracking; an Infrastructure-level composite fetcher chains the existing NBS ASMX service with a new HTML web scraper fallback. Report processing becomes partial-success-aware, creating filings for every resolvable event while recording structured errors for failures.

## Technical Context

**Language/Version**: C# 12 / .NET 8.0  
**Primary Dependencies**: EF Core 8 (SQLite), AngleSharp 1.*, MailKit 4.*, ReactiveUI 20.*, Avalonia 11.*  
**Storage**: SQLite via EF Core (local-first, single-file DB)  
**Testing**: xUnit + FluentAssertions + NSubstitute  
**Target Platform**: Windows + macOS (cross-platform desktop)  
**Project Type**: Desktop application (Avalonia UI, Clean Architecture)  
**Performance Goals**: Rate resolution should complete within 5s per event (including up to 10 fallback network calls in worst case)  
**Constraints**: Local-first data, no cloud, no telemetry. Network limited to NBS endpoints + IMAP.  
**Scale/Scope**: Single-user desktop app; typical reports have 5-50 income events per processing batch.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- [x] **Clean Architecture boundary preserved**: `BusinessDayResolver` is Domain (static, pure). `ExchangeRateResolver` is Application (orchestrates fetcher + domain logic). `NbsWebScraper` and `CompositeExchangeRateFetcher` are Infrastructure (implements `IExchangeRateFetcher`). `ProcessReportsCommandHandler` stays in Application. Desktop calls use cases only.
- [x] **All monetary/rate values use `decimal`**: Exchange rates, middle rates, buying/selling rates — all `decimal`. No `double`/`float` introduced.
- [x] **All business dates use `DateOnly`**: `ExchangeRateSourceDate` on Filing is `DateOnly?`. All date parameters in `BusinessDayResolver` and `ExchangeRateResolver` are `DateOnly`. `HolidayConf.Holidays` is `IReadOnlyList<DateOnly>`.
- [x] **Security/privacy constraints hold**: No new personal data stored. NBS exchange rates are public data. Storage remains local SQLite. No credentials needed for NBS web app (public endpoint).
- [x] **External network limited to approved endpoints**: New endpoint `webappcenter.nbs.rs` is an official NBS (National Bank of Serbia) endpoint, within the approved NBS network scope defined in CA-004 of the spec.
- [x] **All I/O paths are async**: `NbsWebScraper.FetchRateAsync()`, `CompositeExchangeRateFetcher.FetchRateAsync()`, `ExchangeRateResolver.ResolveAsync()` — all `async Task`. No `.Result` or `.Wait()`.
- [x] **Tests and coverage defined**: Domain: 100% coverage for `BusinessDayResolver` (all day types, holiday combos, edge cases). Application: >=90% for `ExchangeRateResolver` and updated `ProcessReportsCommandHandler`. Infrastructure: integration tests for `NbsWebScraper` HTML parsing.
- [x] **Feature mapped to spec**: Spec at `.specify/specs/019-filing-creation-reliability-fixes/spec.md`, status Draft.

## Project Structure

### Documentation (this feature)

```text
specs/feature/019-filing-creation-reliability-fixes/
├── plan.md                              # This file
├── research.md                          # Phase 0: research findings
├── data-model.md                        # Phase 1: schema changes
├── analysis.md                          # Root cause analysis
├── quickstart.md                        # Developer onboarding guide
├── contracts/
│   └── application-contracts.md         # Phase 1: interface contracts
└── tasks.md                             # Phase 2 output (not created by plan)
```

### Source Code (repository root)

```text
src/
├── Rentier.Domain/
│   ├── Entities/
│   │   └── Filing.cs                        # MODIFIED: +ExchangeRateSourceDate, +ExchangeRateSourceType
│   ├── Enums/
│   │   ├── ReportStatus.cs                  # MODIFIED: +PartialError = 3
│   │   └── ExchangeRateSourceType.cs        # NEW: Exact = 0, Fallback = 1
│   ├── Services/
│   │   ├── BusinessDayResolver.cs           # NEW: backward business day walk
│   │   ├── FilingDeadlineCalculator.cs      # EXISTING: forward business day walk
│   │   └── TaxCalculationService.cs         # EXISTING: unchanged
│   └── ValueObjects/
│       ├── HolidayConf.cs                   # EXISTING: unchanged
│       └── ExchangeRate.cs                  # EXISTING: unchanged
│
├── Rentier.Application/
│   ├── DTOs/
│   │   ├── ProcessReportsResult.cs          # MODIFIED: +ReportsPartialError, EventErrors type change
│   │   ├── FilingCreationError.cs           # NEW: structured per-event error
│   │   └── RateResolution.cs                # NEW: rate + provenance
│   ├── Handlers/
│   │   └── ProcessReportsCommandHandler.cs  # MODIFIED: uses ExchangeRateResolver, partial success
│   ├── Interfaces/
│   │   └── IExchangeRateFetcher.cs          # EXISTING: unchanged
│   └── Services/
│       └── ExchangeRateResolver.cs          # NEW: date fallback orchestration
│
├── Rentier.Infrastructure/
│   ├── ExchangeRates/
│   │   ├── NbsExchangeRateFetcher.cs        # EXISTING: unchanged
│   │   ├── NbsWebScraper.cs                 # NEW: HTML scraper
│   │   └── CompositeExchangeRateFetcher.cs  # NEW: ASMX→scraper chain
│   ├── Persistence/
│   │   ├── Configurations/
│   │   │   └── FilingConfiguration.cs       # MODIFIED: +new column mappings
│   │   └── Migrations/
│   │       └── ..._0010_FilingRateProvenance.cs  # NEW: EF migration
│   └── InfrastructureServiceExtensions.cs   # MODIFIED: DI registration

tests/
├── Rentier.Domain.Tests/
│   └── Services/
│       └── BusinessDayResolverTests.cs      # NEW: comprehensive day-walk tests
├── Rentier.Application.Tests/
│   ├── Services/
│   │   └── ExchangeRateResolverTests.cs     # NEW: fallback + provenance tests
│   └── Handlers/
│       └── ProcessReportsCommandHandlerTests.cs  # MODIFIED: partial success tests
└── Rentier.Infrastructure.Tests/
    └── ExchangeRates/
        ├── NbsWebScraperTests.cs            # NEW: HTML parsing tests
        └── CompositeExchangeRateFetcherTests.cs  # NEW: fallback chain tests
```

**Structure Decision**: Follows existing Clean Architecture four-project layout. No new projects added. New files placed in existing directory conventions. `ExchangeRates/` directory already exists in Infrastructure for the ASMX fetcher.

## Complexity Tracking

> No constitution violations. All changes fit within existing architectural boundaries.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — | — | — |

## Design Decisions

### D-001: Layered Rate Resolution Pipeline

**Decision**: Three-layer architecture separating date logic (Domain), workflow orchestration (Application), and data fetching (Infrastructure).

```text
Domain:          BusinessDayResolver.WalkBackward(date, holidays)
Application:     ExchangeRateResolver.ResolveAsync(date, currency, holidays)
Infrastructure:  CompositeExchangeRateFetcher → [ASMX, WebScraper]
```

**Rationale**: Preserves Clean Architecture. Each layer is independently testable. Business day rules don't leak into infrastructure; HTTP concerns don't leak into application logic.

### D-002: AngleSharp for NBS HTML Parsing

**Decision**: Use AngleSharp (already a dependency, v1.*) for the NBS web scraper.

**Rationale**: Already in `Rentier.Infrastructure.csproj`, used by `TimeAndDateHolidayScraper`. Adding HtmlAgilityPack would be redundant. AngleSharp provides CSS selectors and DOM API suitable for table parsing.

### D-003: Composite Fetcher as Transparent Decorator

**Decision**: `CompositeExchangeRateFetcher` implements `IExchangeRateFetcher` and wraps ASMX + scraper. Registered as the DI implementation for `IExchangeRateFetcher`.

**Rationale**: No interface changes needed. All callers (including existing code) transparently get ASMX→scraper fallback. The composite only falls back on HTTP/parse errors, not on `RATE_NOT_FOUND` (both sources agree on holiday dates).

### D-004: Nullable Provenance Columns on Filing

**Decision**: `ExchangeRateSourceDate` (DateOnly?) and `ExchangeRateSourceType` (ExchangeRateSourceType?) are nullable on Filing entity.

**Rationale**: Pre-existing filings have no provenance data. Nullable avoids complex data migration. All new filings will have both fields populated. EF migration is additive-only (two nullable columns).

### D-005: PartialError as New ReportStatus Value

**Decision**: Add `PartialError = 3` to `ReportStatus` enum.

**Rationale**: Integer-backed enum in SQLite. Value `3` doesn't conflict with existing values (0, 1, 2). No existing data affected. UI must be updated to display this status, but that's an additive change.

### D-006: ExchangeRateResolver in Application Layer (Not Domain)

**Decision**: `ExchangeRateResolver` lives in Application, not Domain, despite using `BusinessDayResolver` from Domain.

**Rationale**: `ExchangeRateResolver` depends on `IExchangeRateFetcher` (an Application interface that requires async I/O). Domain services must remain pure and I/O-free per Constitution Principle I. The resolver orchestrates async fetcher calls with domain date logic — this is application-layer workflow.

### D-007: Rate Provider Signature Returns RateResolution

**Decision**: The rate provider delegate in `ProcessReportsCommandHandler` changes from `Func<DateOnly, string, Task<ExchangeRate>>` to returning `RateResolution` which bundles the rate with provenance metadata.

**Rationale**: The handler needs both the rate (for tax calculation) and provenance (for filing creation). Bundling them avoids separate lookups or out-of-band state tracking. `TaxCalculationService` receives the `ExchangeRate` extracted from `RateResolution`, keeping its interface unchanged.

### D-008: Structured FilingCreationError Records

**Decision**: Replace `IReadOnlyList<string>` errors with `IReadOnlyList<FilingCreationError>` containing entity name, date, currency, amount, error code, and message.

**Rationale**: Enables the UI to display per-event error details programmatically. Error codes allow categorization (rate not found vs. parse error vs. unsupported currency). Amount and currency help the user identify the exact income event that failed.
