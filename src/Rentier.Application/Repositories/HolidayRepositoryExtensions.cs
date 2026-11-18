using Rentier.Domain.ValueObjects;

namespace Rentier.Application.Repositories;

public static class HolidayRepositoryExtensions
{
    /// <summary>Loads the configured Serbian holidays as the calendar used for deadlines and rate walk-back.</summary>
    public static async Task<HolidayConf> GetHolidayCalendarAsync(
        this IHolidayRepository repository, CancellationToken ct = default)
    {
        var dto = await repository.GetHolidayConfAsync(ct);
        return new HolidayConf(dto.Holidays.Select(h => h.Date).ToList());
    }
}
