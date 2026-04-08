# Quickstart: Filing Creation Reliability Fixes

**Feature**: 019-filing-creation-reliability-fixes  
**Branch**: `feature/019-filing-creation-reliability-fixes`

## What This Feature Does

Fixes filing generation failures caused by missing exchange rates on weekends and Serbian public holidays. Adds business day fallback, partial success processing, NBS web scraper as backup source, and rate provenance tracking.

## Key Files to Understand

### Domain Layer
| File | Purpose |
|---|---|
| `src/Rentier.Domain/Services/BusinessDayResolver.cs` | **NEW** — Backward business day walk (skip weekends + holidays) |
| `src/Rentier.Domain/Services/FilingDeadlineCalculator.cs` | Existing — Forward business day walk (filing deadline) |
| `src/Rentier.Domain/Services/TaxCalculationService.cs` | Existing — Tax calculation, calls rate provider |
| `src/Rentier.Domain/Entities/Filing.cs` | **MODIFIED** — New `ExchangeRateSourceDate`, `ExchangeRateSourceType` properties |
| `src/Rentier.Domain/Enums/ReportStatus.cs` | **MODIFIED** — New `PartialError = 3` value |
| `src/Rentier.Domain/Enums/ExchangeRateSourceType.cs` | **NEW** — `Exact`, `Fallback` enum |
| `src/Rentier.Domain/ValueObjects/HolidayConf.cs` | Existing — Holiday configuration (used by both calculators) |

### Application Layer
| File | Purpose |
|---|---|
| `src/Rentier.Application/Services/ExchangeRateResolver.cs` | **NEW** — Date fallback orchestration with provenance |
| `src/Rentier.Application/Handlers/ProcessReportsCommandHandler.cs` | **MODIFIED** — Partial success, structured errors, provenance |
| `src/Rentier.Application/DTOs/ProcessReportsResult.cs` | **MODIFIED** — Structured `FilingCreationError` list |
| `src/Rentier.Application/DTOs/FilingCreationError.cs` | **NEW** — Structured per-event error record |
| `src/Rentier.Application/DTOs/RateResolution.cs` | **NEW** — Rate + provenance metadata |
| `src/Rentier.Application/Interfaces/IExchangeRateFetcher.cs` | Existing — Unchanged interface |

### Infrastructure Layer
| File | Purpose |
|---|---|
| `src/Rentier.Infrastructure/ExchangeRates/NbsWebScraper.cs` | **NEW** — HTML scraper fetcher (AngleSharp) |
| `src/Rentier.Infrastructure/ExchangeRates/CompositeExchangeRateFetcher.cs` | **NEW** — ASMX→scraper fallback chain |
| `src/Rentier.Infrastructure/ExchangeRates/NbsExchangeRateFetcher.cs` | Existing — Unchanged ASMX fetcher |
| `src/Rentier.Infrastructure/Persistence/Configurations/FilingConfiguration.cs` | **MODIFIED** — New column mappings |
| `src/Rentier.Infrastructure/InfrastructureServiceExtensions.cs` | **MODIFIED** — DI registration for composite fetcher |
| `src/Rentier.Infrastructure/Persistence/Migrations/..._0010_FilingRateProvenance.cs` | **NEW** — EF migration |

## Architecture Decision: Layered Rate Resolution

```text
Handler (Application)
  └── ExchangeRateResolver (Application) — business day fallback + provenance
        ├── BusinessDayResolver (Domain) — "which dates are business days?"
        └── IExchangeRateFetcher (Application interface)
              └── CompositeExchangeRateFetcher (Infrastructure) — ASMX→scraper chain
                    ├── NbsExchangeRateFetcher (Infrastructure) — primary source
                    └── NbsWebScraper (Infrastructure) — fallback source
```

## Build & Test

```bash
# Build
dotnet build Rentier.slnx

# Run all tests
dotnet test Rentier.slnx

# Run domain tests only (business day resolver, deadline calculator)
dotnet test tests/Rentier.Domain.Tests

# Run application tests only (handler, resolver)
dotnet test tests/Rentier.Application.Tests

# Generate EF migration (after adding new columns to Filing)
cd src/Rentier.Infrastructure
dotnet ef migrations add 0010_FilingRateProvenance --startup-project ../Rentier.Desktop
```

## Key Design Decisions

1. **AngleSharp for HTML parsing** — already a dependency (v1.*), used by holiday scraper
2. **Nullable provenance columns** — backward-compatible with existing filings
3. **Composite fetcher pattern** — transparent to callers, same `IExchangeRateFetcher` interface
4. **ExchangeRateResolver in Application layer** — business day fallback is workflow logic, not domain rule
5. **BusinessDayResolver in Domain layer** — "is this a business day?" is a domain concept
