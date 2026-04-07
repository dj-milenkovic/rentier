using Microsoft.EntityFrameworkCore;
using Rentier.Application.Repositories;
using Rentier.Domain.ValueObjects;
using Rentier.Infrastructure.Persistence;

namespace Rentier.Infrastructure.Repositories;

public sealed class ExchangeRateCacheRepository : IExchangeRateCacheRepository
{
    private readonly AppDbContext _db;

    public ExchangeRateCacheRepository(AppDbContext db) => _db = db;

    public async Task<ExchangeRate?> GetAsync(DateOnly date, string currency, CancellationToken ct = default)
        => await _db.ExchangeRateCache
            .FindAsync(new object[] { date, currency.ToUpperInvariant() }, ct);

    public async Task<IReadOnlyList<ExchangeRate>> GetByDateRangeAsync(
        DateOnly from, DateOnly to, string currency, CancellationToken ct = default)
    {
        var upper = currency.ToUpperInvariant();
        return await _db.ExchangeRateCache.AsNoTracking()
            .Where(e => e.Currency == upper && e.Date >= from && e.Date <= to)
            .OrderBy(e => e.Date)
            .ToListAsync(ct);
    }

    public async Task SaveAsync(ExchangeRate rate, CancellationToken ct = default)
    {
        var upper = rate.Currency.ToUpperInvariant();
        var existing = await _db.ExchangeRateCache
            .FindAsync(new object[] { rate.Date, upper }, ct);
        if (existing is not null)
            _db.Entry(existing).CurrentValues.SetValues(
                new ExchangeRate(rate.Date, upper, rate.RateToRsd));
        else
            _db.ExchangeRateCache.Add(new ExchangeRate(rate.Date, upper, rate.RateToRsd));
        await _db.SaveChangesAsync(ct);
    }

    public async Task SaveBatchAsync(IReadOnlyList<ExchangeRate> rates, CancellationToken ct = default)
    {
        foreach (var rate in rates)
        {
            var upper = rate.Currency.ToUpperInvariant();
            var existing = await _db.ExchangeRateCache
                .FindAsync(new object[] { rate.Date, upper }, ct);
            if (existing is not null)
                _db.Entry(existing).CurrentValues.SetValues(
                    new ExchangeRate(rate.Date, upper, rate.RateToRsd));
            else
                _db.ExchangeRateCache.Add(new ExchangeRate(rate.Date, upper, rate.RateToRsd));
        }
        await _db.SaveChangesAsync(ct);
    }
}
