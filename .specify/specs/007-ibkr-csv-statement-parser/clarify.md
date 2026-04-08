# Feature 007 — IBKR CSV Statement Parser: Clarifications

**Status**: Resolved  
**Date**: 2026-04-07  
**Feature**: IBKR CSV activity statement parser for dividends, withholding tax, interest, and embedded exchange rates  
**Method**: Autonomous resolution

---

## Coverage Scan Summary

| Category | Status | Action |
|---|---|---|
| Functional Scope & Behavior | Partial → **Resolved** | Out-of-scope boundary clarified; no UI in scope |
| Domain & Data Model | Missing → **Resolved** | Record type layer, IbkrExchangeRate shape, aggregation keys all decided |
| Interaction & UX Flow | N/A | Parser is headless; no UI flow needed |
| Non-Functional Quality Attributes | Partial → **Resolved** | Error-handling posture decided (graceful, Result-based) |
| Integration & External Dependencies | Partial → **Resolved** | CsvHelper confirmed; IStatementParser scope pinned |
| Edge Cases & Failure Handling | Missing → **Resolved** | WHT mismatch strategy, debit/credit interest treatment, malformed CSV all decided |
| Constraints & Tradeoffs | Clear | decimal + DateOnly + Clean Architecture; no changes needed |
| Terminology & Consistency | Partial → **Resolved** | IbkrExchangeRate canonical name established |
| Completion Signals | Partial → **Resolved** | Acceptance criteria testability confirmed; constitution coverage floors apply |
| Misc / Placeholders | Partial → **Resolved** | ISIN regex confirmed; all TODO markers resolved |

---

## Q1 — Layer placement for DividendRecord, InterestRecord, WithholdingTaxRecord, StatementParseResult

**Decision**: **Application layer DTOs** — `Rentier.Application/Parsing/`.

Rationale:
- These types are outputs of a parsing operation, not domain business entities. They carry no domain invariants or business rules, making them unsuitable as Domain value objects.
- The constitution assigns Domain to "entities, value objects, domain events" with business-rule enforcement. These records have no state transitions or validation beyond type safety.
- The Domain `Report` entity (aggregate over a parsed activity statement) is a future concern that will *consume* these DTOs; they are not part of the Report aggregate itself.
- Placing them in Application keeps Infrastructure → Application data flow clean: `IbkrCsvParser` (Infrastructure) returns `StatementParseResult` (Application DTO) via the `IStatementParser` interface (Application), which Infrastructure implements.

**Canonical locations**:
```
src/Rentier.Application/Parsing/DividendRecord.cs
src/Rentier.Application/Parsing/InterestRecord.cs
src/Rentier.Application/Parsing/WithholdingTaxRecord.cs
src/Rentier.Application/Parsing/StatementParseResult.cs
src/Rentier.Application/Parsing/IbkrExchangeRate.cs   (see Q2)
src/Rentier.Application/Parsing/ParseError.cs         (see Q3)
src/Rentier.Application/Interfaces/IStatementParser.cs
```

---

## Q2 — embeddedRates type: reuse ExchangeRate domain VO or new type?

**Decision**: **New Application DTO `IbkrExchangeRate`** — do NOT reuse the domain `ExchangeRate` value object.

Rationale:
- The domain `ExchangeRate` record stores `decimal RateToRsd` — semantically "how many RSD for 1 unit of foreign currency", sourced from NBS.
- IBKR Base Currency Exchange Rate section yields `FromCurrency → USD` rates (e.g., EUR/USD = 1.0943) — categorically different semantics.
- Storing an EUR/USD rate in a field named `RateToRsd` would be a silent semantic error, violating the principle of domain model correctness and creating subtle bugs in future cross-rate calculations.
- `IbkrExchangeRate` is an intermediate parse artifact, not a cached NBS rate; it belongs in Application/Parsing alongside the other parse result types.

**Canonical shape**:
```csharp
public sealed record IbkrExchangeRate(
    DateOnly Date,
    string FromCurrency,
    string ToCurrency,
    decimal Rate);
```

