# Feature Specification: IBKR CSV Statement Parser

**Feature Branch**: `feature/007-ibkr-csv-parser`  
**Created**: 2026-04-07  
**Status**: Draft  
**Clarifications resolved**: C1–C13 (see `clarify.md`)

---

## Overview

Interactive Brokers (IBKR) exports account activity as multi-section CSV files. Rentier needs to ingest these files and extract four types of financial records — dividends, interest, withholding tax, and embedded exchange rates — so they can later be used to generate Serbian PP-OPO tax filings.

This feature delivers a **headless parsing service** (`IStatementParser`) that accepts a raw CSV stream and returns a structured result containing all extracted records plus any parse-time warnings. No UI, no database writes, and no currency conversion are part of this feature.

**Goal**: Given a valid IBKR activity statement CSV stream, produce a `StatementParseResult` containing typed, aggregated, validated financial records with full error transparency.

---

## User Scenarios & Testing

### User Story 1 — Parse a clean activity statement (Priority: P1)

A developer (or future Application handler) calls `IStatementParser.ParseAsync` with a well-formed IBKR CSV stream. The result contains all four record collections populated correctly and zero parse errors.

**Why this priority**: This is the core value of the feature — without successful happy-path parsing, no downstream tax calculation is possible.

**Independent Test**: Provide a fixture CSV containing at least one row in each section (Dividends, Withholding Tax, Interest, Base Currency Exchange Rate). Assert all four collections are non-empty and `Errors` is empty.

**Acceptance Scenarios**:

1. **Given** a well-formed IBKR CSV with a single dividend row for "AAPL(US0378331005)" on 2025-03-15, USD, amount 24.00, **When** `ParseAsync` is called, **Then** `result.IsSuccess == true` AND `Dividends` contains exactly one `DividendRecord` with `EntityName = "AAPL"`, `Date = 2025-03-15`, `Currency = "USD"`, `Amount = 24.00`.
2. **Given** a CSV with two dividend rows for the same entity, date, and currency (amounts 10.00 and 14.00), **When** parsed, **Then** `Dividends` contains exactly one record with `Amount = 24.00` (aggregated by key `(Date, EntityName, Currency)`).
3. **Given** a CSV with two dividend rows for the same entity and date but different currencies (USD and EUR), **When** parsed, **Then** `Dividends` contains exactly two records — one per currency.
4. **Given** a CSV with a matching WHT row for the same `(Date, EntityName, Currency)` as a dividend, **When** parsed, **Then** `Withholdings` contains one `WithholdingTaxRecord` with a positive `Amount` and `Errors` is empty.
5. **Given** a CSV with a Credit Interest row and a Debit Interest row on the same date, **When** parsed, **Then** `Interest` contains two separate `InterestRecord` instances — one with `Type = Credit` and one with `Type = Debit`, both with positive `Amount` values.
6. **Given** a CSV with a Base Currency Exchange Rate row for EUR/USD = 1.0943 on 2025-03-15, **When** parsed, **Then** `EmbeddedRates` contains one `IbkrExchangeRate` with `FromCurrency = "EUR"`, `ToCurrency = "USD"`, `Rate = 1.0943`, `Date = 2025-03-15`.

---

### User Story 2 — Parse with recoverable data anomalies (Priority: P2)

The CSV contains one or more rows with data anomalies (orphaned WHT, currency mismatch, malformed decimal). Parsing continues and returns all successfully-parsed records alongside a populated `Errors` list describing every anomaly.

**Why this priority**: Real-world IBKR statements contain anomalies. A user importing their own tax year's data must not lose all good records because of a single bad row.

**Independent Test**: Provide a fixture CSV with one valid dividend, one orphaned WHT row (no corresponding dividend), and one WHT row with a currency mismatch. Assert `result.IsSuccess == true`, `Dividends.Count == 1`, `Withholdings.Count == 0`, `Errors.Count == 2` with appropriate error codes.

