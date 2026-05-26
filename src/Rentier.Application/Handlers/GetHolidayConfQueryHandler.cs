using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Application.Interfaces;
using Rentier.Application.Queries;
using Rentier.Application.Repositories;

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
        var dto = await _repository.GetHolidayConfAsync(ct);
        return Result<HolidayConfDto, Error>.Success(dto);
    }
}
