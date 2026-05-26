namespace Rentier.Application.DTOs;

public sealed record ProcessReportsResult(
    int FilingsCreated,
    int ReportsProcessed,
    int ReportsErrored,
    IReadOnlyList<FilingCreationError> EventErrors,
    int ReportsPartialError = 0,
    IReadOnlyList<ReportProcessingDetail>? ReportDetails = null);