**Acceptance Scenarios**:

1. **Given** a WHT row with `(Date, EntityName, Currency)` that matches no dividend record, **When** parsed, **Then** `Errors` contains a `ParseError` with `Code = "WHT_UNMATCHED"` and a descriptive `Message` referencing the entity name and date.
2. **Given** a WHT row that matches a dividend by `(Date, EntityName)` but has a different `Currency`, **When** parsed, **Then** `Errors` contains a `ParseError` with `Code = "WHT_CURRENCY_MISMATCH"` referencing both currencies.
3. **Given** a single malformed data row (non-parseable date or non-decimal amount) within a section that otherwise has valid rows, **When** parsed, **Then** the malformed row is skipped with a `ParseError` added to `Errors` and all other rows in the section are returned normally.
4. **Given** a CSV with a positive WHT amount (IBKR normally emits negative values), **When** parsed, **Then** a `ParseError` with `Code = "WHT_POSITIVE_AMOUNT"` is added to `Errors` and that row is excluded from `Withholdings`.
5. **Given** a duplicate exchange rate entry for the same `(Date, FromCurrency, ToCurrency)`, **When** parsed, **Then** the last encountered value is used AND a `ParseError` with `Code = "RATE_DUPLICATE"` is added to `Errors`.
6. **Given** a CSV where an exchange rate `Rate` value is zero or negative, **When** parsed, **Then** a `ParseError` with `Code = "RATE_NON_POSITIVE"` is added to `Errors` and that rate is excluded from `EmbeddedRates`.

---

### User Story 3 — Handle unrecoverable / structurally invalid input (Priority: P3)

The input stream is null, unreadable, or does not contain any recognisable IBKR section headers. The parser returns `Result.Failure` without throwing.

**Why this priority**: Defensive error handling is essential for a desktop application where the user may accidentally select the wrong file.

**Independent Test**: Call `ParseAsync` with an empty stream, a stream containing random text, and a null-equivalent scenario. Assert `result.IsSuccess == false` with the expected error code in each case.

**Acceptance Scenarios**:

1. **Given** a stream that cannot be read (e.g., closed/disposed before passing), **When** `ParseAsync` is called, **Then** `result.IsSuccess == false` AND `result.Error.Code == "STREAM_ERROR"`.
2. **Given** a stream containing plain text with no IBKR section headers, **When** `ParseAsync` is called, **Then** `result.IsSuccess == false` AND `result.Error.Code == "INVALID_FORMAT"`.
3. **Given** any input, **When** `ParseAsync` is called, **Then** no exception is thrown from `ParseAsync` under any condition — all exceptional states are communicated via `Result.Failure` or `ParseError`.

---

### Edge Cases

- A section header is present but contains zero data rows → section returns an empty list; not an error.
- `Total` and `Notes` rows within any section are silently skipped.
- An unknown/unrecognised section name in the CSV is silently skipped (forward-compatibility).
- Interest rows that are neither "Credit Interest" nor "Debit Interest" (e.g., "PIK Interest") are silently skipped.
- Multiple Credit Interest rows for the same `(Currency, Date)` are summed into one `InterestRecord`.
- An ISIN-stripped `EntityName` that is empty or whitespace-only results in a `ParseError` and the row is skipped.
- A `CancellationToken` cancellation mid-parse returns `Result.Failure(Error("CANCELLED", ...))` without throwing `OperationCanceledException`.

---

## Requirements

### Functional Requirements

**Interface contract**

- **FR-001**: The system MUST expose an `IStatementParser` interface in `Rentier.Application/Interfaces/` with a single method `ParseAsync(Stream csvStream, CancellationToken cancellationToken)` returning `Task<Result<StatementParseResult, Error>>`.
- **FR-002**: `IStatementParser` MUST be implemented by a concrete class registered in the Infrastructure layer. The Application layer MUST NOT contain any CSV-parsing logic.
- **FR-003**: `ParseAsync` MUST never throw an exception. All error states MUST be communicated via `Result.Failure(Error)` (unrecoverable) or a `ParseError` entry in `StatementParseResult.Errors` (recoverable).

