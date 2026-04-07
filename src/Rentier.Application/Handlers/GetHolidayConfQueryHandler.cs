using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Application.Interfaces;
using Rentier.Application.Queries;
using Rentier.Domain.Entities;

namespace Rentier.Application.Handlers;

public sealed class GetHolidayConfQueryHandler
    : IQueryHandler<GetHolidayConfQuery, Result<HolidayConfDto, Error>>
{
    private readonly IHolidayRepository _repository;

    public GetHolidayConfQueryHandler(IHolidayRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<HolidayConfDto, Error>> HandleAsync(
        GetHolidayConfQuery query, CancellationToken ct = default)
    {
        var yearRange = await _repository.GetYearRangeAsync(ct);

        if (yearRange is null)
        {
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
        }

        var dto = await _repository.GetHolidayConfAsync(ct);
        return Result<HolidayConfDto, Error>.Success(dto);
    }
}
