using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Domain.ValueObjects;

namespace Rentier.Application.Interfaces;

/// <summary>
/// Resolves a functional NBS exchange rate for a given date and currency,
/// walking back through business days when the exact date has no published rate.
/// </summary>
public interface IExchangeRateResolver
{
    Task<Result<RateResolution, Error>> ResolveAsync(
        DateOnly date,
        string currency,
        HolidayConf holidays,
        int maxLookbackDays = 10,
        CancellationToken ct = default);
}
