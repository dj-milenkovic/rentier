using Microsoft.EntityFrameworkCore;
using Rentier.Application.DTOs;
using Rentier.Application.Repositories;
using Rentier.Domain.Entities;
using Rentier.Infrastructure.Persistence;

namespace Rentier.Infrastructure.Repositories;

public sealed class HolidayRepository : IHolidayRepository
{
    private readonly AppDbContext _db;

    public HolidayRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<HolidayConfDto> GetHolidayConfAsync(CancellationToken cancellationToken = default)
    {
        var holidays = await _db.PublicHolidays
            .AsNoTracking()
            .OrderBy(h => h.Date)
            .Select(h => new HolidayEntryDto(h.Date, h.Name))
            .ToListAsync(cancellationToken);

        var yearRange = await _db.HolidayYearRange
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == HolidayYearRange.SingletonId, cancellationToken);

        return new HolidayConfDto(holidays, yearRange?.StartYear ?? 0, yearRange?.EndYear ?? 0);
    }

    public async Task<HolidayYearRange?> GetYearRangeAsync(CancellationToken cancellationToken = default)
    {
        return await _db.HolidayYearRange
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == HolidayYearRange.SingletonId, cancellationToken);
    }

    public async Task SaveHolidaysAsync(
        IReadOnlyList<PublicHoliday> holidays,
        HolidayYearRange yearRange,
        CancellationToken cancellationToken = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await _db.PublicHolidays.ExecuteDeleteAsync(cancellationToken);
            _db.PublicHolidays.AddRange(holidays);

            var rangeExists = await _db.HolidayYearRange
                .AnyAsync(r => r.Id == HolidayYearRange.SingletonId, cancellationToken);

            if (rangeExists)
            {
                await _db.HolidayYearRange
                    .Where(r => r.Id == HolidayYearRange.SingletonId)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(r => r.StartYear, yearRange.StartYear)
                        .SetProperty(r => r.EndYear, yearRange.EndYear),
                        cancellationToken);
            }
            else
            {
                _db.HolidayYearRange.Add(yearRange);
            }

            await _db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
