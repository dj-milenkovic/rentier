namespace Rentier.Application.DTOs;

/// <summary>Paged result returned by GetFilingsQueryHandler.</summary>
public sealed record FilingsPageResult(
    IReadOnlyList<FilingRowDto> Rows,
    int TotalCount,
    int TotalPages);
