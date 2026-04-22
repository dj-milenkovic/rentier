using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.Interfaces;
using Rentier.Application.Repositories;
using Rentier.Domain.Entities;

namespace Rentier.Application.Handlers;

/// <summary>
/// Seeds default Serbian public holidays for the current year through the next three years
/// when no holiday configuration exists yet. Idempotent — does nothing if data is already present.
/// </summary>
public sealed class EnsureHolidaysSeededCommandHandler
    : ICommandHandler<EnsureHolidaysSeededCommand, Result<bool, Error>>
{
    private readonly IHolidayRepository _repository;

    public EnsureHolidaysSeededCommandHandler(IHolidayRepository repository)
    {
        _repository = repository;
    }

    /// <returns>
    /// <see langword="true"/> when holidays were seeded; <see langword="false"/> when data
    /// already existed and no action was taken.
    /// </returns>
    public async Task<Result<bool, Error>> HandleAsync(
        EnsureHolidaysSeededCommand command, CancellationToken ct = default)
    {
        var yearRange = await _repository.GetYearRangeAsync(ct);
        if (yearRange is not null)
            return Result<bool, Error>.Success(false);

        var currentYear = DateOnly.FromDateTime(DateTime.Today).Year;
        var endYear = currentYear + 3;

        // Seed the fixed-date Serbian public holidays for every year in the range.
        // Variable-date holidays (Easter Orthodox, Labour Day) must be imported via the web importer.
        var seededHolidays = new List<PublicHoliday>();
        for (int year = currentYear; year <= endYear; year++)
        {
            seededHolidays.Add(PublicHoliday.Create(new DateOnly(year, 1, 1), "Nova godina"));
            seededHolidays.Add(PublicHoliday.Create(new DateOnly(year, 1, 2), "Nova godina"));
            seededHolidays.Add(PublicHoliday.Create(new DateOnly(year, 1, 7), "Božić"));
            seededHolidays.Add(PublicHoliday.Create(new DateOnly(year, 2, 15), "Sretenje"));
            seededHolidays.Add(PublicHoliday.Create(new DateOnly(year, 2, 16), "Sretenje"));
            seededHolidays.Add(PublicHoliday.Create(new DateOnly(year, 5, 1), "Praznik rada"));
            seededHolidays.Add(PublicHoliday.Create(new DateOnly(year, 5, 2), "Praznik rada"));
            seededHolidays.Add(PublicHoliday.Create(new DateOnly(year, 6, 28), "Vidovdan"));
            seededHolidays.Add(PublicHoliday.Create(new DateOnly(year, 11, 11), "Dan primirja"));
        }

        var seedRange = new HolidayYearRange(currentYear, endYear);
        await _repository.SaveHolidaysAsync(seededHolidays, seedRange, ct);

        return Result<bool, Error>.Success(true);
    }
}