**Section parsing**

- **FR-004**: The parser MUST process the **Dividends** section and produce `DividendRecord` instances for each valid data row.
- **FR-005**: The parser MUST process the **Withholding Tax** section and produce `WithholdingTaxRecord` instances for rows successfully matched to a dividend.
- **FR-006**: The parser MUST process the **Interest** section and produce `InterestRecord` instances for rows whose description contains `"Credit Interest"` or `"Debit Interest"`. All other interest row descriptions MUST be silently skipped.
- **FR-007**: The parser MUST process the **Base Currency Exchange Rate** section and produce `IbkrExchangeRate` instances for each valid rate row.
- **FR-008**: Section rows with `row-type == "Total"` or `row-type == "Notes"` MUST be silently skipped in all sections.
- **FR-009**: Any section not listed in FR-004–FR-007 MUST be silently skipped.

**Aggregation**

- **FR-010**: Dividend rows sharing the same `(Date, EntityName, Currency)` key MUST be summed into a single `DividendRecord`. Rows with different currencies under the same entity and date produce separate records.
- **FR-011**: Interest rows of the same `InterestType` sharing the same `(Date, Currency)` key MUST be summed into a single `InterestRecord`. Credit and Debit records MUST remain separate even if they share the same date and currency.
- **FR-012**: Exchange rates with duplicate `(Date, FromCurrency, ToCurrency)` keys MUST retain the last-encountered value and emit a `ParseError("RATE_DUPLICATE", ...)`.

**Entity name extraction**

- **FR-013**: The `EntityName` for Dividend, WHT, and Interest rows MUST be derived by stripping the ISIN pattern `(XX#########0)` (where `XX` is a two-letter country code, followed by 9 alphanumeric characters, followed by one digit) from the description field and trimming surrounding whitespace.
- **FR-014**: ISIN stripping MUST be applied identically to both Dividend and WHT description fields so that the `EntityName` used for matching is deterministic and consistent.
- **FR-015**: If ISIN stripping produces an empty or whitespace-only `EntityName`, the row MUST be skipped and a `ParseError` added to `Errors`.

**WHT matching**

- **FR-016**: Each WHT row MUST be matched to a dividend by the composite key `(Date, EntityName, Currency)`. A match means a `WithholdingTaxRecord` is produced.
- **FR-017**: If a WHT row matches a dividend by `(Date, EntityName)` but the currencies differ, the parser MUST add a `ParseError` with `Code = "WHT_CURRENCY_MISMATCH"` and skip the WHT row (not add it to `Withholdings`).
- **FR-018**: If a WHT row has no matching dividend at all, the parser MUST add a `ParseError` with `Code = "WHT_UNMATCHED"` and skip the WHT row.
- **FR-019**: WHT amounts in the CSV are negative values. The parser MUST store `WithholdingTaxRecord.Amount` as the absolute (positive) value. A WHT row with a positive amount in the CSV MUST be rejected with `ParseError("WHT_POSITIVE_AMOUNT", ...)`.

**Exchange rate validation**

- **FR-020**: An `IbkrExchangeRate` entry with a `Rate` value ≤ 0 MUST NOT be added to `EmbeddedRates`. A `ParseError` with `Code = "RATE_NON_POSITIVE"` MUST be added to `Errors`.

**Unrecoverable failures**

- **FR-021**: If the input stream is null, disposed, or otherwise unreadable, `ParseAsync` MUST return `Result.Failure(new Error("STREAM_ERROR", ...))`.
- **FR-022**: If the CSV contains no recognisable IBKR section headers after full traversal, `ParseAsync` MUST return `Result.Failure(new Error("INVALID_FORMAT", ...))`.
- **FR-023**: If a CancellationToken is cancelled, `ParseAsync` MUST return `Result.Failure(new Error("CANCELLED", ...))` without propagating `OperationCanceledException`.

