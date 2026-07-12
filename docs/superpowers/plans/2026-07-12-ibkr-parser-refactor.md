# IbkrCsvParser Refactor — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Refactor `IbkrCsvParser.cs` to eliminate repeated row-guard/parse boilerplate and reduce cyclomatic complexity in `ParseWithholdingTax`, clearing two SonarQube quality gate failures without changing any public behaviour.

**Architecture:** Extract four `private static` helpers (`IsDataRow`, `IsTotalRow`, `TryParseDate`, `TryParseDecimal`) that replace identical boilerplate repeated across all four `ParseXxx` methods. Split `ParseWithholdingTax` into a lean iteration loop that calls a focused `AccumulateOrErrorWht` method for the 3-way dividend-matching logic, bringing cognitive complexity below SonarQube's threshold of 15.

**Tech Stack:** C# 12, .NET 9, CsvHelper 33, xUnit + FluentAssertions + Verify (snapshot testing).

## Global Constraints

- `decimal` only for all monetary values and exchange rates — never `double` or `float`.
- `DateOnly` for all dates — no `DateTime`.
- Public signature is frozen: `ParseAsync(Stream, CancellationToken)` returns `Result<StatementParseResult, Error>`.
- All existing error codes must be preserved verbatim: `ROW_DATE_INVALID`, `ROW_AMOUNT_INVALID`, `ROW_TOO_SHORT`, `WHT_POSITIVE_AMOUNT`, `WHT_CURRENCY_MISMATCH`, `WHT_UNMATCHED`, `RATE_NON_POSITIVE`, `RATE_DUPLICATE`, `INVALID_FORMAT`, `PARSE_EXCEPTION`.
- No new files — all changes are inside `src/Rentier.Infrastructure/Parsing/IbkrCsvParser.cs`.
- No new tests — existing tests provide full branch coverage.

---

### Task 1: Establish green baseline

**Files:**
- Run only — no edits.

- [ ] **Step 1: Build the solution**

```
dotnet build Rentier.slnx --no-restore -c Release
```

Expected: `Build succeeded.` with 0 errors.

- [ ] **Step 2: Run unit + snapshot tests**

```
dotnet test Rentier.slnx --filter "Category!=Integration" -c Release
```

Expected: all tests pass, 0 failures.

- [ ] **Step 3: Run infrastructure integration tests**

```
dotnet test tests/Rentier.Infrastructure.Tests --filter "Category=Integration" -c Release
```

Expected: all tests pass, 0 failures.

---

### Task 2: Extract `IsDataRow` and `IsTotalRow` — apply to all four section parsers

**Files:**
- Modify: `src/Rentier.Infrastructure/Parsing/IbkrCsvParser.cs`

**Interfaces:**
- Produces: `IsDataRow(string[] record, string sectionName) → bool`
- Produces: `IsTotalRow(string[] record) → bool`

- [ ] **Step 1: Add the two helpers at the bottom of the class** (before the final `}`)

```csharp
    private static bool IsDataRow(string[] record, string sectionName)
        => record.Length >= 2
           && record[0].Equals(sectionName, StringComparison.OrdinalIgnoreCase)
           && record[1].Equals("Data", StringComparison.OrdinalIgnoreCase);

    private static bool IsTotalRow(string[] record)
        => record.Length >= 3
           && record[2].Trim().StartsWith("Total", StringComparison.OrdinalIgnoreCase);
```

- [ ] **Step 2: Replace section + data guards in `ParseDividends`**

The two existing guards (lines ~115–118):
```csharp
// REMOVE these two blocks:
if (record.Length < 1 || !string.Equals(record[0], "Dividends", StringComparison.OrdinalIgnoreCase))
    continue;
if (record.Length < 2 || !string.Equals(record[1], "Data", StringComparison.OrdinalIgnoreCase))
    continue;
```

Replace with:
```csharp
if (!IsDataRow(record, "Dividends")) continue;
```

Also replace the Total-row guard (lines ~126–128):
```csharp
// REMOVE:
var currency = record[2].Trim();
// Skip summary/total rows (e.g. "Dividends,Data,Total,,,0.35")
if (currency.StartsWith("Total", StringComparison.OrdinalIgnoreCase))
    continue;
```

Replace with:
```csharp
if (IsTotalRow(record)) continue;
var currency = record[2].Trim();
```

- [ ] **Step 3: Replace section + data guards in `ParseWithholdingTax`**

