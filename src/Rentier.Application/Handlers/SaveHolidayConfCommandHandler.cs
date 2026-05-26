using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Application.Interfaces;
using Rentier.Application.Repositories;
using Rentier.Domain.Entities;
using Rentier.Domain.Exceptions;

namespace Rentier.Application.Handlers;

public sealed class SaveHolidayConfCommandHandler
    : ICommandHandler<SaveHolidayConfCommand, Result<VoidResult, Error>>
{
    private readonly IHolidayRepository _repository;

    public SaveHolidayConfCommandHandler(IHolidayRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<VoidResult, Error>> HandleAsync(
        SaveHolidayConfCommand cmd, CancellationToken ct = default)
    {
        HolidayYearRange yearRange;
        try
        {
            yearRange = new HolidayYearRange(cmd.StartYear, cmd.EndYear);
        }
        catch (DomainException ex)
        {
            return Result<VoidResult, Error>.Failure(new Error(ErrorCodes.HOLIDAY_SAVE_INVALID_YEAR_RANGE, ex.Message));
        }

        var dateGroups = cmd.Holidays.GroupBy(h => h.Date);
        if (dateGroups.Any(g => g.Count() > 1))
            return Result<VoidResult, Error>.Failure(new Error(ErrorCodes.HOLIDAY_SAVE_DUPLICATE_DATES, "Holiday list contains duplicate dates."));

        var holidays = cmd.Holidays
            .Select(dto => PublicHoliday.Create(dto.Date, dto.Name))
            .ToList();

        await _repository.SaveHolidaysAsync(holidays, yearRange, ct);
        return Result<VoidResult, Error>.Success(VoidResult.Value);
    }
}
