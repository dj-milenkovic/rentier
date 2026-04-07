// contracts/IHolidayRepository.cs
// Interface contract for Feature 003: Holiday Configuration
// Layer: Rentier.Application (defined here; implemented in Rentier.Infrastructure)
//
// Full implementation lives at:
//   src/Rentier.Application/Interfaces/IHolidayRepository.cs

using Rentier.Application.DTOs;
using Rentier.Domain.Entities;

namespace Rentier.Application.Interfaces;

/// <summary>
/// Repository contract for persisting and retrieving holiday configuration.
/// Defined in Application; implemented in Infrastructure.
/// </summary>
public interface IHolidayRepository
{
    /// <summary>
    /// Returns all persisted holidays mapped to DTOs, ordered by Date ascending,
    /// together with the configured year range.
    /// Returns an empty Holidays list and a null YearRange if no data has been saved yet.
    /// </summary>
    Task<HolidayConfDto> GetHolidayConfAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the singleton HolidayYearRange (Id=1), or null if it does not yet exist.
    /// Used by SaveHolidayConfCommandHandler to detect first-run seeding.
    /// </summary>
    Task<HolidayYearRange?> GetYearRangeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces ALL existing PublicHoliday rows with the provided list, and upserts the
    /// HolidayYearRange singleton (Id=1) with the supplied startYear and endYear.
    /// Uses ExecuteDeleteAsync + AddRange strategy (no diffing).
    /// </summary>
    Task SaveHolidaysAsync(
        IReadOnlyList<PublicHoliday> holidays,
        HolidayYearRange yearRange,
        CancellationToken cancellationToken = default);
}