**Dependencies**

- **FR-024**: The CSV parsing library MUST be added to `Rentier.Infrastructure.csproj` only. It MUST NOT be referenced by `Rentier.Application` or `Rentier.Domain`.
- **FR-025**: No Entity Framework migrations or `DbContext` modifications are required or permitted for this feature.

---

### Non-Functional Requirements

- **NFR-001 (Correctness)**: The parser MUST produce amounts as `decimal` values with no floating-point rounding. All monetary fields (`Amount`, `Rate`) use `decimal` arithmetic throughout.
- **NFR-002 (Date handling)**: All date fields MUST use `DateOnly`. The expected date format from IBKR CSVs is `yyyy-MM-dd`. A row with an unparseable date MUST be skipped with a `ParseError`.
- **NFR-003 (Thread safety)**: The `IStatementParser` implementation MUST be stateless and safe to use as a transient service (a new instance per call).
- **NFR-004 (No persistence)**: `ParseAsync` MUST NOT write to any database, file system, or external service. It is a pure in-memory transformation.
- **NFR-005 (Test coverage)**: The Infrastructure parser implementation MUST achieve ≥ 90% line coverage in `Rentier.Infrastructure.Tests`. Internal helper methods (e.g., ISIN stripping) MUST be accessible for direct testing via `InternalsVisibleTo`.
- **NFR-006 (Encoding)**: The parser assumes UTF-8 encoding for IBKR CSV files. No BOM stripping or fallback encoding is required.

---

### Constitution Alignment

- **CA-001 (Architecture)**: This feature touches Application (interfaces + DTOs) and Infrastructure (implementation). Domain is unchanged. Clean Architecture flow: Infrastructure → Application. The `IStatementParser` interface in Application is implemented by `IbkrCsvParser` in Infrastructure — no dependency inversion violation.
- **CA-002 (Money and Dates)**: All monetary fields (`Amount`, `Rate`) use `decimal`. All date fields use `DateOnly`. No `DateTime`, `float`, or `double` is permitted.
- **CA-003 (Privacy and Security)**: CSV processing is entirely local and in-memory. No data is transmitted externally.
- **CA-004 (Network Scope)**: No outbound network calls. This feature is entirely local.
- **CA-005 (Async and UI)**: `ParseAsync` returns `Task<...>` to satisfy the interface contract. The implementation may complete synchronously but MUST NOT block any UI thread. No Avalonia or Desktop references.
- **CA-006 (Testing Impact)**: New tests required in `Rentier.Infrastructure.Tests/Parsing/`. CSV fixture files embedded as resources in that project. No existing test suites are modified.

---

## Data Model

All types listed below are **new Application-layer DTOs** in `Rentier.Application/Parsing/`. None modify or extend existing Domain value objects.

### `StatementParseResult`

The top-level result container returned by a successful `ParseAsync` call.

| Property       | Type                                  | Description                                                          |
|----------------|---------------------------------------|----------------------------------------------------------------------|
| `Dividends`    | `IReadOnlyList<DividendRecord>`       | Aggregated dividend records extracted from the Dividends section     |
| `Interest`     | `IReadOnlyList<InterestRecord>`       | Credit and Debit interest records (kept separate)                    |
| `Withholdings` | `IReadOnlyList<WithholdingTaxRecord>` | Matched WHT records (positive amounts)                               |
| `EmbeddedRates`| `IReadOnlyList<IbkrExchangeRate>`     | Exchange rates from the Base Currency Exchange Rate section          |
| `Errors`       | `IReadOnlyList<ParseError>`           | Recoverable warnings and anomalies encountered during parsing        |

