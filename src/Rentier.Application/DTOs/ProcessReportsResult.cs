namespace Rentier.Application.DTOs;

public sealed record ProcessReportsResult(
    int FilingsCreated,
    int ReportsProcessed,
    int ReportsErrored,
    IReadOnlyList<string> Errors);
