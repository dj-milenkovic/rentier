using Rentier.Domain.ValueObjects;

namespace Rentier.Application.Repositories;

public interface IExchangeRateCacheRepository
{
    Task<ExchangeRate?> GetAsync(DateOnly date, string currency, CancellationToken ct = default);
    Task<IReadOnlyList<ExchangeRate>> GetByDateRangeAsync(DateOnly from, DateOnly to, string currency, CancellationToken ct = default);
    Task SaveAsync(ExchangeRate rate, CancellationToken ct = default);
    Task SaveBatchAsync(IReadOnlyList<ExchangeRate> rates, CancellationToken ct = default);
}
