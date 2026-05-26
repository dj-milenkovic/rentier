using Rentier.Application.Common;
using Rentier.Domain.ValueObjects;

namespace Rentier.Application.Interfaces;

/// <summary>
/// Fetches the NBS official middle exchange rate for a currency on a given date.
/// Checks the local SQLite cache first; falls back to the NBS XML web service on miss.
/// </summary>
public interface IExchangeRateFetcher
{
    Task<Result<ExchangeRate, Error>> FetchRateAsync(
        DateOnly date, string currency, CancellationToken ct = default);
}