The two existing guards (lines ~169–172):
```csharp
// REMOVE:
if (record.Length < 1 || !string.Equals(record[0], "Withholding Tax", StringComparison.OrdinalIgnoreCase))
    continue;
if (record.Length < 2 || !string.Equals(record[1], "Data", StringComparison.OrdinalIgnoreCase))
    continue;
```

Replace with:
```csharp
if (!IsDataRow(record, "Withholding Tax")) continue;
```

Also replace the Total-row guard (lines ~179–181):
```csharp
// REMOVE:
var currency = record[2].Trim();
if (currency.StartsWith("Total", StringComparison.OrdinalIgnoreCase))
    continue;
```

Replace with:
```csharp
if (IsTotalRow(record)) continue;
var currency = record[2].Trim();
```

- [ ] **Step 4: Replace section + data guards in `ParseInterest`**

```csharp
// REMOVE:
if (record.Length < 1 || !string.Equals(record[0], "Interest", StringComparison.OrdinalIgnoreCase))
    continue;
if (record.Length < 2 || !string.Equals(record[1], "Data", StringComparison.OrdinalIgnoreCase))
    continue;
```

Replace with:
```csharp
if (!IsDataRow(record, "Interest")) continue;
```

(`ParseInterest` has no Total-row skip — leave the rest of the method unchanged.)

- [ ] **Step 5: Replace section + data guards in `ParseExchangeRates`**

```csharp
// REMOVE:
if (record.Length < 1 || !string.Equals(record[0], "Base Currency Exchange Rate", StringComparison.OrdinalIgnoreCase))
    continue;
if (record.Length < 2 || !string.Equals(record[1], "Data", StringComparison.OrdinalIgnoreCase))
    continue;
```

Replace with:
```csharp
if (!IsDataRow(record, "Base Currency Exchange Rate")) continue;
```

- [ ] **Step 6: Run tests**

```
dotnet test Rentier.slnx --filter "Category!=Integration" -c Release
```

Expected: all tests pass.

- [ ] **Step 7: Commit**

```
git add src/Rentier.Infrastructure/Parsing/IbkrCsvParser.cs
git commit -m "refactor(parser): extract IsDataRow and IsTotalRow helpers"
```

---

### Task 3: Extract `TryParseDate` and `TryParseDecimal` — apply to all four section parsers

**Files:**
- Modify: `src/Rentier.Infrastructure/Parsing/IbkrCsvParser.cs`

**Interfaces:**
- Consumes: `IsDataRow`, `IsTotalRow` (Task 2)
- Produces: `TryParseDate(string raw, int rowIndex, string contextLabel, List<ParseError> errors, out DateOnly date) → bool`
- Produces: `TryParseDecimal(string raw, int rowIndex, string errorCode, string valueLabel, List<ParseError> errors, out decimal value) → bool`

The `contextLabel` parameter in `TryParseDate` is a prefix injected into the error message so each section preserves its original wording:
- `""` → `"Cannot parse date '…'."` (Dividends)
- `"WHT "` → `"Cannot parse WHT date '…'."` (Withholding Tax)
- `"interest "` → `"Cannot parse interest date '…'."` (Interest)
- `"FX rate "` → `"Cannot parse FX rate date '…'."` (Exchange Rates)

The `valueLabel` parameter in `TryParseDecimal` likewise preserves per-section wording:
- `"amount"` → `"Cannot parse amount '…'."` (Dividends)
- `"WHT amount"` → `"Cannot parse WHT amount '…'."` (Withholding Tax)
- `"interest amount"` → `"Cannot parse interest amount '…'."` (Interest)
- `"FX rate"` → `"Cannot parse FX rate '…'."` (Exchange Rates)

- [ ] **Step 1: Add the two helpers after `IsTotalRow`**

```csharp
    private static bool TryParseDate(
        string raw, int rowIndex, string contextLabel,
        List<ParseError> errors, out DateOnly date)
    {
        if (DateOnly.TryParseExact(raw, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out date))
            return true;
        errors.Add(new ParseError("ROW_DATE_INVALID",
            $"Cannot parse {contextLabel}date '{raw}'.", rowIndex));
        return false;
    }

    private static bool TryParseDecimal(
        string raw, int rowIndex, string errorCode, string valueLabel,
        List<ParseError> errors, out decimal value)
    {
        if (decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out value))
            return true;
        errors.Add(new ParseError(errorCode,
            $"Cannot parse {valueLabel} '{raw}'.", rowIndex));
        return false;
    }
```

- [ ] **Step 2: Replace `ParseDividends` with the updated version**