`ToCurrency` is always `"USD"` in the current IBKR format but is included explicitly to make the pair self-describing and resilient to future IBKR format changes.

---

## Q3 — WHT currency mismatch: full Failure or partial success with error collection?

**Decision**: **Partial success with `IReadOnlyList<ParseError> Errors` embedded in `StatementParseResult`** — do NOT return `Result.Failure` on a single mismatch.

Rationale:
- An activity statement may contain dozens of dividend/WHT pairs. Failing the entire parse on one mismatch loses all correctly-parsed records, making the feature nearly useless for real-world statements that may have one anomalous row.
- Accuracy is paramount for tax filings: surfacing every anomaly is more valuable than failing silently. The `ParseError` collection allows the caller (future Application handler) to decide whether to proceed, prompt the user, or reject the import.
- The `Result<StatementParseResult, Error>` return type is reserved for **unrecoverable** failures only (e.g., the stream is not a valid IBKR CSV at all, or the stream cannot be read).

**`ParseError` shape**:
```csharp
public sealed record ParseError(string Code, string Message, int? RowNumber = null);
```

**Error codes** for WHT mismatch:
- `"WHT_CURRENCY_MISMATCH"` — WHT row currency does not match the corresponding dividend record currency for the same (Date, EntityName) key.
- `"WHT_UNMATCHED"` — WHT row has no corresponding dividend record (orphaned WHT).

**`StatementParseResult` shape**:
```csharp
public sealed record StatementParseResult(
    IReadOnlyList<DividendRecord>       Dividends,
    IReadOnlyList<InterestRecord>       Interest,
    IReadOnlyList<WithholdingTaxRecord> Withholdings,
    IReadOnlyList<IbkrExchangeRate>     EmbeddedRates,
    IReadOnlyList<ParseError>           Errors);
```

A caller can check `result.Errors.Count == 0` for a clean parse, or inspect errors before proceeding to filing creation.

---

## Q4 — Dividend aggregation key: same entity + same date → sum or keep separate?

**Decision**: **Aggregate (sum amounts) by `(EntityName, Date, Currency)`** — confirmed as specified in the feature request.

Rationale:
- IBKR sometimes emits multiple rows for the same entity on the same date (e.g., dividend adjustments, multiple share classes under the same entity name). Summing them produces the single net dividend figure needed for PP-OPO filing.
- Different dates always produce separate `DividendRecord` instances; no cross-date aggregation.
- Different currencies under the same entity on the same date produce separate records (currency is part of the aggregation key).

**`DividendRecord` shape**:
```csharp
public sealed record DividendRecord(
    DateOnly Date,
    string   Currency,
    string   EntityName,
    decimal  Amount);
```

`EntityName` is the ISIN-stripped entity name (see Q6 for regex).

---

## Q5 — Interest: net credit+debit or keep separate?

**Decision**: **Keep credit and debit as separate records** — do NOT net them at parse time.

Rationale:
- Tax treatment differs: credit interest (received from IBKR) is taxable passive income in Serbia; debit interest (paid to IBKR for margin) is an expense with potentially different deductibility rules. Netting before tax calculation destroys information required for correct PP-OPO classification.
- The feature request phrase "interest debit/credit netting" describes a potential edge-case aggregate option; however, given tax-accuracy requirements (Constitution §III), the safer default is to preserve both types so the downstream Application handler can apply the correct tax logic.
- Aggregation **within** each type (multiple credit rows same currency+date → sum) is still applied, since those represent the same tax event.

**`InterestType` enum**:
```csharp
public enum InterestType { Credit, Debit }
```

**`InterestRecord` shape**:
```csharp
public sealed record InterestRecord(
    DateOnly     Date,
    string       Currency,
    string       EntityName,   // always "Interactive Brokers" per spec
    decimal      Amount,       // always positive; sign conveyed by InterestType
    InterestType Type);
```

