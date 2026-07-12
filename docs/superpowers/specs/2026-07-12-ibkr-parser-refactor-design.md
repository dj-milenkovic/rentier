# IbkrCsvParser Refactor — Design Spec
_Date: 2026-07-12_

## Problem

`IbkrCsvParser.cs` fails two SonarQube quality gate checks:

1. **Duplicate code** — the same 6-step boilerplate (filter section → filter data row →
   check minimum field count → skip total rows → parse date → parse decimal amount)
   is repeated verbatim across all four `ParseXxx` private methods.

2. **Cognitive complexity** — `ParseWithholdingTax` exceeds SonarQube's threshold (15)
   because it mixes row-iteration/parsing concerns with three-way dividend-matching logic
   in a single method body.

## Constraints

- Single-file refactor: `IbkrCsvParser.cs` stays one class, no new files.
- Public API is unchanged: `ParseAsync(Stream, CancellationToken)` signature and all
  error codes (`ROW_DATE_INVALID`, `ROW_AMOUNT_INVALID`, `WHT_UNMATCHED`, etc.) remain identical.
- No new tests required: existing test suite (unit + snapshot + integration) covers
  every branch of the refactored code indirectly.

## Architecture

### Helpers extracted

Four new `private static` methods, added at the bottom of the class:

```csharp
// True when the row belongs to the named section and column 1 == "Data".
private static bool IsDataRow(string[] record, string sectionName)

// True when column 2 starts with "Total" (summary/aggregate rows to skip).
private static bool IsTotalRow(string[] record)

// Parses record[fieldIndex] as DateOnly (yyyy-MM-dd); appends ROW_DATE_INVALID on failure.
private static bool TryParseDate(
    string[] record, int fieldIndex, int rowIndex,
    List<ParseError> errors, out DateOnly date)

// Parses a raw string as decimal; appends the given errorCode on failure.
private static bool TryParseDecimal(
    string raw, string errorCode, string context,
    int rowIndex, List<ParseError> errors, out decimal value)
```

Each existing `ParseXxx` method becomes a lean filter-and-dispatch loop:
guard with `IsDataRow` / `IsTotalRow`, call `TryParseDate` / `TryParseDecimal`,
then execute its section-specific business logic.

### ParseWithholdingTax split

The method is split at the point where parsing ends and matching begins:

```
ParseWithholdingTax(rows, dividends)
  → iterates rows; applies IsDataRow / IsTotalRow / TryParseDate / TryParseDecimal
  → rawAmount > 0 guard (parse-time rejection, stays here)
  → for each valid WHT row, calls AccumulateOrErrorWht(...)

AccumulateOrErrorWht(entity, date, currency, absAmount, rowIndex,
                     dividends, accumulator, errors)
  → exact key matches a dividend  → accumulate in accumulator dict
  → same entity+date, different currency → WHT_CURRENCY_MISMATCH error
  → no matching dividend at all   → WHT_UNMATCHED error
```

`AccumulateOrErrorWht` is `private static`; it does no parsing.

**Cognitive complexity targets:**
- `ParseWithholdingTax` after split: ~8 (loop + 4 guards + delegate call)
- `AccumulateOrErrorWht`: ~5 (3-way branch)
- All other `ParseXxx` methods: ~7–9 each

All well under SonarQube's threshold of 15.

## Data Flow

```
ParseAsync
  ├─ ReadRowsAsync          (unchanged)
  ├─ ParseDividends         → uses IsDataRow, IsTotalRow, TryParseDate, TryParseDecimal
  ├─ ParseWithholdingTax    → uses same helpers + AccumulateOrErrorWht
  ├─ ParseInterest          → uses IsDataRow, TryParseDate, TryParseDecimal
  └─ ParseExchangeRates     → uses IsDataRow, TryParseDate, TryParseDecimal
```

## Error Handling

No change to error codes, messages, or the `Result<T, Error>` pattern. The helpers
surface errors through the existing `List<ParseError> errors` out-parameter pattern —
callers add to the list and `continue`, identical to today.

## Testing Plan

No new tests. Run both tiers after the refactor:

1. **Unit + snapshot**: `dotnet test Rentier.slnx --filter "Category!=Integration"`
   — covers all `IbkrCsvParserTests` and `IbkrCsvParserSnapshotTests`.

2. **Infrastructure integration**: `dotnet test tests/Rentier.Infrastructure.Tests --filter "Category=Integration"`
   — ensures no breakage in the broader infrastructure layer.

Both must be green before merging.