```csharp
private static (Dictionary<(string Entity, DateOnly Date, string Currency), DividendRecord> dividends,
                List<ParseError> errors)
    ParseDividends(List<string[]> rows)
{
    var dict = new Dictionary<(string, DateOnly, string), DividendRecord>();
    var errors = new List<ParseError>();
    int rowIndex = 0;

    foreach (var record in rows)
    {
        rowIndex++;
        if (!IsDataRow(record, "Dividends")) continue;
        if (record.Length < 6)
        {
            errors.Add(new ParseError("ROW_TOO_SHORT",
                $"Dividends row has {record.Length} fields, expected >=6.", rowIndex));
            continue;
        }
        if (IsTotalRow(record)) continue;

        var currency = record[2].Trim();
        var description = record[4].Trim();

        if (!TryParseDate(record[3].Trim(), rowIndex, "", errors, out var date)) continue;
        if (!TryParseDecimal(record[5].Trim(), rowIndex, "ROW_AMOUNT_INVALID", "amount", errors, out var amount)) continue;

        var entity = StripIsin(description);
        var key = (entity, date, currency);

        if (dict.TryGetValue(key, out var existing))
            dict[key] = existing with { Amount = existing.Amount + amount };
        else
            dict[key] = new DividendRecord(date, currency, entity, amount);
    }

    return (dict, errors);
}
```

- [ ] **Step 3: Replace `ParseWithholdingTax` with the updated version**

(Keep the match-logic block intact — `AccumulateOrErrorWht` is extracted in Task 4.)

```csharp
private static (IReadOnlyList<WithholdingTaxRecord> withholdings, List<ParseError> errors)
    ParseWithholdingTax(
        List<string[]> rows,
        Dictionary<(string Entity, DateOnly Date, string Currency), DividendRecord> dividends)
{
    var accumulator = new Dictionary<(string Entity, DateOnly Date, string Currency), decimal>();
    var errors = new List<ParseError>();
    int rowIndex = 0;

    foreach (var record in rows)
    {
        rowIndex++;
        if (!IsDataRow(record, "Withholding Tax")) continue;
        if (record.Length < 6)
        {
            errors.Add(new ParseError("ROW_TOO_SHORT",
                $"WHT row has {record.Length} fields, expected >=6.", rowIndex));
            continue;
        }
        if (IsTotalRow(record)) continue;

        var currency = record[2].Trim();
        var description = record[4].Trim();
        var amountStr = record[5].Trim();

        if (!TryParseDate(record[3].Trim(), rowIndex, "WHT ", errors, out var date)) continue;
        if (!TryParseDecimal(amountStr, rowIndex, "ROW_AMOUNT_INVALID", "WHT amount", errors, out var rawAmount)) continue;
        if (rawAmount > 0)
        {
            errors.Add(new ParseError("WHT_POSITIVE_AMOUNT",
                $"WHT amount should be negative in IBKR CSV, got '{amountStr}'.", rowIndex));
            continue;
        }

        var entity = StripIsin(description);
        var exactKey = (entity, date, currency);

        if (dividends.TryGetValue(exactKey, out _))
        {
            if (accumulator.TryGetValue(exactKey, out var existing))
                accumulator[exactKey] = existing + Math.Abs(rawAmount);
            else
                accumulator[exactKey] = Math.Abs(rawAmount);
        }
        else
        {
            bool sameDateEntity = dividends.Keys.Any(k =>
                k.Entity == entity && k.Date == date);

            if (sameDateEntity)
                errors.Add(new ParseError("WHT_CURRENCY_MISMATCH",
                    $"WHT currency '{currency}' does not match dividend currency for '{entity}' on {date:yyyy-MM-dd}.", rowIndex));
            else
                errors.Add(new ParseError("WHT_UNMATCHED",
                    $"No dividend found for WHT entry: entity='{entity}', date={date:yyyy-MM-dd}, currency='{currency}'.", rowIndex));
        }
    }

    var result = accumulator
        .Select(kvp => new WithholdingTaxRecord(kvp.Key.Date, kvp.Key.Currency, kvp.Key.Entity, kvp.Value))
        .ToList();

    return (result.AsReadOnly(), errors);
}
```

- [ ] **Step 4: Replace `ParseInterest` with the updated version**

