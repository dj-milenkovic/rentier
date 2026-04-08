// contracts/IHolidayImporter.cs
// Interface contract for Feature 003: Holiday Configuration
// Layer: Rentier.Application (defined here; implemented in Rentier.Infrastructure)
//
// Full implementation lives at:
//   src/Rentier.Application/Interfaces/IHolidayImporter.cs

using Rentier.Application.Common;
using Rentier.Application.DTOs;

namespace Rentier.Application.Interfaces;

/// <summary>
/// Contract for importing public holidays from an external source (e.g., timeanddate.com).
/// Defined in Application; implemented in Infrastructure by TimeAndDateHolidayScraper.
///
/// IMPORTANT: This interface MUST NOT be called automatically. It is only invoked
/// when the user explicitly triggers the "Import from Web" action.
/// (Constitution amendment CA-EXT-001)
/// </summary>
public interface IHolidayImporter
{
    /// <summary>
    /// Fetches public holidays for the specified year from an external web source.
    ///
    /// Returns:
    ///   Result.Success with a non-empty list of HolidayEntryDto on success.
    ///   Result.Failure with a descriptive error message on HTTP error, parse error,
    ///   or when zero holidays are found.
    ///
    /// Does NOT persist to the database. The caller (ViewModel) is responsible
    /// for merging the returned list into the UI and deciding whether to save.
    /// </summary>
    /// <param name="year">
    ///   The calendar year to import. Must be within HolidayYearRange bounds (2020–2030+).
    /// </param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    Task<Result<IReadOnlyList<HolidayEntryDto>, Error>> ImportAsync(
        int year,
        CancellationToken cancellationToken = default);
}
