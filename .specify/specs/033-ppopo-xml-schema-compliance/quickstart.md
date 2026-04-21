# Quickstart: PP-OPO XML Schema Compliance Fix + Export Filename Convention

**Feature**: 033-ppopo-xml-schema-compliance  
**Branch**: `feature/032-033-034-column-xml-manual`

## Prerequisites

- .NET 8.0 SDK
- Windows or macOS (cross-platform build)
- SQLite (bundled via EF Core provider)

## Build & Test

```bash
# Restore and build
dotnet build Rentier.slnx

# Run all tests
dotnet test Rentier.slnx

# Run only infrastructure tests (XML serializer)
dotnet test tests/Rentier.Infrastructure.Tests/Rentier.Infrastructure.Tests.csproj --filter "Category=Integration"

# Run only unit tests (Filing domain)
dotnet test tests/Rentier.UnitTests/Rentier.UnitTests.csproj
```

## Files to Change

### Domain Layer (`src/Rentier.Domain/`)

| File | Change |
|------|--------|
| `Entities/Filing.cs` | Add `Ticker` property (nullable, max 20). Add `ticker` param to `CreateFromIncome`. Add validation. |

### Application Layer (`src/Rentier.Application/`)

| File | Change |
|------|--------|
| `Handlers/ExportFilingCommandHandler.cs` | Update filename generation: `{yyyy}-{MM}-{Ticker}.xml` with fallback. |
| `Handlers/ProcessReportsCommandHandler.cs` | Pass `div.EntityName` / `interest.EntityName` as `ticker` param to `Filing.CreateFromIncome`. |

### Infrastructure Layer (`src/Rentier.Infrastructure/`)

| File | Change |
|------|--------|
| `Serialization/PpOpoXmlSerializer.cs` | Rewrite `Serialize()` to emit ePorezi-compliant XML: namespace, element names, encoding, sections. Fix OsnovicaZaPorez mapping. |
| `Persistence/Configurations/FilingConfiguration.cs` | Add `Ticker` column configuration (nullable, max 20). |
| `Persistence/Migrations/XXXX_0013_FilingTicker.cs` | New EF Core migration for Ticker column. |

### Tests

| File | Change |
|------|--------|
| `tests/Rentier.Infrastructure.Tests/Serialization/PpOpoXmlSerializerTests.cs` | Rewrite all assertions to match new XML structure (namespace, element names, sections). |
| `tests/Rentier.Infrastructure.Tests/Serialization/PpOpoXmlSerializerSnapshotTests.cs` | Update snapshot verification and `.verified.xml` file. |
| `tests/Rentier.UnitTests/` | Add tests for Filing.Ticker field (creation, validation, null handling). |
| `tests/Rentier.UnitTests/` | Add tests for filename generation logic. |

## Key Design Decisions

1. **Serializer rewrite, not patch** — The number of structural differences warrants a clean rewrite of the `Serialize` method.
2. **OsnovicaZaPorez bug fix** — Map to `GrossIncomeRsd` (tax base), not `GrossTaxPayableRsd` (computed tax).
3. **Ticker is separate from PayingEntity** — Different semantics; Ticker is for filenames, PayingEntity is the income source name.
4. **CDATA removed** — ePorezi schema uses plain text elements; XDocument handles XML escaping automatically.
5. **Uppercase UTF-8** — Use `XmlWriterSettings` to control the XML declaration encoding string.

## EF Core Migration

```bash
# Generate migration (from repo root)
dotnet ef migrations add 0013_FilingTicker \
  --project src/Rentier.Infrastructure \
  --startup-project src/Rentier.Desktop \
  --output-dir Persistence/Migrations

# Apply (the app auto-migrates on startup, but for manual testing)
dotnet ef database update \
  --project src/Rentier.Infrastructure \
  --startup-project src/Rentier.Desktop
```