```csharp
private static (Dictionary<(string Currency, DateOnly Date, InterestType Type), InterestRecord> interest,
                List<ParseError> errors)
    ParseInterest(List<string[]> rows)
{
    var dict = new Dictionary<(string, DateOnly, InterestType), InterestRecord>();
    var errors = new List<ParseError>();
    int rowIndex = 0;

    foreach (var record in rows)
    {
        rowIndex++;
        if (!IsDataRow(record, "Interest")) continue;
        if (record.Length < 6) continue; // silently skip short rows

        var currency = record[2].Trim();
        var description = record[4].Trim();

        InterestType type;
        if (description.Contains("Credit Interest", StringComparison.OrdinalIgnoreCase))
            type = InterestType.Credit;
        else if (description.Contains("Debit Interest", StringComparison.OrdinalIgnoreCase))
            type = InterestType.Debit;
        else
            continue; // silently skip non-standard interest rows

        if (!TryParseDate(record[3].Trim(), rowIndex, "interest ", errors, out var date)) continue;
        if (!TryParseDecimal(record[5].Trim(), rowIndex, "ROW_AMOUNT_INVALID", "interest amount", errors, out var rawAmount)) continue;

        var amount = Math.Abs(rawAmount);
        var key = (currency, date, type);

        if (dict.TryGetValue(key, out var existing))
            dict[key] = existing with { Amount = existing.Amount + amount };
        else
            dict[key] = new InterestRecord(date, currency, "Interactive Brokers", amount, type);
    }

    return (dict, errors);
}
```

- [ ] **Step 5: Replace `ParseExchangeRates` with the updated version**

```csharp
private static (IReadOnlyList<IbkrExchangeRate> rates, List<ParseError> errors)
    ParseExchangeRates(List<string[]> rows)
{
    var dict = new Dictionary<(DateOnly Date, string From, string To), IbkrExchangeRate>();
    var errors = new List<ParseError>();
    int rowIndex = 0;

    foreach (var record in rows)
    {
        rowIndex++;
        if (!IsDataRow(record, "Base Currency Exchange Rate")) continue;
        if (record.Length < 7) continue; // silently skip short rows

        var fromCurrency = record[2].Trim();
        var toCurrency = record[5].Trim();
        var rateStr = record[6].Trim();

        if (!TryParseDate(record[3].Trim(), rowIndex, "FX rate ", errors, out var date)) continue;
        if (!TryParseDecimal(rateStr, rowIndex, "ROW_AMOUNT_INVALID", "FX rate", errors, out var rate)) continue;
        if (rate <= 0)
        {
            errors.Add(new ParseError("RATE_NON_POSITIVE",
                $"FX rate must be positive, got '{rateStr}'.", rowIndex));
            continue;
        }

        var key = (date, fromCurrency, toCurrency);
        if (dict.ContainsKey(key))
            errors.Add(new ParseError("RATE_DUPLICATE",
                $"Duplicate FX rate for {fromCurrency}/{toCurrency} on {date:yyyy-MM-dd}. Last value wins.", rowIndex));

        dict[key] = new IbkrExchangeRate(date, fromCurrency, toCurrency, rate);
    }

    return (dict.Values.ToList().AsReadOnly(), errors);
}
```

- [ ] **Step 6: Run tests**

```
dotnet test Rentier.slnx --filter "Category!=Integration" -c Release
```

Expected: all tests pass.

> **If the snapshot test fails:** The ibkr-sample.csv fixture may contain rows that exercise the date/amount error paths, making the snapshot include error messages. If the only change is error message wording (codes unchanged), accept the new snapshot:
> ```
> dotnet test tests/Rentier.Infrastructure.Tests -c Release -- --update-snapshots
> ```
> Then re-run the test suite to confirm green.

- [ ] **Step 7: Commit**

```
git add src/Rentier.Infrastructure/Parsing/IbkrCsvParser.cs
git commit -m "refactor(parser): extract TryParseDate and TryParseDecimal helpers"
```

---

### Task 4: Extract `AccumulateOrErrorWht` — reduce `ParseWithholdingTax` complexity

**Files:**
- Modify: `src/Rentier.Infrastructure/Parsing/IbkrCsvParser.cs`

**Interfaces:**
- Consumes: `IsDataRow`, `IsTotalRow`, `TryParseDate`, `TryParseDecimal` (Tasks 2–3)
- Produces:
  ```csharp
  private static void AccumulateOrErrorWht(
      string entity, DateOnly date, string currency, decimal absAmount, int rowIndex,
      Dictionary<(string Entity, DateOnly Date, string Currency), DividendRecord> dividends,
      Dictionary<(string Entity, DateOnly Date, string Currency), decimal> accumulator,
      List<ParseError> errors)
  ```

- [ ] **Step 1: Add `AccumulateOrErrorWht` after `TryParseDecimal`**