A caller MUST check `Errors.Count == 0` to confirm a fully clean parse before proceeding to filing creation.

---

### `DividendRecord`

Represents a net dividend received from a single entity on a single date in a single currency, after aggregation.

| Property     | Type       | Description                                           |
|--------------|------------|-------------------------------------------------------|
| `Date`       | `DateOnly` | Ex-dividend or payment date from the CSV              |
| `Currency`   | `string`   | ISO 4217 currency code (e.g., `"USD"`, `"EUR"`)       |
| `EntityName` | `string`   | ISIN-stripped issuer name (e.g., `"AAPL"`)            |
| `Amount`     | `decimal`  | Gross dividend amount (positive); sum of all matching rows |

Aggregation key: `(Date, EntityName, Currency)`.

---

### `WithholdingTaxRecord`

Represents withholding tax deducted on a dividend payment, matched to a `DividendRecord` by the composite key.

| Property     | Type       | Description                                                     |
|--------------|------------|-----------------------------------------------------------------|
| `Date`       | `DateOnly` | Date matching the associated dividend                           |
| `Currency`   | `string`   | ISO 4217 currency code — must match the associated dividend     |
| `EntityName` | `string`   | ISIN-stripped issuer name — must match the associated dividend  |
| `Amount`     | `decimal`  | WHT amount as a positive value (CSV negative values converted)  |

WHT rows with no matching dividend or with a currency mismatch are not stored — they produce `ParseError` entries.

---

### `InterestRecord`

Represents a credit (received) or debit (paid) interest entry. Credit and Debit records are always kept separate.

| Property     | Type           | Description                                                          |
|--------------|----------------|----------------------------------------------------------------------|
| `Date`       | `DateOnly`     | Date of the interest event                                           |
| `Currency`   | `string`       | ISO 4217 currency code                                               |
| `EntityName` | `string`       | Always `"Interactive Brokers"` for IBKR interest rows               |
| `Amount`     | `decimal`      | Always positive; sign semantics conveyed by `Type`                   |
| `Type`       | `InterestType` | `Credit` for received interest; `Debit` for paid/charged interest   |

Aggregation key: `(Date, Currency, Type)`. Interest rows not matching "Credit Interest" or "Debit Interest" in their description are silently skipped.

---

### `InterestType` (enum)

| Value    | Meaning                                             |
|----------|-----------------------------------------------------|
| `Credit` | Interest received from IBKR (taxable passive income)|
| `Debit`  | Interest paid to IBKR (e.g., margin interest)       |

---

### `IbkrExchangeRate`

Represents one exchange rate entry from the IBKR Base Currency Exchange Rate section. Distinct from the Domain `ExchangeRate` value object (which stores NBS RSD rates).

| Property       | Type       | Description                                             |
|----------------|------------|---------------------------------------------------------|
| `Date`         | `DateOnly` | Date of the rate                                        |
| `FromCurrency` | `string`   | Left-hand ISO 4217 code (e.g., `"EUR"`)                 |
| `ToCurrency`   | `string`   | Right-hand ISO 4217 code (currently always `"USD"`)     |
| `Rate`         | `decimal`  | Positive exchange rate (e.g., `1.0943` for EUR/USD)     |

No aggregation — one rate per `(Date, FromCurrency, ToCurrency)`. Duplicates: last value wins + `ParseError("RATE_DUPLICATE", ...)`.

---

### `ParseError`

Represents a recoverable anomaly encountered during parsing. Populates `StatementParseResult.Errors`.

| Property    | Type     | Description                                                  |
|-------------|----------|--------------------------------------------------------------|
| `Code`      | `string` | Machine-readable error code (see Error Code Reference below) |
| `Message`   | `string` | Human-readable description including row context             |
| `RowNumber` | `int?`   | Optional 1-based CSV row number where the error occurred     |

---

## Parsing Algorithm

### Section Detection

