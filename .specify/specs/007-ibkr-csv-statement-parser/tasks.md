---
description: "Task list for Feature 007 — IBKR CSV Statement Parser"
---

# Tasks: Feature 007 — IBKR CSV Statement Parser

**Input**: Design documents from `.specify/specs/007-ibkr-csv-statement-parser/`
**Prerequisites**: plan.md ✅, spec.md ✅, clarify.md ✅

**Tests**: Included — fixture-based XUnit tests per spec.md Phase 3.

**Organization**: Three user stories. US1 = happy-path parsing, US2 = recoverable anomalies, US3 = fatal/unrecoverable input. The parser is a single class; stories layer in capability in dependency order.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: Which user story this task belongs to (US1, US2, US3)
- Paths are relative to repository root `F:\Projects\Rentier\rentier\`

---

## Phase 1: Setup

**Purpose**: Add CsvHelper dependency and configure test-assembly visibility.

- [X] T001 Add `<PackageReference Include="CsvHelper" Version="33.*" />` and the `InternalsVisibleTo` `AssemblyAttribute` block for `Rentier.Infrastructure.Tests` to `src/Rentier.Infrastructure/Rentier.Infrastructure.csproj`. Check whether `InternalsVisibleTo` already exists before adding. Exact XML to add:
  ```xml
  <PackageReference Include="CsvHelper" Version="33.*" />
  ```
  ```xml
  <ItemGroup>
    <AssemblyAttribute Include="System.Runtime.CompilerServices.InternalsVisibleToAttribute">
      <_Parameter1>Rentier.Infrastructure.Tests</_Parameter1>
    </AssemblyAttribute>
  </ItemGroup>
  ```
- [X] T002 Add `<EmbeddedResource Include="Parsers\Fixtures\*.csv" />` to an `ItemGroup` in `tests/Rentier.Infrastructure.Tests/Rentier.Infrastructure.Tests.csproj` so fixture CSVs are embedded in the test assembly and loadable via `Assembly.GetManifestResourceStream`.

**Checkpoint**: `dotnet restore` succeeds; CsvHelper resolves.

---

## Phase 2: Foundational (Application Layer — Blocking Prerequisites)

**Purpose**: Application-layer DTOs and interface that ALL implementation and test tasks depend on.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [X] T003 [P] Create `src/Rentier.Application/Parsing/ParseError.cs`:
  ```csharp
  namespace Rentier.Application.Parsing;
  public sealed record ParseError(string Code, string Message, int? RowNumber = null);
  ```
- [X] T004 [P] Create `src/Rentier.Application/Parsing/DividendRecord.cs`:
  ```csharp
  namespace Rentier.Application.Parsing;
  public sealed record DividendRecord(DateOnly Date, string Currency, string EntityName, decimal Amount);
  ```
- [X] T005 [P] Create `src/Rentier.Application/Parsing/InterestType.cs`:
  ```csharp
  namespace Rentier.Application.Parsing;
  public enum InterestType { Credit, Debit }
  ```
- [X] T006 [P] Create `src/Rentier.Application/Parsing/InterestRecord.cs`:
  ```csharp
  namespace Rentier.Application.Parsing;
  public sealed record InterestRecord(DateOnly Date, string Currency, string EntityName, decimal Amount, InterestType Type);
  // Amount always positive; EntityName = StripIsin(description) for interest rows
  ```
- [X] T007 [P] Create `src/Rentier.Application/Parsing/WithholdingTaxRecord.cs`:
  ```csharp
  namespace Rentier.Application.Parsing;
  public sealed record WithholdingTaxRecord(DateOnly Date, string Currency, string EntityName, decimal Amount);
  // Amount always positive (CSV negatives converted via Math.Abs)
  ```
- [X] T008 [P] Create `src/Rentier.Application/Parsing/IbkrExchangeRate.cs`:
  ```csharp
  namespace Rentier.Application.Parsing;
  public sealed record IbkrExchangeRate(DateOnly Date, string FromCurrency, string ToCurrency, decimal Rate);
  // Do NOT confuse with domain ExchangeRate VO (which carries RateToRsd for NBS rates)
  ```
- [X] T009 [P] Create `src/Rentier.Application/Parsing/StatementParseResult.cs`:
  ```csharp
  namespace Rentier.Application.Parsing;
  public sealed record StatementParseResult(
      IReadOnlyList<DividendRecord>       Dividends,
      IReadOnlyList<InterestRecord>       Interest,
      IReadOnlyList<WithholdingTaxRecord> Withholdings,
      IReadOnlyList<IbkrExchangeRate>     EmbeddedRates,
      IReadOnlyList<ParseError>           Errors);
  ```
- [X] T010 Create `src/Rentier.Application/Interfaces/IStatementParser.cs`. Uses the existing `Result<T, Error>` type from `Rentier.Application.Common`:
  ```csharp
  using Rentier.Application.Common;
  using Rentier.Application.Parsing;

  namespace Rentier.Application.Interfaces;

  public interface IStatementParser
  {
      Task<Result<StatementParseResult, Error>> ParseAsync(
          Stream csvStream,
          CancellationToken cancellationToken = default);
  }
  ```

**Checkpoint**: `dotnet build src/Rentier.Application` succeeds; all 8 types visible.

---

## Phase 3: User Story 1 — Parse a clean activity statement (Priority: P1) 🎯 MVP

**Goal**: `ParseAsync` returns `IsSuccess == true` for a well-formed IBKR CSV. All four record collections populated, `Errors` empty, multi-row dividends aggregated by `(Date, EntityName, Currency)`, ISIN suffixes stripped.

**Independent Test**: Load `happy_path.csv` → assert `IsSuccess == true`, all collections non-empty, `Errors` empty. Load `multiple_dividends_same_entity.csv` → assert one `DividendRecord` with `Amount == 24.00m`.

### Implementation for User Story 1

- [X] T011 [US1] Create `src/Rentier.Infrastructure/Parsing/IbkrCsvParser.cs` with the full class skeleton, `internal static StripIsin`, and all private parse methods as stubs. Key structural contract:

  ```csharp
  using CsvHelper;
  using CsvHelper.Configuration;
  using Rentier.Application.Common;
  using Rentier.Application.Interfaces;
  using Rentier.Application.Parsing;
  using System.Globalization;

  namespace Rentier.Infrastructure.Parsing;

  public sealed class IbkrCsvParser : IStatementParser
  {
      public async Task<Result<StatementParseResult, Error>> ParseAsync(
          Stream csvStream, CancellationToken cancellationToken = default) { ... }

      // [AMENDED C1] ReadRows MUST be async Task<...> because it uses await csv.ReadAsync() internally.
      // A synchronous signature (IReadOnlyList<string[]>) would be a compile error with await.
      private static async Task<IReadOnlyList<string[]>> ReadRows(Stream stream) { ... }

      private static (List<DividendRecord>, List<ParseError>) ParseDividends(IReadOnlyList<string[]> rows) { ... }
      private static (List<WithholdingTaxRecord>, List<ParseError>) ParseWithholdingTax(
          IReadOnlyList<string[]> rows, IReadOnlyList<DividendRecord> dividends) { ... }
      private static (List<InterestRecord>, List<ParseError>) ParseInterest(IReadOnlyList<string[]> rows) { ... }
      private static (List<IbkrExchangeRate>, List<ParseError>) ParseExchangeRates(IReadOnlyList<string[]> rows) { ... }

      // [H1 NOTE] StripIsin uses Split('(')[0].Trim() — takes everything BEFORE the first '(' and trims.
      // This produces "AAPL" from "AAPL(US0378331005) Cash Dividend USD 0.24 per Share".
      // WARNING: clarify.md Q6 shows IsinPattern.Replace(...) which is INCORRECT for this use —
      // Regex.Replace removes the "(ISIN)" token but leaves the surrounding text, producing wrong EntityName.
      // The correct implementation is Split-based as shown here. All unit tests in T026 assert this behaviour.
      internal static string StripIsin(string description) =>
          description.Split('(')[0].Trim();
  }
  ```

  **CsvHelper config** (use inside `ReadRows`):
  ```csharp
  var config = new CsvConfiguration(CultureInfo.InvariantCulture)
  {
      HasHeaderRecord = false,
      MissingFieldFound = null,
      BadDataFound = null,
  };
  // Read all rows as string[]:
  // [AMENDED C1] ReadRows is async Task<IReadOnlyList<string[]>> — use await here.
  using var reader = new StreamReader(csvStream, leaveOpen: true);
  using var csv = new CsvReader(reader, config);
  var rows = new List<string[]>();
  while (await csv.ReadAsync()) rows.Add(csv.Parser.Record!);
  ```

  **Fatal guards** in `ParseAsync` (stubs — fully implemented in T016):
  - `if (csvStream is null) return Result<StatementParseResult, Error>.Failure(...)`
  - `var rows = await ReadRows(csvStream);`  ← [AMENDED C1] must await async ReadRows
  - Wrap `await ReadRows(csvStream)` in try/catch for `CsvHelperException` / `IOException`
  - After `ReadRows`, check for zero recognised IBKR section names → `INVALID_FORMAT`

- [X] T012 [US1] Implement `ParseDividends` inside `src/Rentier.Infrastructure/Parsing/IbkrCsvParser.cs`.

  **Column layout** (0-indexed): `[0]=SectionName [1]=RowType [2]=Currency [3]=Date [4]=Description [5]=Amount`

  **Logic**:
  1. Filter: `row[0] == "Dividends"` AND `row[1] == "Data"`
  2. `DateOnly.TryParseExact(row[3], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)` → failure: add `ParseError("ROW_DATE_INVALID", ..., rowIndex)` and `continue`
  3. `decimal.TryParse(row[5], NumberStyles.Any, CultureInfo.InvariantCulture, out var amount)` → failure: add `ParseError("ROW_AMOUNT_INVALID", ..., rowIndex)` and `continue`
  4. `entityName = StripIsin(row[4])`; `currency = row[2]`
  5. **[AMENDED H4]** `if (string.IsNullOrWhiteSpace(entityName)) { errors.Add(new ParseError("EMPTY_ENTITY_NAME", $"ISIN stripping produced an empty entity name for row {rowIndex}.", rowIndex)); continue; }`
  6. Aggregate into `Dictionary<(DateOnly, string, string), decimal>` keyed by `(date, entityName, currency)` — sum amounts
  7. Return `dictionary.Select(kv => new DividendRecord(kv.Key.Item1, kv.Key.Item3, kv.Key.Item2, kv.Value)).ToList()`

- [X] T013 [US1] Implement `ParseInterest` inside `src/Rentier.Infrastructure/Parsing/IbkrCsvParser.cs`.

  **Column layout**: same as Dividends — `[0]=SectionName [1]=RowType [2]=Currency [3]=Date [4]=Description [5]=Amount`

  **Logic**:
  1. Filter: `row[0] == "Interest"` AND `row[1] == "Data"`
  2. Only process rows where `row[4].Contains("Credit Interest")` OR `row[4].Contains("Debit Interest")`; silently skip all others
  3. Parse date and amount with same guards (`ROW_DATE_INVALID`, `ROW_AMOUNT_INVALID`)
  4. `InterestType type = row[4].Contains("Credit Interest") ? InterestType.Credit : InterestType.Debit`
  5. Aggregate: `Dictionary<(string, DateOnly, InterestType), decimal>` keyed by `(currency, date, type)` → sum `Math.Abs(amount)`
  6. **[AMENDED H2]** `entityName = "Interactive Brokers"` — this is a constant for ALL interest records per spec.md data model and clarify.md Q5. Do NOT use `StripIsin(row[4])` here; interest descriptions have no ISIN and the spec mandates the literal string `"Interactive Brokers"`.
  7. Return list of `InterestRecord(date, currency, entityName, amount, type)` from dictionary

- [X] T014 [US1] Implement `ParseExchangeRates` inside `src/Rentier.Infrastructure/Parsing/IbkrCsvParser.cs`.

  **Column layout**: `[0]=SectionName [1]=RowType [2]=FromCurrency [3]=Date [4]=Description [5]=ToCurrency [6]=Rate`

  **Logic**:
  1. Filter: `row[0] == "Base Currency Exchange Rate"` AND `row[1] == "Data"`
  2. Parse date with `ROW_DATE_INVALID` guard
  3. `decimal.TryParse(row[6], ...)` → failure: `ROW_AMOUNT_INVALID`; success but `rate <= 0`: `ParseError("RATE_NON_POSITIVE", ..., rowIndex)` and `continue`
  4. `key = (date, row[2], row[5])` — check `Dictionary<(DateOnly, string, string), IbkrExchangeRate>`: if key exists add `ParseError("RATE_DUPLICATE", ..., rowIndex)` (last value wins — overwrite)
  5. Return `dictionary.Values.ToList()`

**Checkpoint**: `dotnet build src/Rentier.Infrastructure` succeeds. US1 happy-path traceable through code.

---

## Phase 4: User Story 2 — Parse with recoverable data anomalies (Priority: P2)

**Goal**: Parsing continues past row-level errors. Valid rows returned in their collections; each anomaly adds a `ParseError`. `result.IsSuccess` remains `true`.

**Independent Test**: Load `wht_unmatched.csv` → `IsSuccess == true`, `Withholdings.Count == 0`, `Errors.Count == 1`, `Errors[0].Code == "WHT_UNMATCHED"`. Load `wht_currency_mismatch.csv` → `Errors[0].Code == "WHT_CURRENCY_MISMATCH"`.

### Implementation for User Story 2

- [X] T015 [US2] Implement `ParseWithholdingTax` inside `src/Rentier.Infrastructure/Parsing/IbkrCsvParser.cs`. Called **after** `ParseDividends`; receives already-parsed dividend list.

  **Column layout**: `[0]=SectionName [1]=RowType [2]=Currency [3]=Date [4]=Description [5]=Amount`

  **Logic**:
  1. Filter: `row[0] == "Withholding Tax"` AND `row[1] == "Data"`
  2. Parse date with `ROW_DATE_INVALID` guard
  3. Parse amount with `ROW_AMOUNT_INVALID` guard
  4. If `amount > 0`: `ParseError("WHT_POSITIVE_AMOUNT", $"WHT amount must be negative — got {amount} for {entityName} on {date}", rowIndex)` → `continue`
  5. `entityName = StripIsin(row[4])`; `currency = row[2]`
  6. **[AMENDED H4]** `if (string.IsNullOrWhiteSpace(entityName)) { errors.Add(new ParseError("EMPTY_ENTITY_NAME", $"ISIN stripping produced an empty entity name for WHT row {rowIndex}.", rowIndex)); continue; }`
  7. **[AMENDED C2]** Build TWO lookup structures from `dividends` to distinguish currency mismatch from unmatched:
     ```csharp
     // Full-key dict: exact match including currency
     var divByCurrency = dividends.ToDictionary(d => (d.Date, d.EntityName, d.Currency));
     // Entity-only set: presence check ignoring currency (to detect mismatches)
     var divByEntity = dividends.Select(d => (d.Date, d.EntityName)).ToHashSet();
     ```
  8. **[AMENDED C2]** Perform two-level lookup:
     - `if (divByCurrency.TryGetValue((date, entityName, currency), out _))` → currencies agree → `new WithholdingTaxRecord(date, currency, entityName, Math.Abs(amount))`
     - `else if (divByEntity.Contains((date, entityName)))` → dividend exists but currency differs → `ParseError("WHT_CURRENCY_MISMATCH", $"WHT currency {currency} does not match dividend currency for {entityName} on {date}", rowIndex)` — skip row
     - `else` → no dividend found at all → `ParseError("WHT_UNMATCHED", $"No dividend matches WHT for {entityName} on {date}", rowIndex)` — skip row

**Checkpoint**: All five WHT error scenarios traceable; `dotnet build` still clean.

---

## Phase 5: User Story 3 — Handle unrecoverable / structurally invalid input (Priority: P3)

**Goal**: `ParseAsync` returns `Result.Failure` (never throws) for null stream, closed/unreadable stream, or CSV with no IBKR section headers.

**Independent Test**: `ParseAsync(null!)` → `IsSuccess == false`, `Error.Code == "STREAM_ERROR"`, no exception thrown. Stream of `"this,is,not,ibkr"` → `IsSuccess == false`, `Error.Code == "INVALID_FORMAT"`, no exception thrown.

### Implementation for User Story 3

- [X] T016 [US3] Harden `ParseAsync` in `src/Rentier.Infrastructure/Parsing/IbkrCsvParser.cs` with all fatal guards:
  1. **Null stream** (very first line): `if (csvStream is null) return Result<StatementParseResult, Error>.Failure(new Error("STREAM_ERROR", "Input stream is null or cannot be read."));`
  2. **CsvHelper / IO exception**: Wrap `await ReadRows(csvStream)` in:
     ```csharp
     try { rows = await ReadRows(csvStream); }
     catch (OperationCanceledException)   // [AMENDED M2] must come BEFORE general catch
     {
         return Result<StatementParseResult, Error>.Failure(new Error("CANCELLED", "Parsing was cancelled."));
     }
     catch (Exception ex) when (ex is CsvHelperException or IOException)
     {
         return Result<StatementParseResult, Error>.Failure(new Error("PARSE_EXCEPTION", ex.Message));
     }
     ```
  3. **Invalid format**: After `ReadRows`, check if any `row[0]` matches any of `"Dividends"`, `"Withholding Tax"`, `"Interest"`, `"Base Currency Exchange Rate"`. If none match → `return Result<...>.Failure(new Error("INVALID_FORMAT", "No recognisable IBKR sections found."));`
  4. **Final catch-all**: Wrap the entire method body in an outer `try/catch (Exception ex)` returning `Result.Failure(new Error("PARSE_EXCEPTION", ex.Message))` — no exception must ever escape `ParseAsync`.
  5. **[AMENDED M2]** Cancellation order is critical: `catch (OperationCanceledException)` MUST appear before `catch (Exception)`. If the general catch comes first it will intercept `OperationCanceledException` (which inherits from `Exception`) and emit `"PARSE_EXCEPTION"` instead of `"CANCELLED"`, violating FR-023.

---

## Phase 6: DI Registration

**Purpose**: Wire `IbkrCsvParser` into the container so `IStatementParser` resolves correctly.

- [X] T017 Add `services.AddTransient<IStatementParser, IbkrCsvParser>();` to `InfrastructureServiceExtensions.AddInfrastructureServices()` in `src/Rentier.Infrastructure/InfrastructureServiceExtensions.cs`. Add usings for `Rentier.Application.Interfaces` and `Rentier.Infrastructure.Parsing` if not already present.

---

## Phase 7: Test Fixtures & Tests

**Purpose**: Embed 8 CSV fixtures as `EmbeddedResource` and write `IbkrCsvParserTests` covering all acceptance criteria from spec.md US1, US2, and US3.

### Fixture Files

- [X] T018 [P] Create `tests/Rentier.Infrastructure.Tests/Parsers/Fixtures/happy_path.csv`:
  ```
  Dividends,Header,Currency,Date,Description,Amount
  Dividends,Data,USD,2024-01-15,"AAPL(US0378331005) Cash Dividend USD 0.24 per Share",24.00
  Dividends,Total,USD,,Total,24.00
  Withholding Tax,Header,Currency,Date,Description,Amount
  Withholding Tax,Data,USD,2024-01-15,"AAPL(US0378331005) Cash Dividend USD 0.24 per Share - US Tax",-3.60
  Withholding Tax,Total,USD,,Total,-3.60
  Interest,Header,Currency,Date,Description,Amount
  Interest,Data,USD,2024-01-31,Credit Interest for Jan-2024,5.42
  Interest,Total,USD,,Total,5.42
  Base Currency Exchange Rate,Header,FromCurrency,Date,Description,ToCurrency,ExchangeRate
  Base Currency Exchange Rate,Data,EUR,2024-01-15,EUR.USD 01/15/2024,USD,1.0943
  Base Currency Exchange Rate,Total,,,Total,,
  ```
- [X] T019 [P] Create `tests/Rentier.Infrastructure.Tests/Parsers/Fixtures/multiple_dividends_same_entity.csv`. Two rows for same `(Date, EntityName, Currency)` → expect one aggregated record with `Amount == 24.00m`:
  ```
  Dividends,Header,Currency,Date,Description,Amount
  Dividends,Data,USD,2024-01-15,"AAPL(US0378331005) Cash Dividend USD 0.24 per Share",10.00
  Dividends,Data,USD,2024-01-15,"AAPL(US0378331005) Cash Dividend Adjustment USD 0.14 per Share",14.00
  Dividends,Total,USD,,Total,24.00
  ```
- [X] T020 [P] Create `tests/Rentier.Infrastructure.Tests/Parsers/Fixtures/multiple_dividends_different_dates.csv`. Same entity, different dates → expect two separate records:
  ```
  Dividends,Header,Currency,Date,Description,Amount
  Dividends,Data,USD,2024-01-15,"AAPL(US0378331005) Cash Dividend",12.00
  Dividends,Data,USD,2024-04-15,"AAPL(US0378331005) Cash Dividend",13.00
  Dividends,Total,USD,,Total,25.00
  ```
- [X] T021 [P] Create `tests/Rentier.Infrastructure.Tests/Parsers/Fixtures/interest_debit_credit.csv`. Credit and Debit interest → expect two separate `InterestRecord` instances, both `Amount > 0`:
  ```
  Interest,Header,Currency,Date,Description,Amount
  Interest,Data,USD,2024-01-31,Credit Interest for Jan-2024,5.42
  Interest,Data,USD,2024-01-31,Debit Interest for Jan-2024,-1.20
  Interest,Total,USD,,Total,4.22
  ```
- [X] T022 [P] Create `tests/Rentier.Infrastructure.Tests/Parsers/Fixtures/wht_currency_mismatch.csv`. Dividend is USD; WHT is EUR → expect `WHT_CURRENCY_MISMATCH`:
  ```
  Dividends,Header,Currency,Date,Description,Amount
  Dividends,Data,USD,2024-01-15,"AAPL(US0378331005) Cash Dividend",24.00
  Dividends,Total,USD,,Total,24.00
  Withholding Tax,Header,Currency,Date,Description,Amount
  Withholding Tax,Data,EUR,2024-01-15,"AAPL(US0378331005) Cash Dividend - Tax",-3.60
  Withholding Tax,Total,EUR,,Total,-3.60
  ```
- [X] T023 [P] Create `tests/Rentier.Infrastructure.Tests/Parsers/Fixtures/wht_unmatched.csv`. WHT entity/date matches no dividend → expect `WHT_UNMATCHED`:
  ```
  Dividends,Header,Currency,Date,Description,Amount
  Dividends,Data,USD,2024-01-15,"MSFT(US5949181045) Cash Dividend",24.00
  Dividends,Total,USD,,Total,24.00
  Withholding Tax,Header,Currency,Date,Description,Amount
  Withholding Tax,Data,USD,2024-01-20,"AAPL(US0378331005) Cash Dividend - US Tax",-3.60
  Withholding Tax,Total,USD,,Total,-3.60
  ```
- [X] T024 [P] Create `tests/Rentier.Infrastructure.Tests/Parsers/Fixtures/malformed_row.csv`. First row has non-decimal amount; second row is valid → expect `ROW_AMOUNT_INVALID` error AND one valid `DividendRecord` from MSFT:
  ```
  Dividends,Header,Currency,Date,Description,Amount
  Dividends,Data,USD,2024-01-15,"AAPL(US0378331005) Cash Dividend",NOT_A_NUMBER
  Dividends,Data,USD,2024-01-20,"MSFT(US5949181045) Cash Dividend",48.00
  Dividends,Total,USD,,Total,48.00
  ```
- [X] T025 [P] Create `tests/Rentier.Infrastructure.Tests/Parsers/Fixtures/empty_sections.csv`. Section headers and totals present but zero data rows → expect all collections empty, `Errors` empty:
  ```
  Dividends,Header,Currency,Date,Description,Amount
  Dividends,Total,USD,,Total,0.00
  Interest,Header,Currency,Date,Description,Amount
  Interest,Total,USD,,Total,0.00
  ```

### Test Class

- [X] T026 [US1] [US2] [US3] Create `tests/Rentier.Infrastructure.Tests/Parsers/IbkrCsvParserTests.cs`. Use `var parser = new IbkrCsvParser()` directly (no DI). Fixture loader:
  ```csharp
  private static Stream LoadFixture(string name)
  {
      var resourceName = $"Rentier.Infrastructure.Tests.Parsers.Fixtures.{name}";
      return typeof(IbkrCsvParserTests).Assembly
                 .GetManifestResourceStream(resourceName)
             ?? throw new InvalidOperationException($"Fixture not found: {resourceName}");
  }
  ```

  **StripIsin unit tests** (no stream, no async):
  - `IbkrCsvParser.StripIsin("AAPL(US0378331005) Cash Dividend")` → `"AAPL"`
  - `IbkrCsvParser.StripIsin("MSFT(US5949181045) Dividend")` → `"MSFT"`
  - `IbkrCsvParser.StripIsin("No ISIN description")` → `"No ISIN description"`

  **US1 — Happy path (AC-001 through AC-006)**:
  - `happy_path.csv`: `IsSuccess == true`, `Dividends.Count == 1`, `Withholdings.Count == 1`, `Interest.Count == 1`, `EmbeddedRates.Count == 1`, `Errors` empty
  - `happy_path.csv`: `Dividends[0]` → `EntityName == "AAPL"`, `Currency == "USD"`, `Date == new DateOnly(2024, 1, 15)`, `Amount == 24.00m`
  - `happy_path.csv`: `EmbeddedRates[0]` → `FromCurrency == "EUR"`, `ToCurrency == "USD"`, `Rate == 1.0943m`
  - `multiple_dividends_same_entity.csv`: `Dividends.Count == 1`, `Dividends[0].Amount == 24.00m`
  - `multiple_dividends_different_dates.csv`: `Dividends.Count == 2`
  - `interest_debit_credit.csv`: `Interest.Count == 2`, one `Type == Credit` and one `Type == Debit`, both `Amount > 0`

  **US2 — Recoverable anomalies (AC-001 through AC-006)**:
  - `wht_unmatched.csv`: `IsSuccess == true`, `Withholdings.Count == 0`, `Errors.Count == 1`, `Errors[0].Code == "WHT_UNMATCHED"`
  - `wht_currency_mismatch.csv`: `IsSuccess == true`, `Withholdings.Count == 0`, `Errors.Count == 1`, `Errors[0].Code == "WHT_CURRENCY_MISMATCH"`
  - `malformed_row.csv`: `IsSuccess == true`, `Dividends.Count == 1`, `Errors.Count == 1`, `Errors[0].Code == "ROW_AMOUNT_INVALID"`
  - `empty_sections.csv`: `IsSuccess == true`, all four collections empty, `Errors` empty

  **US3 — Fatal / unrecoverable (AC-001 through AC-003)**:
  - `ParseAsync(null!)`: `IsSuccess == false`, `Error.Code == "STREAM_ERROR"`, no exception propagates
  - Stream containing `"this,is,not,ibkr\n1,2,3"`: `IsSuccess == false`, `Error.Code == "INVALID_FORMAT"`, no exception propagates
  - No test case should observe any thrown exception from `ParseAsync` under any input condition

---

## Phase 8: Build Verification

**Purpose**: Confirm the full solution compiles and all tests pass on a clean build.

- [X] T027 Run `dotnet build Rentier.slnx -warnaserror --no-incremental` from the repository root. Fix any compilation errors or warnings related to Feature 007 types before proceeding to T028.
- [X] T028 Run `dotnet test tests/Rentier.Infrastructure.Tests` from the repository root. All fixture-based and `StripIsin` unit tests must pass (≥ 13 new test cases). If any fail, debug and fix the corresponding implementation task before marking complete. Expected: all existing tests green + ≥ 13 new tests passing.

---

## Dependencies & Execution Order

### Phase Dependencies

```
Phase 1: Setup (T001, T002)
  └─► Phase 2: Foundational DTOs + Interface (T003–T010)  ← BLOCKS EVERYTHING
        └─► Phase 3: US1 Implementation (T011–T014)
              └─► Phase 4: US2 Implementation (T015)
                    └─► Phase 5: US3 Fatal Guards (T016)
                          ├─► Phase 6: DI Registration (T017)
                          └─► Phase 7: Fixtures (T018–T025 [P]) + Tests (T026)
                                └─► Phase 8: Build + Test (T027 → T028)
