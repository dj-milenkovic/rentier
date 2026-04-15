using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Application.Interfaces;

namespace Rentier.Application.Handlers;

public sealed class FetchHolidaysFromWebCommandHandler
    : ICommandHandler<FetchHolidaysFromWebCommand, Result<IReadOnlyList<HolidayEntryDto>, Error>>
{
    private readonly IHolidayImporter _importer;

    public FetchHolidaysFromWebCommandHandler(IHolidayImporter importer)
    {
        _importer = importer;
    }

    public async Task<Result<IReadOnlyList<HolidayEntryDto>, Error>> HandleAsync(
        FetchHolidaysFromWebCommand cmd, CancellationToken ct = default)
    {
        var merged = new Dictionary<DateOnly, HolidayEntryDto>();
        var failedYears = new List<int>();

        for (int year = cmd.StartYear; year <= cmd.EndYear; year++)
        {
            var result = await _importer.ImportAsync(year, ct);
            if (result.IsSuccess)
            {
                foreach (var dto in result.Value)
                    merged.TryAdd(dto.Date, dto);
            }
            else
            {
                failedYears.Add(year);
            }
        }

        if (merged.Count == 0 && failedYears.Count > 0)
            return Result<IReadOnlyList<HolidayEntryDto>, Error>.Failure(
                new Error("HOLIDAY_FETCH_ALL_FAILED",
                    $"Failed to fetch holidays for all years: {string.Join(", ", failedYears)}"));

        return Result<IReadOnlyList<HolidayEntryDto>, Error>.Success(
            merged.Values.OrderBy(d => d.Date).ToList());
    }
}
