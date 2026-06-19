using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Rentier.Application.DTOs;
using Rentier.Application.Repositories;
using Rentier.Domain.Entities;
using Rentier.Infrastructure.Persistence;

namespace Rentier.Infrastructure.Repositories;

public sealed class HolidayRepository(AppDbContext db, ILogger<HolidayRepository>? logger = null) : IHolidayRepository
{
    private readonly AppDbContext _db;
    private readonly ILogger<HolidayRepository> _logger;

    public HolidayRepository(AppDbContext db, ILogger<HolidayRepository>? logger = null)
    {
        _db = db;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<HolidayRepository>.Instance;
    }

    public async Task<HolidayConfDto> GetHolidayConfAsync(CancellationToken cancellationToken = default)
    {
        var holidays = await db.PublicHolidays
            .AsNoTracking()
            .OrderBy(h => h.Date)
            .Select(h => new HolidayEntryDto(h.Date, h.Name))
            .ToListAsync(cancellationToken);

        var yearRange = await db.HolidayYearRange
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == HolidayYearRange.SingletonId, cancellationToken);

        return new HolidayConfDto(holidays, yearRange?.StartYear ?? 0, yearRange?.EndYear ?? 0);
    }

    public async Task<HolidayYearRange?> GetYearRangeAsync(CancellationToken cancellationToken = default)
    {
        return await db.HolidayYearRange
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == HolidayYearRange.SingletonId, cancellationToken);
    }

    public async Task SaveHolidaysAsync(
        IReadOnlyList<PublicHoliday> holidays,
        HolidayYearRange yearRange,
        CancellationToken cancellationToken = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await db.PublicHolidays.ExecuteDeleteAsync(cancellationToken);
            db.PublicHolidays.AddRange(holidays);

            var rangeExists = await db.HolidayYearRange
                .AnyAsync(r => r.Id == HolidayYearRange.SingletonId, cancellationToken);

            if (rangeExists)
            {
                await db.HolidayYearRange
                    .Where(r => r.Id == HolidayYearRange.SingletonId)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(r => r.StartYear, yearRange.StartYear)
                        .SetProperty(r => r.EndYear, yearRange.EndYear),
                        cancellationToken);
            }
            else
            {
                db.HolidayYearRange.Add(yearRange);
            }

            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }
        catch
        {
            try
            {
                await tx.RollbackAsync(cancellationToken);
            }
            catch (Exception rollbackEx)
            {
                // Rollback failure is logged but not rethrown — the original exception is the root cause.
                _logger.LogError(rollbackEx, "Transaction rollback failed after a holiday save error.");
            }
            throw;
        }
    }
}