Amounts are stored as **positive decimals** for both types; the `InterestType` discriminator conveys sign semantics. This keeps the type system honest — no negative values in Amount.

Filter rules:
- Include only description rows containing `"Credit Interest"` → `InterestType.Credit`
- Include only description rows containing `"Debit Interest"` → `InterestType.Debit`
- Skip all other Interest rows (e.g., PIK interest, accrued interest — not relevant to PP-OPO).

---

## Q6 — ISIN stripping regex: confirm pattern

**Decision**: **Confirmed** — C# regex `@"\([A-Z]{2}[A-Z0-9]{9}[0-9]\)"` is correct for all standard ISINs.

Analysis:
- ISIN structure (ISO 6166): 2-letter country code + 9 alphanumeric NSIN characters + 1 numeric check digit = 12 characters total.
- Pattern breakdown: `\(` + `[A-Z]{2}` (country) + `[A-Z0-9]{9}` (NSIN body) + `[0-9]` (check digit) + `\)` — matches exactly.
- The parentheses are literal `(` and `)` characters as they appear in IBKR description strings.

**Canonical implementation**:
```csharp
private static readonly Regex IsinPattern =
    new(@"\([A-Z]{2}[A-Z0-9]{9}[0-9]\)", RegexOptions.Compiled);

internal static string StripIsin(string description) =>
    IsinPattern.Replace(description, string.Empty).Trim();
```

Use `internal static` so the regex is accessible in unit tests within the same assembly (or via `InternalsVisibleTo`).

---

## Q7 — WHT → dividend matching: use same ISIN stripping?

**Decision**: **Yes** — apply identical `StripIsin()` to both Dividend and WHT description fields; the **matching key** is `(Date, EntityName, Currency)`.

Rationale:
- Both sections share the same `EntityName(ISIN) description text` format per the CSV spec. Using the same stripping function guarantees that `EntityName` is derived identically in both paths, making the match deterministic.
- Matching by `(Date, EntityName, Currency)` is preferred over `(Date, ISIN)` because the ISIN is discarded after stripping — and entity-name matching aligns with what a user would verify visually.

**Matching algorithm** (post-parse aggregation phase):
1. Build a `Dictionary<(DateOnly, string, string), DividendRecord>` keyed on `(Date, EntityName, Currency)`.
2. For each WHT row, strip ISIN from description → derive `EntityName`.
3. Look up `(Date, EntityName, WhtCurrency)` in the dividend dictionary.
4. If found and `WhtCurrency == DividendRecord.Currency` → create `WithholdingTaxRecord` and associate.
5. If found but currencies differ → emit `ParseError("WHT_CURRENCY_MISMATCH", ...)`.
6. If not found → emit `ParseError("WHT_UNMATCHED", ...)`.

**`WithholdingTaxRecord` shape**:
```csharp
public sealed record WithholdingTaxRecord(
    DateOnly Date,
    string   Currency,
    string   EntityName,
    decimal  Amount);   // always positive (CSV negatives are converted)
```

WHT amounts in the CSV are negative; convert via `Math.Abs(rawAmount)` during row parsing.

---

## Q8 — IbkrExchangeRate in embeddedRates: canonical shape

**Decision**: As established in Q2 — `IbkrExchangeRate(DateOnly Date, string FromCurrency, string ToCurrency, decimal Rate)`.

Additional rules:
- `FromCurrency` = the left-hand ISO 4217 code (e.g., `"EUR"`).
- `ToCurrency` = the right-hand code, always `"USD"` in IBKR Base Currency Exchange Rate section.
- `Rate` = decimal exchange rate (e.g., `1.0943`), always positive; emit `ParseError("RATE_NON_POSITIVE", ...)` if ≤ 0.
- No aggregation needed — one rate per `(Date, FromCurrency, ToCurrency)` pair; if duplicates appear, last value wins with a `ParseError("RATE_DUPLICATE", ...)` warning.
- `EmbeddedRates` is consumed by a future cross-rate calculation step (currency→USD then USD→RSD via NBS). It is **not** persisted by this parser — the caller decides whether to pass it to `IExchangeRateFetcher` or discard it.