```csharp
    private static void AccumulateOrErrorWht(
        string entity, DateOnly date, string currency, decimal absAmount, int rowIndex,
        Dictionary<(string Entity, DateOnly Date, string Currency), DividendRecord> dividends,
        Dictionary<(string Entity, DateOnly Date, string Currency), decimal> accumulator,
        List<ParseError> errors)
    {
        var exactKey = (entity, date, currency);
        if (dividends.TryGetValue(exactKey, out _))
        {
            if (accumulator.TryGetValue(exactKey, out var existing))
                accumulator[exactKey] = existing + absAmount;
            else
                accumulator[exactKey] = absAmount;
        }
        else
        {
            bool sameDateEntity = dividends.Keys.Any(k => k.Entity == entity && k.Date == date);
            if (sameDateEntity)
                errors.Add(new ParseError("WHT_CURRENCY_MISMATCH",
                    $"WHT currency '{currency}' does not match dividend currency for '{entity}' on {date:yyyy-MM-dd}.", rowIndex));
            else
                errors.Add(new ParseError("WHT_UNMATCHED",
                    $"No dividend found for WHT entry: entity='{entity}', date={date:yyyy-MM-dd}, currency='{currency}'.", rowIndex));
        }
    }
```

- [ ] **Step 2: Replace `ParseWithholdingTax` with the slimmed version that calls `AccumulateOrErrorWht`**

```csharp
private static (IReadOnlyList<WithholdingTaxRecord> withholdings, List<ParseError> errors)
    ParseWithholdingTax(
        List<string[]> rows,
        Dictionary<(string Entity, DateOnly Date, string Currency), DividendRecord> dividends)
{
    var accumulator = new Dictionary<(string Entity, DateOnly Date, string Currency), decimal>();
    var errors = new List<ParseError>();
    int rowIndex = 0;

    foreach (var record in rows)
    {
        rowIndex++;
        if (!IsDataRow(record, "Withholding Tax")) continue;
        if (record.Length < 6)
        {
            errors.Add(new ParseError("ROW_TOO_SHORT",
                $"WHT row has {record.Length} fields, expected >=6.", rowIndex));
            continue;
        }
        if (IsTotalRow(record)) continue;

        var currency = record[2].Trim();
        var description = record[4].Trim();
        var amountStr = record[5].Trim();

        if (!TryParseDate(record[3].Trim(), rowIndex, "WHT ", errors, out var date)) continue;
        if (!TryParseDecimal(amountStr, rowIndex, "ROW_AMOUNT_INVALID", "WHT amount", errors, out var rawAmount)) continue;
        if (rawAmount > 0)
        {
            errors.Add(new ParseError("WHT_POSITIVE_AMOUNT",
                $"WHT amount should be negative in IBKR CSV, got '{amountStr}'.", rowIndex));
            continue;
        }

        AccumulateOrErrorWht(
            StripIsin(description), date, currency, Math.Abs(rawAmount),
            rowIndex, dividends, accumulator, errors);
    }

    var result = accumulator
        .Select(kvp => new WithholdingTaxRecord(kvp.Key.Date, kvp.Key.Currency, kvp.Key.Entity, kvp.Value))
        .ToList();

    return (result.AsReadOnly(), errors);
}
```

- [ ] **Step 3: Run unit + snapshot tests**

```
dotnet test Rentier.slnx --filter "Category!=Integration" -c Release
```

Expected: all tests pass.

- [ ] **Step 4: Commit**

```
git add src/Rentier.Infrastructure/Parsing/IbkrCsvParser.cs
git commit -m "refactor(parser): extract AccumulateOrErrorWht to reduce ParseWithholdingTax complexity"
```

---

### Task 5: Full verification

**Files:**
- Run only — no edits.

- [ ] **Step 1: Run full unit + snapshot suite**

```
dotnet test Rentier.slnx --filter "Category!=Integration" -c Release
```

Expected: all tests pass, 0 failures.

- [ ] **Step 2: Run infrastructure integration tests**

```
dotnet test tests/Rentier.Infrastructure.Tests --filter "Category=Integration" -c Release
```

Expected: all tests pass, 0 failures.

- [ ] **Step 3: Verify formatting**

```
dotnet format Rentier.slnx --no-restore --verify-no-changes
```

Expected: exits 0 with no changes reported. If formatting issues exist, run:
```
dotnet format Rentier.slnx --no-restore
git add src/Rentier.Infrastructure/Parsing/IbkrCsvParser.cs
git commit -m "style(parser): apply dotnet format"
```