```

### Story Dependencies

- **US1 (P1)**: Requires Phases 1–2 complete → T011–T014
- **US2 (P2)**: Requires US1 complete (dividends needed for WHT matching) → T015
- **US3 (P3)**: Requires parser skeleton from T011 → T016 hardens existing guards
- **DI (T017)**: Any time after T010 (interface) and T011 (implementation) exist
- **Fixtures (T018–T025)**: All `[P]` — create in parallel once T001/T002 done
- **Tests (T026)**: Requires all 8 fixtures AND all implementation tasks complete

### Parallel Opportunities

- **Phase 2**: T003–T009 are all independent files → parallelise freely
- **Phase 7 fixtures**: T018–T025 are all independent files → parallelise freely
- **Phase 8**: T027 must complete before T028

---

## Parallel Execution Example

```bash
# Phase 2 — all 7 DTO files at once:
Task: "Create ParseError.cs"            # T003
Task: "Create DividendRecord.cs"        # T004
Task: "Create InterestType.cs"          # T005
Task: "Create InterestRecord.cs"        # T006
Task: "Create WithholdingTaxRecord.cs"  # T007
Task: "Create IbkrExchangeRate.cs"      # T008
Task: "Create StatementParseResult.cs"  # T009

# Phase 7 — all 8 fixture CSVs at once:
Task: "Create happy_path.csv"                         # T018
Task: "Create multiple_dividends_same_entity.csv"     # T019
Task: "Create multiple_dividends_different_dates.csv" # T020
Task: "Create interest_debit_credit.csv"              # T021
Task: "Create wht_currency_mismatch.csv"              # T022
Task: "Create wht_unmatched.csv"                      # T023
Task: "Create malformed_row.csv"                      # T024
Task: "Create empty_sections.csv"                     # T025
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001–T002)
2. Complete Phase 2: Foundational DTOs + Interface (T003–T010) — **blocks everything**
3. Complete Phase 3: US1 parser implementation (T011–T014)
4. Create happy-path fixtures (T018–T021)
5. Write US1 portion of T026
6. Run T027 + T028 — **STOP and VALIDATE US1 independently**