---

## Q9 — Malformed/truncated CSV handling: graceful or throw?

**Decision**: **Fully graceful — never throw from `ParseAsync`**. All exceptional conditions return `Result.Failure(...)` or populate `StatementParseResult.Errors`.

Posture:
| Condition | Response |
|---|---|
| Stream is null or unreadable | `Result.Failure(new Error("STREAM_ERROR", "..."))` |
| CSV has no recognised IBKR section headers | `Result.Failure(new Error("INVALID_FORMAT", "No IBKR sections found"))` |
| CsvHelper throws `CsvHelperException` | Catch → `Result.Failure(new Error("PARSE_EXCEPTION", ex.Message))` |
| Single malformed data row (bad date, non-decimal amount) | `ParseError` added to `Errors` list; row is skipped; parsing continues |
| Section header present but zero data rows | Allowed; section produces an empty list — not an error |
| `Total` row in any section | Skip silently (identified by `row[1] == "Total"`) |

Rationale: Consistent with project-wide Result pattern (Constitution §V). A desktop user importing their own CSV file should receive actionable error information, not an unhandled exception crash.

---

## Q10 — IStatementParser vs. IReportImporter pipeline

**Decision**: **Standalone `IStatementParser` interface in Application** — no pipeline wrapper in this feature.

Rationale:
- The feature request explicitly specifies `IStatementParser` with `ParseAsync(Stream, CancellationToken) → Result<StatementParseResult, Error>`. Honoring this keeps scope tight.
- A broader `IReportImporter` pipeline (discover CSV → parse → create Report aggregate → persist) is a separate feature responsibility. Conflating them now creates premature architecture coupling.
- `IStatementParser` is composable: a future `ImportActivityStatementCommand` handler can depend on `IStatementParser` and delegate parsing, then build the `Report` entity and persist it independently.

**Canonical interface**:
```csharp
// src/Rentier.Application/Interfaces/IStatementParser.cs
public interface IStatementParser
{
    Task<Result<StatementParseResult, Error>> ParseAsync(
        Stream            csvStream,
        CancellationToken cancellationToken = default);
}
```

Implementation registered in Infrastructure DI:
```csharp
services.AddTransient<IStatementParser, IbkrCsvParser>();
```

---

## Architecture Decisions

### AD-1: File locations

```
src/Rentier.Application/
  Interfaces/IStatementParser.cs                  NEW
  Parsing/DividendRecord.cs                       NEW
  Parsing/InterestRecord.cs                       NEW
  Parsing/InterestType.cs                         NEW (enum)
  Parsing/WithholdingTaxRecord.cs                 NEW
  Parsing/IbkrExchangeRate.cs                     NEW
  Parsing/ParseError.cs                           NEW
  Parsing/StatementParseResult.cs                 NEW

src/Rentier.Infrastructure/
  Parsing/IbkrCsvParser.cs                        NEW
  Parsing/IbkrCsvParser.Dividends.cs              NEW (partial — section reader)
  Parsing/IbkrCsvParser.WithholdingTax.cs         NEW (partial — section reader)
  Parsing/IbkrCsvParser.Interest.cs               NEW (partial — section reader)
  Parsing/IbkrCsvParser.ExchangeRates.cs          NEW (partial — section reader)

tests/Rentier.Infrastructure.Tests/
  Parsing/IbkrCsvParserTests.cs                   NEW (xUnit)
  Parsing/Fixtures/happy_path.csv                 NEW (embedded resource)
  Parsing/Fixtures/wht_currency_mismatch.csv      NEW
  Parsing/Fixtures/interest_debit_credit.csv      NEW
  Parsing/Fixtures/malformed_row.csv              NEW
  Parsing/Fixtures/duplicate_dividend_same_date.csv  NEW
```

### AD-2: CsvHelper integration

