using System.Globalization;
using System.Text.RegularExpressions;
using CsvHelper;
using Microsoft.Extensions.Logging;
using Rentier.Application.Common;
using Rentier.Application.Interfaces;
using Rentier.Application.Parsing;
using Rentier.Infrastructure.Logging;

namespace Rentier.Infrastructure.Parsing;

public sealed partial class IbkrCsvParser(ILogger<IbkrCsvParser> logger) : IStatementParser
{
    // Logging rule for this class: counts and error codes only. Statement content (amounts,
    // tickers, account ids) and CsvHelper messages, which quote the raw record, never go in a template.
    private readonly ILogger<IbkrCsvParser> _logger = logger;

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
            // Boundary translation: any read/CSV failure on a user-supplied file is an expected
            // parse failure; only caller cancellation propagates.
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                _logger.StatementUnreadable(ex);
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
            {
                _logger.StatementHasNoSections(rows.Count);
                return Result<StatementParseResult, Error>.Failure(
                    new Error("INVALID_FORMAT", "No recognised IBKR sections found in the CSV."));
            }

            var allErrors = new List<ParseError>();

            var (dividends, divErrors) = ParseDividends(rows);
            allErrors.AddRange(divErrors);

            var (interest, intErrors) = ParseInterest(rows);
            allErrors.AddRange(intErrors);

            // Withholding is paired onto the income events here, in one place, so consumers never
            // re-match it. Unpaired withholding is an Error diagnostic, never silently dropped.
            allErrors.AddRange(PairWithholdingTax(rows, dividends, interest));

            // FX rates only feed the cross-rate fallback; a bad row there loses no income (an
            // event that then lacks a rate fails visibly on its own), so these are warnings.
            var (fxRates, fxErrors) = ParseExchangeRates(rows);
            allErrors.AddRange(fxErrors.Select(e => e with { Severity = ParseSeverity.Warning }));

            _logger.StatementParsed(dividends.Count, interest.Count, fxRates.Count, allErrors.Count);