### Incremental Delivery

1. MVP (US1) → `ParseAsync` succeeds on clean input, all 4 collections populated
2. Add US2 (T015) → recoverable anomalies surface in `Errors`; `IsSuccess` remains `true`
3. Add US3 (T016) → null/invalid input returns `Result.Failure`; no exceptions leak
4. DI (T017) → parser injectable via `IStatementParser`
5. All remaining fixtures (T022–T025) + full test suite (T026) → green build (T027–T028)

---

## Notes

- `[P]` tasks operate on different files with no shared state — safe to parallelise
- `[Story]` labels map to user stories in `spec.md`
- `Amount` on `WithholdingTaxRecord` and `InterestRecord` is **always stored positive** (`Math.Abs(amount)`)
- `StripIsin` is `internal static` — accessible from tests only via `InternalsVisibleTo` added in T001
- No EF migrations. No `DbContext` changes. No UI components. Pure infrastructure parsing service.
- IBKR `"Total"` and `"Notes"` rows are **silently skipped** in all section parsers — never emit errors for them
- Unknown section names in the CSV are also silently skipped (forward-compatibility per spec edge cases)
- Run T027 (`dotnet build`) before T028 (`dotnet test`) — compilation errors will mask test failures
- Commit after each logical group: after Phase 2 complete, after each US phase complete
