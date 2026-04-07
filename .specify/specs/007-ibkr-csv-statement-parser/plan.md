# Feature 007 — IBKR CSV Statement Parser: Implementation Plan

## Problem
Parse IBKR Flex Query / Activity Statement CSV exports into structured, typed
records needed for PP-OPO tax filing calculation (dividends, withholding tax,
interest, embedded FX rates).

## Approach
Pure infrastructure parsing service. No EF, no UI. Three-phase implementation:
1. Application DTOs + interface contract
2. Infrastructure parser (IbkrCsvParser with CsvHelper)
3. Tests with embedded CSV fixtures

---

## Phase 1 — Application Layer (DTOs + Interface)

Create `Rentier.Application/Parsing/` namespace with 7 new files:

| File | Content |
|---|---|
| `ParseError.cs` | `record ParseError(string Code, string Message, int? RowNumber)` |
| `DividendRecord.cs` | `record DividendRecord(DateOnly Date, string Currency, string EntityName, decimal Amount)` |
| `InterestType.cs` | `enum InterestType { Credit, Debit }` |
| `InterestRecord.cs` | `record InterestRecord(DateOnly Date, string Currency, string EntityName, decimal Amount, InterestType Type)` |
| `WithholdingTaxRecord.cs` | `record WithholdingTaxRecord(DateOnly Date, string Currency, string EntityName, decimal Amount)` |
| `IbkrExchangeRate.cs` | `record IbkrExchangeRate(DateOnly Date, string FromCurrency, string ToCurrency, decimal Rate)` |
| `StatementParseResult.cs` | Aggregate record with 5 collections |

`IStatementParser` goes in `Rentier.Application/Interfaces/IStatementParser.cs`.

## Phase 2 — Infrastructure Parser

Single file `src/Rentier.Infrastructure/Parsing/IbkrCsvParser.cs`:

```
IbkrCsvParser : IStatementParser
  ├── ParseAsync(stream, ct) → Result<StatementParseResult, Error>
  ├── private static ReadRows(stream) → IReadOnlyList<string[]>
  ├── private static ParseDividends(rows) → (List<DividendRecord>, List<ParseError>)
  ├── private static ParseWithholdingTax(rows, dividends) → (List<WithholdingTaxRecord>, List<ParseError>)
  ├── private static ParseInterest(rows) → (List<InterestRecord>, List<ParseError>)
  ├── private static ParseExchangeRates(rows) → (List<IbkrExchangeRate>, List<ParseError>)
  └── internal static string StripIsin(string description) — description.Split('(')[0].Trim()
```

**Section detection**: `record[0]` = section name, `record[1]` = row type ("Header"/"Data"/"Total"/"Notes").  
**Only "Data" rows processed** per section.

**CsvHelper config**:
```csharp
new CsvConfiguration(CultureInfo.InvariantCulture)
{
    HasHeaderRecord = false,
    MissingFieldFound = null,
    BadDataFound = null,
}
```

**Dividend aggregation**:  
Key = `(EntityName, Date, Currency)` → `Dictionary` → sum amounts.

**WHT matching** (post-dividend pass):  
Build `Dictionary<(DateOnly, string, string), DividendRecord>` from parsed dividends.  
For each WHT row: lookup by `(Date, StripIsin(description), Currency)`.

**Interest aggregation**:  
Key = `(Currency, Date, InterestType)` → sum amounts (all stored positive).

**Error guards** (row-level, add ParseError + continue):
- `DateOnly.TryParseExact` fails → `"ROW_DATE_INVALID"`
- `decimal.TryParse` fails → `"ROW_AMOUNT_INVALID"`
- WHT amount > 0 → `"WHT_POSITIVE_AMOUNT"`
- WHT no matching dividend → `"WHT_UNMATCHED"`
- WHT currency mismatch → `"WHT_CURRENCY_MISMATCH"`
- FX rate ≤ 0 → `"RATE_NON_POSITIVE"`
- Duplicate FX rate → `"RATE_DUPLICATE"` (last value wins)

**Fatal guards** (return Result.Failure):
- stream null → `"STREAM_ERROR"`
- CsvHelperException → `"PARSE_EXCEPTION"`
- Zero IBKR sections found → `"INVALID_FORMAT"`

## Phase 3 — Tests

`tests/Rentier.Infrastructure.Tests/Parsers/IbkrCsvParserTests.cs` with fixture files
embedded as `EmbeddedResource` in the test csproj:

| Fixture | Tests |
|---|---|
| `happy_path.csv` | AC-001: all sections populated, Errors empty |
| `multiple_dividends_same_entity.csv` | AC-002: aggregation sums to 1 record |
| `multiple_dividends_different_dates.csv` | AC-003: 2 separate records |
| `interest_debit_credit.csv` | AC-004: 2 InterestRecord, Credit + Debit |
| `wht_currency_mismatch.csv` | AC-005: Errors has WHT_CURRENCY_MISMATCH |
| `wht_unmatched.csv` | AC-006: Errors has WHT_UNMATCHED |
| `malformed_row.csv` | AC-007: Errors has row error, valid rows still parsed |
| `empty_sections.csv` | AC-008: empty lists, no errors |
| null stream | AC-009: Result.Failure STREAM_ERROR |
| StripIsin unit tests | ISIN removed, entity name preserved |

---

## Dependency Order

```
1. Application/Parsing/*.cs + IStatementParser.cs
2. Infrastructure.csproj — add CsvHelper + InternalsVisibleTo
3. Infrastructure/Parsing/IbkrCsvParser.cs
4. InfrastructureServiceExtensions.cs — add DI registration
5. Test fixtures (*.csv EmbeddedResource)
6. IbkrCsvParserTests.cs
7. Build + test
```

---

## No EF Migration
This feature has no persistence. No `DbContext` changes. No migration files.

---

## Project File Changes

### Rentier.Infrastructure.csproj
```xml
<PackageReference Include="CsvHelper" Version="33.*" />
<AssemblyAttribute Include="System.Runtime.CompilerServices.InternalsVisibleToAttribute">
  <_Parameter1>Rentier.Infrastructure.Tests</_Parameter1>
</AssemblyAttribute>
```

### Rentier.Infrastructure.Tests.csproj
```xml
<EmbeddedResource Include="Parsers\Fixtures\*.csv" />
```