IBKR CSV files are multi-section. Each section is identified by a row where:
- Column 0 = section name (e.g., `"Dividends"`, `"Withholding Tax"`, `"Interest"`, `"Base Currency Exchange Rate"`)
- Column 1 = row type (`"Header"`, `"Data"`, `"Total"`, `"Notes"`, `"Subtitle"`)

The parser reads all rows sequentially, inspects columns 0 and 1, and dispatches to the appropriate section accumulator. `Total` and `Notes` rows are always skipped. Unknown section names are skipped entirely.

### Dividends Section

1. For each `Data` row in the `Dividends` section:
   a. Parse `Date` from the date column using format `yyyy-MM-dd`. On failure → `ParseError`, skip row.
   b. Read `Currency` from the currency column.
   c. Read the description field; apply ISIN stripping to derive `EntityName`. If result is empty → `ParseError`, skip row.
   d. Parse `Amount` as `decimal`. On failure → `ParseError`, skip row.
   e. Accumulate into a dictionary keyed on `(Date, EntityName, Currency)`, summing `Amount` for duplicate keys.
2. Convert dictionary values to `IReadOnlyList<DividendRecord>`.

### Withholding Tax Section

1. For each `Data` row in the `Withholding Tax` section:
   a. Parse `Date`, `Currency`, `EntityName` (with ISIN stripping), and `Amount` — on any parse failure → `ParseError`, skip row.
   b. Assert `Amount` is negative in CSV. If positive → `ParseError("WHT_POSITIVE_AMOUNT", ...)`, skip row.
   c. Convert amount to positive (`Math.Abs`).
   d. Look up `(Date, EntityName, Currency)` in the Dividends dictionary:
      - Match found, currencies match → create `WithholdingTaxRecord`.
      - Match found on `(Date, EntityName)` but currency differs → `ParseError("WHT_CURRENCY_MISMATCH", ...)`, skip.
      - No match → `ParseError("WHT_UNMATCHED", ...)`, skip.
2. Collect matched records into `IReadOnlyList<WithholdingTaxRecord>`.

> **Note**: WHT section is processed after the Dividends section has been fully accumulated, because matching requires the completed Dividends dictionary.

### Interest Section

1. For each `Data` row in the `Interest` section:
   a. Read description. If it does not contain `"Credit Interest"` or `"Debit Interest"` → silently skip.
   b. Determine `InterestType`: `Credit` for `"Credit Interest"`, `Debit` for `"Debit Interest"`.
   c. Parse `Date`, `Currency`, and `Amount` — on any parse failure → `ParseError`, skip row.
   d. Assert `Amount` is positive (Interest rows for credit/debit should have positive amounts in the CSV per IBKR format). If the raw CSV amount is negative, take its absolute value (to match the "always positive Amount" contract).
   e. Set `EntityName = "Interactive Brokers"`.
   f. Accumulate into a dictionary keyed on `(Date, Currency, InterestType)`, summing `Amount`.
2. Convert dictionary values to `IReadOnlyList<InterestRecord>`.

### Base Currency Exchange Rate Section

1. For each `Data` row in the `Base Currency Exchange Rate` section:
   a. Parse `Date` using `yyyy-MM-dd`. On failure → `ParseError`, skip row.
   b. Read `FromCurrency` and `ToCurrency` from the currency pair description.
   c. Parse `Rate` as `decimal`. On failure → `ParseError`, skip row.
   d. Validate `Rate > 0`. If not → `ParseError("RATE_NON_POSITIVE", ...)`, skip row.
   e. Check for duplicate key `(Date, FromCurrency, ToCurrency)`:
      - If duplicate → overwrite with current value AND emit `ParseError("RATE_DUPLICATE", ...)`.
      - Otherwise → add to dictionary.
2. Convert dictionary values to `IReadOnlyList<IbkrExchangeRate>`.

---

## Error Code Reference

