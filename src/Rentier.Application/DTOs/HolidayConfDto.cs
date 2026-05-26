namespace Rentier.Application.DTOs;

public sealed record HolidayConfDto(IReadOnlyList<HolidayEntryDto> Holidays, int StartYear, int EndYear);
