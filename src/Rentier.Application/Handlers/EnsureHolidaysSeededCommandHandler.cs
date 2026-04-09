using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.Interfaces;
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
        var seededHolidays = new List<PublicHoliday>
        {
            PublicHoliday.Create(new DateOnly(currentYear, 1, 1), "Nova godina"),
            PublicHoliday.Create(new DateOnly(currentYear, 1, 2), "Nova godina"),
            PublicHoliday.Create(new DateOnly(currentYear, 1, 7), "Božić"),
            PublicHoliday.Create(new DateOnly(currentYear, 2, 15), "Sretenje"),
            PublicHoliday.Create(new DateOnly(currentYear, 2, 16), "Sretenje"),
            PublicHoliday.Create(new DateOnly(currentYear, 5, 1), "Praznik rada"),
            PublicHoliday.Create(new DateOnly(currentYear, 5, 2), "Praznik rada"),
            PublicHoliday.Create(new DateOnly(currentYear, 6, 28), "Vidovdan"),
            PublicHoliday.Create(new DateOnly(currentYear, 11, 11), "Dan primirja"),
        };
        var seedRange = new HolidayYearRange(currentYear, currentYear + 3);
        await _repository.SaveHolidaysAsync(seededHolidays, seedRange, ct);

        return Result<bool, Error>.Success(true);
    }
}