| Code                    | Severity    | Trigger                                                              | Effect                                 |
|-------------------------|-------------|----------------------------------------------------------------------|----------------------------------------|
| `STREAM_ERROR`          | Fatal       | Input stream is null, disposed, or unreadable                        | `Result.Failure`                       |
| `INVALID_FORMAT`        | Fatal       | No recognisable IBKR section headers found in CSV                   | `Result.Failure`                       |
| `PARSE_EXCEPTION`       | Fatal       | CSV library throws an unhandled exception                            | `Result.Failure`                       |
| `CANCELLED`             | Fatal       | `CancellationToken` is cancelled during parsing                      | `Result.Failure`                       |
| `WHT_UNMATCHED`         | Warning     | WHT row has no matching dividend by `(Date, EntityName, Currency)`  | Row skipped; added to `Errors`         |
| `WHT_CURRENCY_MISMATCH` | Warning     | WHT row matches dividend by `(Date, EntityName)` but currencies differ | Row skipped; added to `Errors`       |
| `WHT_POSITIVE_AMOUNT`   | Warning     | WHT row has a positive amount (should be negative in IBKR CSV)       | Row skipped; added to `Errors`         |
| `RATE_NON_POSITIVE`     | Warning     | Exchange rate value is zero or negative                              | Row skipped; added to `Errors`         |
| `RATE_DUPLICATE`        | Warning     | Duplicate `(Date, FromCurrency, ToCurrency)` exchange rate key       | Last value wins; added to `Errors`     |
| `MALFORMED_ROW`         | Warning     | Date or amount field in a data row cannot be parsed                  | Row skipped; added to `Errors`         |
| `EMPTY_ENTITY_NAME`     | Warning     | ISIN stripping produces empty/whitespace `EntityName`               | Row skipped; added to `Errors`         |

---

## Success Criteria

### Measurable Outcomes

- **SC-001**: Given a representative IBKR CSV fixture with 20 dividend rows, 18 WHT rows (2 mismatched), 4 interest rows (2 credit, 2 debit), and 3 exchange rate rows, `ParseAsync` returns a success result with `Dividends.Count ≥ 1`, `Withholdings.Count ≥ 1`, `Interest.Count == 2` (one Credit, one Debit), `EmbeddedRates.Count == 3`, and `Errors.Count == 2`.
- **SC-002**: All four record types have correct aggregation: no duplicate `(Date, EntityName, Currency)` keys in `Dividends`, no duplicate `(Date, Currency, Type)` keys in `Interest`, no duplicate `(Date, FromCurrency, ToCurrency)` in `EmbeddedRates`.
- **SC-003**: `ParseAsync` completes in under 1 second for a CSV file containing 500 data rows across all sections on standard developer hardware.
- **SC-004**: `ParseAsync` never throws an exception for any input — verified by automated tests covering null stream, empty stream, random-text stream, and a stream cancelled mid-parse.
- **SC-005**: All `Amount` and `Rate` values in the result are positive `decimal` values — no negative values appear in any record collection.
- **SC-006**: `WithholdingTaxRecord.EntityName` and `DividendRecord.EntityName` are identical for matched pairs — the matching key is applied consistently.
- **SC-007**: Infrastructure test project achieves ≥ 90% line coverage on the parser implementation, as reported by the CI coverage tool.

---

## File Locations

### New Files — Application Layer

```
src/Rentier.Application/Interfaces/IStatementParser.cs
src/Rentier.Application/Parsing/StatementParseResult.cs
src/Rentier.Application/Parsing/DividendRecord.cs
src/Rentier.Application/Parsing/WithholdingTaxRecord.cs
src/Rentier.Application/Parsing/InterestRecord.cs
src/Rentier.Application/Parsing/InterestType.cs
src/Rentier.Application/Parsing/IbkrExchangeRate.cs
src/Rentier.Application/Parsing/ParseError.cs
```

### New Files — Infrastructure Layer

