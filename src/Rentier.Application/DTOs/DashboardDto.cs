namespace Rentier.Application.DTOs;

public sealed record DashboardDto(
    IReadOnlyList<UpcomingDeadlineDto> UpcomingDeadlines,
    IReadOnlyList<OverdueFilingDto> OverdueFilings,
    int InitCount,
    int FiledCount,
    int PaidCount,
    decimal TotalUnpaidRsd,
    DateOnly? LastSyncDate);
