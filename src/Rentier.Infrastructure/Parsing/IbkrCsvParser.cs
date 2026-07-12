using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using CsvHelper;
using CsvHelper.Configuration;
using Rentier.Application.Common;
using Rentier.Application.Interfaces;
using Rentier.Application.Parsing;

namespace Rentier.Infrastructure.Parsing;

public sealed class IbkrCsvParser : IStatementParser
{
    public async Task<Result<StatementParseResult, Error>> ParseAsync(
        Stream csvStream, CancellationToken cancellationToken = default)
    {
        try
        {
            List<string[]> rows;
            try
            {
                rows = await ReadRowsAsync(csvStream, cancellationToken);
            }
            catch (Exception ex)
            {
                return Result<StatementParseResult, Error>.Failure(
                    new Error("PARSE_EXCEPTION", ex.Message));
            }

            var knownSections = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Dividends", "Withholding Tax", "Interest", "Base Currency Exchange Rate"
            };
            bool hasAnySection = rows.Any(r =>
                r.Length >= 1 && knownSections.Contains(r[0]));

            if (!hasAnySection)
                return Result<StatementParseResult, Error>.Failure(
                    new Error("INVALID_FORMAT", "No recognised IBKR sections found in the CSV."));

            var allErrors = new List<ParseError>();

            var (dividends, divErrors) = ParseDividends(rows);
            allErrors.AddRange(divErrors);

            var (withholdings, whtErrors) = ParseWithholdingTax(rows, dividends);
            allErrors.AddRange(whtErrors);

            var (interest, intErrors) = ParseInterest(rows);
            allErrors.AddRange(intErrors);

            var (fxRates, fxErrors) = ParseExchangeRates(rows);
            allErrors.AddRange(fxErrors);

            return Result<StatementParseResult, Error>.Success(new StatementParseResult(
                dividends.Values.ToList().AsReadOnly(),
                interest.Values.ToList().AsReadOnly(),
                withholdings,
                fxRates,
                allErrors.AsReadOnly()));
        }
        catch (Exception ex)
        {
            return Result<StatementParseResult, Error>.Failure(
                new Error("PARSE_EXCEPTION", ex.Message));
        }
    }

    private static async Task<List<string[]>> ReadRowsAsync(
        Stream stream, CancellationToken ct)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = false,
            MissingFieldFound = null,
            BadDataFound = null,
        });

        var rows = new List<string[]>();
        while (await csv.ReadAsync())
        {
            ct.ThrowIfCancellationRequested();
            var record = csv.Parser.Record;
            if (record is not null)
                rows.Add(record);
        }
        return rows;
    }

    // ISIN pattern: (XX0000000000) — 2 letter country + 9 alphanumeric + 1 digit.
    // IBKR descriptions look like "AAPL(US0378331005) Cash Dividend" — the ISIN is mid-string,
    // so we strip the ISIN parenthetical and everything after it to recover the entity name.
    private static readonly Regex IsinPattern =
        new(@"\s*\([A-Z]{2}[A-Z0-9]{9}\d\).*$", RegexOptions.Compiled);

    /// <summary>
    /// Strips the ISIN parenthetical (e.g. "(US0378331005)") and any trailing suffix from a
    /// description, returning just the entity name (e.g. "AAPL" from "AAPL(US0378331005) Cash Dividend").
    /// </summary>
    private static string StripIsin(string description) =>
        IsinPattern.Replace(description, string.Empty).Trim();

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
                errors.Add(new ParseError("ROW_TOO_SHORT", $"Dividends row has {record.Length} fields, expected >=6.", rowIndex));
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
                errors.Add(new ParseError("ROW_TOO_SHORT", $"WHT row has {record.Length} fields, expected >=6.", rowIndex));
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
            if (record.Length < 6)
                continue; // silently skip short rows in Interest section

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

            var amount = Math.Abs(rawAmount); // always store positive
            var key = (currency, date, type);

            if (dict.TryGetValue(key, out var existing))
                dict[key] = existing with { Amount = existing.Amount + amount };
            else
                dict[key] = new InterestRecord(date, currency, "Interactive Brokers", amount, type);
        }

        return (dict, errors);
    }

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
            if (record.Length < 7)
                continue; // silently skip short rows — custom statements use a 4-column FX format

            var fromCurrency = record[2].Trim();
            var toCurrency = record[5].Trim();
            var rateStr = record[6].Trim();

            if (!TryParseDate(record[3].Trim(), rowIndex, "FX rate ", errors, out var date)) continue;
            if (!TryParseDecimal(rateStr, rowIndex, "ROW_AMOUNT_INVALID", "FX rate", errors, out var rate)) continue;
            if (rate <= 0)
            {
                errors.Add(new ParseError("RATE_NON_POSITIVE", $"FX rate must be positive, got '{rateStr}'.", rowIndex));
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

    private static bool IsDataRow(string[] record, string sectionName)
        => record.Length >= 2
           && record[0].Equals(sectionName, StringComparison.OrdinalIgnoreCase)
           && record[1].Equals("Data", StringComparison.OrdinalIgnoreCase);

    private static bool IsTotalRow(string[] record)
        => record.Length >= 3
           && record[2].Trim().StartsWith("Total", StringComparison.OrdinalIgnoreCase);

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
}