```
src/Rentier.Infrastructure/Parsing/IbkrCsvParser.cs
src/Rentier.Infrastructure/Parsing/IbkrCsvParser.Dividends.cs
src/Rentier.Infrastructure/Parsing/IbkrCsvParser.WithholdingTax.cs
src/Rentier.Infrastructure/Parsing/IbkrCsvParser.Interest.cs
src/Rentier.Infrastructure/Parsing/IbkrCsvParser.ExchangeRates.cs
```

### New Files — Tests

```
tests/Rentier.Infrastructure.Tests/Parsing/IbkrCsvParserTests.cs
tests/Rentier.Infrastructure.Tests/Parsing/Fixtures/happy_path.csv
tests/Rentier.Infrastructure.Tests/Parsing/Fixtures/wht_currency_mismatch.csv
tests/Rentier.Infrastructure.Tests/Parsing/Fixtures/interest_debit_credit.csv
tests/Rentier.Infrastructure.Tests/Parsing/Fixtures/malformed_row.csv
tests/Rentier.Infrastructure.Tests/Parsing/Fixtures/duplicate_dividend_same_date.csv
```

CSV fixture files are embedded resources (`Build Action: EmbeddedResource`) loaded via `Assembly.GetManifestResourceStream(...)`.

### Modified Files

- `src/Rentier.Infrastructure/Rentier.Infrastructure.csproj` — add CsvHelper NuGet package reference
- `src/Rentier.Infrastructure/InfrastructureServiceExtensions.cs` — register `IStatementParser → IbkrCsvParser` as Transient
- `src/Rentier.Infrastructure/Rentier.Infrastructure.csproj` — add `InternalsVisibleTo("Rentier.Infrastructure.Tests")`

---

## Assumptions

1. IBKR CSV files are UTF-8 encoded without BOM. No encoding fallback is required.
2. Date values in all IBKR sections use the format `yyyy-MM-dd` exclusively. No alternative formats are handled.
3. `"Credit Interest"` and `"Debit Interest"` substrings are matched case-sensitively, as IBKR descriptions are consistently formatted.
4. Only the text preceding the `(ISIN)` token in a description is used as `EntityName`. Any text following the closing `)` is discarded.
5. IBKR CSV `Amount` fields use plain decimal notation without thousands separators. Standard decimal parsing is sufficient.
6. `IbkrExchangeRate.ToCurrency` is always `"USD"` in the current IBKR Base Currency Exchange Rate format. The field is retained explicitly for self-documentation and future resilience.
7. No `async` I/O beyond the initial stream acquisition is required; the `ParseAsync` method is `async Task<...>` for interface compliance but may complete synchronously.
8. The parser implementation is stateless and thread-safe; it is registered as `Transient` in the DI container.
9. `InternalsVisibleTo("Rentier.Infrastructure.Tests")` is added to `Rentier.Infrastructure.csproj` to allow direct testing of internal ISIN stripping and other internal helpers.

---

## Out of Scope

| Item                                      | Reason                                                               |
|-------------------------------------------|----------------------------------------------------------------------|
| Currency → RSD conversion                 | `IbkrExchangeRate` provides raw rates; conversion via NBS (Feature 006) is a caller responsibility |
| `Report` entity creation or persistence   | A future feature builds the `Report` aggregate from `StatementParseResult`; this feature stops at parsing |
| Desktop UI (file picker, progress bar)    | `IStatementParser` is headless; no Avalonia components in this feature |
| IBKR sections beyond the four specified   | Unknown sections are silently skipped for forward-compatibility      |
| ISIN validation against an external registry | ISIN stripping is a local regex operation only                   |
| Multi-currency dividend netting           | Each currency is a separate `DividendRecord` by design               |
| EF migrations or database schema changes  | This feature has no persistence layer                                |
| Bulk import pipeline / `IReportImporter`  | A future feature that composes `IStatementParser` into a broader workflow |