CsvHelper NuGet package (`CsvHelper` by Josh Close) MUST be added to `Rentier.Infrastructure.csproj` only. It MUST NOT appear in `Rentier.Application` or `Rentier.Domain`.

The parser uses CsvHelper in **manual/low-level mode** (not class-map mode) because the IBKR CSV is multi-section and heterogeneous — standard class-map parsing cannot handle it. The parser reads the CSV line-by-line, detects section boundaries by checking `record[0]` (e.g., `"Dividends"`, `"Withholding Tax"`), and switches parsing mode accordingly.

### AD-3: Section detection strategy

IBKR CSVs are multi-section files where each section header row has `record[1] == "Header"`. Strategy:
1. Read CSV with CsvHelper's `CsvReader` in raw field mode (no class map).
2. On each row, inspect `record[0]` (section name) and `record[1]` (row type: `"Header"`, `"Data"`, `"Total"`, `"Notes"`).
3. Dispatch to the appropriate section accumulator.
4. `"Total"` and `"Notes"` rows are skipped.
5. Unknown sections are skipped silently (future-proofing for IBKR format additions).

### AD-4: No EF migration needed

This feature is purely a parsing service. It does not persist data. No `DbContext` changes, no migration files.

### AD-5: Test fixture CSV files

CSV fixtures MUST be embedded resources in the test project (`Build Action: EmbeddedResource`). Tests load them via `Assembly.GetManifestResourceStream(...)`. This avoids path-relative fragility across operating systems (constitution §CI: Windows + macOS matrix).

---

## Explicit Out-of-Scope Decisions

| Item | Decision |
|---|---|
| Currency → RSD conversion | **Out of scope** — `IbkrExchangeRate` provides raw rates; conversion via NBS (Feature 006) is a caller responsibility |
| `Report` entity creation | **Out of scope** — future feature builds `Report` aggregate from `StatementParseResult` |
| Desktop UI (file picker, progress) | **Out of scope** — `IStatementParser` is headless |
| IBKR sections other than the four specified | **Skipped silently** by parser |
| ISIN lookup / validation against external registry | **Out of scope** |
| Multi-currency dividend netting | **Out of scope** — each currency is a separate record |

---

## Assumptions

1. IBKR CSV files are UTF-8 encoded. CsvHelper defaults to UTF-8; no BOM handling needed for typical IBKR exports.
2. Date format in IBKR CSVs is always `yyyy-MM-dd`. `DateOnly.ParseExact(dateStr, "yyyy-MM-dd")` is used; no format fallbacks.
3. `"Credit Interest"` and `"Debit Interest"` are matched via `description.Contains(...)` (case-sensitive, as IBKR descriptions are consistent).
4. The `EntityName` extracted via ISIN stripping may contain trailing text from the description (e.g., `"AAPL"` from `"AAPL(US0378331005) Cash Dividend USD 0.24 per Share"`). Only the text **before** the `(ISIN)` token is used as entity name — the remaining description text after `(ISIN)` is discarded.
5. IBKR CSV `Amount` fields use plain decimal notation (no thousands separators). CsvHelper default decimal parsing is sufficient.
6. WHT amounts in the CSV are negative (e.g., `-18.00`). `Math.Abs(amount)` is applied during parsing. A positive WHT amount in the CSV is treated as a `ParseError("WHT_POSITIVE_AMOUNT", ...)`.
7. `IStatementParser` is stateless and thread-safe (registered as `Transient`).
8. Application coverage target: ≥ 90% per constitution §V. Domain: N/A (no Domain code in this feature). Infrastructure parser is tested via Infrastructure unit tests.
9. `InternalsVisibleTo("Rentier.Infrastructure.Tests")` is added to `Rentier.Infrastructure.csproj` so `internal` helper methods (e.g., `StripIsin`) can be tested directly.
10. No `async` I/O inside `IbkrCsvParser` beyond the initial stream read; `ParseAsync` signature is `async Task<...>` for interface compliance but may complete synchronously.
