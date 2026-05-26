namespace Rentier.Application.DTOs;

/// <summary>Paged result returned by GetReportsQueryHandler.</summary>
public sealed record ReportsPageResult(
    IReadOnlyList<ReportRowDto> Rows,
    int TotalCount,
    int TotalPages);
