using Rentier.Application.DTOs;

namespace Rentier.Application.Commands;

public sealed record SaveHolidayConfCommand(IReadOnlyList<HolidayEntryDto> Holidays, int StartYear, int EndYear);
