using Rentier.Application.DTOs;
using Rentier.Domain.Entities;

namespace Rentier.Application.Interfaces;

/// <summary>
/// Repository contract for persisting and retrieving holiday configuration.
/// </summary>
public interface IHolidayRepository
{
    Task<HolidayConfDto> GetHolidayConfAsync(CancellationToken cancellationToken = default);
    Task<HolidayYearRange?> GetYearRangeAsync(CancellationToken cancellationToken = default);
    Task SaveHolidaysAsync(
        IReadOnlyList<PublicHoliday> holidays,
        HolidayYearRange yearRange,
        CancellationToken cancellationToken = default);
}
