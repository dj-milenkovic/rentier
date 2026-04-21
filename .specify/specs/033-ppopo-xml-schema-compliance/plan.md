# Implementation Plan: PP-OPO XML Schema Compliance Fix + Export Filename Convention

**Branch**: `feature/032-033-034-column-xml-manual` | **Date**: 2025-07-22 | **Spec**: [spec.md](spec.md)  
**Input**: Feature specification from `.specify/specs/033-ppopo-xml-schema-compliance/spec.md`

## Summary

Corrects the PP-OPO XML serializer output to fully comply with the ePorezi portal schema (`http://pid.purs.gov.rs`), fixes a data mapping bug where `OsnovicaZaPorez` incorrectly used `GrossTaxPayableRsd` instead of `GrossIncomeRsd`, updates the suggested export filename to `{yyyy}-{MM}-{Ticker}.xml`, and adds a nullable `Ticker` field to the Filing domain entity for asset traceability across persistence round-trips.

## Technical Context

**Language/Version**: C# 12 / .NET 8.0  
**Primary Dependencies**: System.Xml.Linq (XDocument), EF Core 8 (SQLite), xUnit + FluentAssertions + NSubstitute  
**Storage**: SQLite via EF Core (Filings table needs migration for Ticker column)  
**Testing**: xUnit with FluentAssertions (unit + integration + snapshot tests)  
**Target Platform**: Cross-platform desktop (Windows + macOS) via Avalonia UI  
**Project Type**: Desktop application (Clean Architecture — Domain / Application / Infrastructure / Desktop)  
**Performance Goals**: N/A — XML serialization is synchronous in-memory, sub-millisecond  
**Constraints**: Offline-capable, local-only data, all monetary values as `decimal`, all dates as `DateOnly`  
**Scale/Scope**: Single-user desktop app, one filing per XML export

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- [x] Clean Architecture boundary is preserved (`Desktop -> Application -> Domain`; Infrastructure implements Application contracts only).
  - Domain: `Filing` gains `Ticker` property — no new dependencies.
  - Application: `ExportFilingCommandHandler` gains filename logic — depends on Domain only.
  - Infrastructure: `PpOpoXmlSerializer` rewrite — implements `IXmlFilingSerializer` from Application.
  - No cross-layer violations introduced.
- [x] All monetary/rate/percentage values are modeled as `decimal`.
  - All monetary XML fields use `decimal.ToString("F2")`. No `double`/`float` introduced.
- [x] All business dates are modeled as `DateOnly`; boundary conversions are identified.
  - `IncomeDate` and `FilingDeadline` remain `DateOnly`. Serialized to string at Infrastructure boundary only.
- [x] Security/privacy constraints hold: local-first data, OS credential store for secrets, no telemetry.
  - No new network calls. JMBG removed from suggested filenames (improvement). All data remains local.
- [x] External network usage is limited to approved endpoints (IMAP and NBS) or explicitly justified.
  - `http://pid.purs.gov.rs` is used **only** as an XML namespace string literal, never as a network endpoint.
- [x] All I/O paths are async; UI work avoids blocking calls and uses reactive async command flow.
  - XML serialization remains synchronous in-memory (returns `byte[]`). Export handler remains async for file I/O.
- [x] Tests and coverage impact are defined for Domain (rule/state coverage) and Application (>=90%).
  - Domain: Ticker field creation, validation, null handling, length constraint tests.
  - Application: Filename generation logic tests (with ticker, without ticker, sanitization).
  - Infrastructure: Full serializer rewrite test coverage (namespace, elements, encoding, sections, bug fix).
  - Snapshot test updated to match new XML schema.
- [x] Feature work is mapped to an approved spec task under `.specify/tasks/`.
  - Spec at `.specify/specs/033-ppopo-xml-schema-compliance/spec.md`.

## Project Structure

### Documentation (this feature)