            return Result<StatementParseResult, Error>.Success(new StatementParseResult(
                dividends.Values.ToList().AsReadOnly(),
                interest.Values.ToList().AsReadOnly(),
                fxRates,
                allErrors.AsReadOnly()));
        }
        // Boundary translation: a malformed statement must surface as a Result failure the
        // report-processing handler can record, never as an exception; only caller cancellation
        // propagates.
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _logger.StatementParseFailed(ex);
            return Result<StatementParseResult, Error>.Failure(
                new Error("PARSE_EXCEPTION", ex.Message));
        }
    }

    // CsvHelper 34 auto-detects the separator and expects a header row by default, and IBKR
    // statements are ragged (every section has a different column count). Pin everything the
    // parser depends on so a library default can never silently reshape the statement.
    private static readonly CsvReaderOptions ReaderOptions = new()
    {
        Separator = ',',
        HasHeader = false,
        SkipEmptyLines = true,
        TrimWhitespace = false,
        FieldCountMismatch = CsvFieldCountHandling.Allow,
        MissingField = CsvMissingFieldHandling.ReturnEmpty,
        MalformedData = CsvMalformedDataHandling.Recover,
        FormatProvider = CultureInfo.InvariantCulture,
    };

    private static async Task<List<string[]>> ReadRowsAsync(
        Stream stream, CancellationToken ct)
    {
        await using var csv = CsvReader.Create(stream, ReaderOptions, leaveOpen: true);

        var rows = new List<string[]>();
        while (await csv.ReadAsync(ct))
        {
            ct.ThrowIfCancellationRequested();
            var record = new string[csv.FieldCount];
            for (var i = 0; i < record.Length; i++)
                record[i] = csv.GetString(i);
            rows.Add(record);
        }
        return rows;
    }

    private const string ROW_AMOUNT_INVALID = "ROW_AMOUNT_INVALID";

    // ISIN pattern: (XX0000000000) — 2 letter country + 9 alphanumeric + 1 digit.
    // IBKR descriptions look like "AAPL(US0378331005) Cash Dividend" — the ISIN is mid-string,
    // so we strip the ISIN parenthetical and everything after it to recover the entity name.
    [GeneratedRegex(@"\s*\([A-Z]{2}[A-Z0-9]{9}\d\).*$")]
    private static partial Regex IsinPattern();

    /// <summary>
    /// Strips the ISIN parenthetical (e.g. "(US0378331005)") and any trailing suffix from a
    /// description, returning just the entity name (e.g. "AAPL" from "AAPL(US0378331005) Cash Dividend").
    /// </summary>
    private static string StripIsin(string description) =>
        IsinPattern().Replace(description, string.Empty).Trim();

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
            if (!TryReadDataRow(record, "Dividends", "Dividends", rowIndex, errors, out var currency, out var description)) continue;

            if (!TryParseDate(record[3].Trim(), rowIndex, "", errors, out var date)) continue;
            if (!TryParseDecimal(record[5].Trim(), rowIndex, ROW_AMOUNT_INVALID, "amount", errors, out var amount)) continue;

            var entity = StripIsin(description);
            var key = (entity, date, currency);

            if (dict.TryGetValue(key, out var existing))
                dict[key] = existing with { Amount = existing.Amount + amount };
            else
                dict[key] = new DividendRecord(date, currency, entity, amount);
        }

        return (dict, errors);
    }

    /// <summary>
    /// Reads the "Withholding Tax" section and adds each row's amount onto the income event it
    /// belongs to: a dividend on (entity, date, currency), or — for rows describing withholding on
    /// credit interest — the credit interest on (currency, date). A row that matches no income event
    /// is returned as an Error diagnostic.
    /// </summary>
    private static List<ParseError> PairWithholdingTax(
        List<string[]> rows,
        Dictionary<(string Entity, DateOnly Date, string Currency), DividendRecord> dividends,
        Dictionary<(string Currency, DateOnly Date, InterestType Type), InterestRecord> interest)
    {
        var errors = new List<ParseError>();
        int rowIndex = 0;

        foreach (var record in rows)
        {
            rowIndex++;
            if (!TryReadDataRow(record, "Withholding Tax", "WHT", rowIndex, errors, out var currency, out var description)) continue;
            var amountStr = record[5].Trim();

            if (!TryParseDate(record[3].Trim(), rowIndex, "WHT ", errors, out var date)) continue;
            if (!TryParseDecimal(amountStr, rowIndex, ROW_AMOUNT_INVALID, "WHT amount", errors, out var rawAmount)) continue;
            if (rawAmount > 0)
            {
                errors.Add(new ParseError("WHT_POSITIVE_AMOUNT",
                    $"WHT amount should be negative in IBKR CSV, got '{amountStr}'.", rowIndex));
                continue;
            }

            var absAmount = Math.Abs(rawAmount);
            if (IsInterestWithholding(description))
                PairInterestWithholding((currency, date, InterestType.Credit), absAmount, rowIndex, interest, errors);
            else
                PairDividendWithholding((StripIsin(description), date, currency), absAmount, rowIndex, dividends, errors);
        }

        return errors;
    }

    /// <summary>IBKR describes withholding on interest as e.g. "Withholding @ 20% on Credit Interest for Jan-2024".</summary>
    private static bool IsInterestWithholding(string description)
        => description.Contains("Credit Interest", StringComparison.OrdinalIgnoreCase);

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

            if (!TryClassifyInterestType(description, out var type)) continue; // silently skip non-standard interest rows

            if (!TryParseDate(record[3].Trim(), rowIndex, "interest ", errors, out var date)) continue;
            if (!TryParseDecimal(record[5].Trim(), rowIndex, ROW_AMOUNT_INVALID, "interest amount", errors, out var rawAmount)) continue;

            var amount = Math.Abs(rawAmount); // always store positive
            var key = (currency, date, type);

            if (dict.TryGetValue(key, out var existing))
                dict[key] = existing with { Amount = existing.Amount + amount };
            else
                dict[key] = new InterestRecord(date, currency, "Interactive Brokers", amount, type);
        }

        return (dict, errors);
    }

    /// <summary>
    /// Classifies an Interest section row description as Credit or Debit interest.
    /// Returns false for non-standard rows, which callers silently skip.
    /// </summary>
    private static bool TryClassifyInterestType(string description, out InterestType type)
    {
        if (description.Contains("Credit Interest", StringComparison.OrdinalIgnoreCase))
        {
            type = InterestType.Credit;
            return true;
        }

        if (description.Contains("Debit Interest", StringComparison.OrdinalIgnoreCase))
        {
            type = InterestType.Debit;
            return true;
        }

        type = default;
        return false;
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
            if (!TryParseDecimal(rateStr, rowIndex, ROW_AMOUNT_INVALID, "FX rate", errors, out var rate)) continue;
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

    private static bool TryReadDataRow(
        string[] record, string sectionName, string tooShortPrefix,
        int rowIndex, List<ParseError> errors,
        out string currency, out string description)
    {
        currency = description = string.Empty;
        if (!IsDataRow(record, sectionName)) return false;
        if (record.Length < 6)
        {
            errors.Add(new ParseError("ROW_TOO_SHORT",
                $"{tooShortPrefix} row has {record.Length} fields, expected >=6.", rowIndex));
            return false;
        }
        if (IsTotalRow(record)) return false;
        currency = record[2].Trim();
        description = record[4].Trim();
        return true;
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

    private static void PairInterestWithholding(
        (string Currency, DateOnly Date, InterestType Type) key, decimal absAmount, int rowIndex,
        Dictionary<(string Currency, DateOnly Date, InterestType Type), InterestRecord> interest,
        List<ParseError> errors)
    {
        if (interest.TryGetValue(key, out var credit))
        {
            interest[key] = credit with { Withholding = credit.Withholding + absAmount };
            return;
        }

        errors.Add(new ParseError("WHT_UNMATCHED",
            $"No credit interest found for interest WHT entry: date={key.Date:yyyy-MM-dd}, currency='{key.Currency}'.", rowIndex));
    }

    private static void PairDividendWithholding(
        (string Entity, DateOnly Date, string Currency) key, decimal absAmount, int rowIndex,
        Dictionary<(string Entity, DateOnly Date, string Currency), DividendRecord> dividends,
        List<ParseError> errors)
    {
        if (dividends.TryGetValue(key, out var dividend))
        {
            dividends[key] = dividend with { Withholding = dividend.Withholding + absAmount };
        }
        else
        {
            bool sameDateEntity = dividends.Keys.Any(k => k.Entity == key.Entity && k.Date == key.Date);
            if (sameDateEntity)
                errors.Add(new ParseError("WHT_CURRENCY_MISMATCH",
                    $"WHT currency '{key.Currency}' does not match dividend currency for '{key.Entity}' on {key.Date:yyyy-MM-dd}.", rowIndex));
            else
                errors.Add(new ParseError("WHT_UNMATCHED",
                    $"No dividend found for WHT entry: entity='{key.Entity}', date={key.Date:yyyy-MM-dd}, currency='{key.Currency}'.", rowIndex));
        }
    }
}
