using Rentier.Application.Common;
using Rentier.Domain.Enums;

namespace Rentier.Application.Filings;

/// <summary>
/// One taxable income event — a dividend or credit interest — from a broker statement or
/// entered manually. Each event becomes at most one PP-OPO filing.
/// </summary>
/// <param name="PayingEntity">Who paid the income (ticker for dividends, broker for interest).</param>
/// <param name="Amount">Gross amount in <paramref name="Currency"/>.</param>
/// <param name="Withholding">Foreign tax withheld, in <paramref name="Currency"/>; 0 when none.</param>
public sealed record IncomeEvent(
    IncomeType IncomeType,
    string PayingEntity,
    DateOnly Date,
    string Currency,
    decimal Amount,
    decimal Withholding = 0m)
{
    /// <summary>
    /// Validates a manual entry and turns it into an event: the ticker is trimmed and
    /// upper-cased, and withholding is the difference between gross and net received.
    /// </summary>
    public static Result<IncomeEvent, Error> FromManualEntry(
        IncomeType incomeType,
        string ticker,
        DateOnly incomeDate,
        string currency,
        decimal grossAmount,
        decimal? netReceived)
    {
        if (string.IsNullOrWhiteSpace(ticker))
            return Fail(ErrorCodes.TICKER_REQUIRED, "Ticker is required");

        if (grossAmount <= 0)
            return Fail(ErrorCodes.GROSS_REQUIRED, "Gross amount must be greater than zero");

        if (incomeDate == default)
            return Fail(ErrorCodes.DATE_REQUIRED, "Income date is required");

        if (netReceived > grossAmount)
            return Fail(ErrorCodes.NET_EXCEEDS_GROSS, "Net received cannot exceed gross amount");

        if (netReceived < 0)
            return Fail(ErrorCodes.NET_NEGATIVE, "Net received cannot be negative");

        var withholding = netReceived.HasValue ? grossAmount - netReceived.Value : 0m;
        return Result<IncomeEvent, Error>.Success(new IncomeEvent(
            incomeType, ticker.Trim().ToUpperInvariant(), incomeDate, currency, grossAmount, withholding));
    }

    private static Result<IncomeEvent, Error> Fail(string code, string message)
        => Result<IncomeEvent, Error>.Failure(new Error(code, message));
}