```text
.specify/specs/033-ppopo-xml-schema-compliance/
├── spec.md              # Feature specification (input)
├── plan.md              # This file
├── research.md          # Phase 0 output — all research decisions
├── data-model.md        # Phase 1 output — entity changes and XML schema
├── quickstart.md        # Phase 1 output — developer quickstart guide
├── contracts/
│   └── xml-export-contract.md  # Phase 1 output — XML and filename contracts
└── tasks.md             # Phase 2 output (/speckit.tasks command)
```

### Source Code (affected paths)

```text
src/
├── Rentier.Domain/
│   └── Entities/
│       └── Filing.cs                    # Add Ticker property + factory param + validation
├── Rentier.Application/
│   └── Handlers/
│       ├── ExportFilingCommandHandler.cs     # New filename convention
│       └── ProcessReportsCommandHandler.cs   # Pass ticker to Filing.CreateFromIncome
└── Rentier.Infrastructure/
    ├── Serialization/
    │   └── PpOpoXmlSerializer.cs             # Full rewrite for ePorezi compliance
    └── Persistence/
        ├── Configurations/
        │   └── FilingConfiguration.cs        # Add Ticker column config
        └── Migrations/
            └── XXXX_0013_FilingTicker.cs     # New migration

tests/
├── Rentier.UnitTests/
│   └── [Filing Ticker tests]                 # New: creation, validation, null handling
│   └── [Filename generation tests]           # New: ExportFilingCommandHandler tests
├── Rentier.Infrastructure.Tests/
│   └── Serialization/
│       ├── PpOpoXmlSerializerTests.cs        # Rewrite: all assertions for new schema
│       └── PpOpoXmlSerializerSnapshotTests.cs # Update: new verified XML snapshot
└── Rentier.Tests.Common/                     # Shared test builders (if Filing builder needs Ticker)
```

**Structure Decision**: Existing Clean Architecture 4-project structure is unchanged. All modifications are within existing files/directories. One new migration file is the only new source file.

## Complexity Tracking

No constitution violations. All changes align with established patterns:

| Decision | Justification |
|----------|---------------|
| Serializer rewrite (not patch) | 15+ element name changes, new root element, namespace addition, new sections — patching is more error-prone |
| Ticker as separate field from PayingEntity | Different semantics: PayingEntity = income source name, Ticker = asset symbol for filenames |
| Uppercase UTF-8 via XmlWriterSettings | XDocument.Save with StreamWriter produces lowercase; explicit writer settings needed |

## Change Impact Summary

### By Layer

| Layer | Files Changed | Nature |
|-------|--------------|--------|
| Domain | 1 (`Filing.cs`) | Add property + factory parameter + validation |
| Application | 2 (`ExportFilingCommandHandler.cs`, `ProcessReportsCommandHandler.cs`) | Filename logic + ticker propagation |
| Infrastructure | 3 (`PpOpoXmlSerializer.cs`, `FilingConfiguration.cs`, migration) | Serializer rewrite + DB schema |
| Tests | 4+ files | Rewrite serializer tests, add domain/app tests, update snapshot |

### Risk Assessment

| Risk | Severity | Mitigation |
|------|----------|------------|
| Serializer rewrite breaks existing export | High | Full test coverage including snapshot test; verify against known-good ePorezi XML |
| OsnovicaZaPorez bug fix causes surprise in existing exports | Medium | Documented as bug fix; old behavior was incorrect |
| Migration fails on existing databases | Low | Simple nullable column addition; no data transformation |
| IBKR EntityName doesn't match expected ticker format | Low | EntityName from StripIsin is already the ticker (e.g., "AAPL"); no transformation needed |

### Dependency Order

```text
1. Filing.cs (Domain) — Ticker field
   ↓
2. FilingConfiguration.cs + Migration (Infrastructure) — Persist Ticker
   ↓
3. ProcessReportsCommandHandler.cs (Application) — Populate Ticker from import
   ↓
4. PpOpoXmlSerializer.cs (Infrastructure) — Schema-compliant XML output
   ↓
5. ExportFilingCommandHandler.cs (Application) — New filename convention
   ↓
6. Tests — All layers (can parallelize per layer after dependencies met)
```
