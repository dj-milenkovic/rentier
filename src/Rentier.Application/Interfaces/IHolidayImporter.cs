using Rentier.Application.Common;
using Rentier.Application.DTOs;

namespace Rentier.Application.Interfaces;

/// <summary>
/// IMPORTANT: This interface MUST NOT be called automatically. Only invoked on explicit user action.
/// (Constitution amendment CA-EXT-001)
/// </summary>
public interface IHolidayImporter
{
    Task<Result<IReadOnlyList<HolidayEntryDto>, Error>> ImportAsync(
        int year,
        CancellationToken cancellationToken = default);
}
