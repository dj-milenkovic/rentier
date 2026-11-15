namespace Rentier.Application.DTOs;

/// <param name="ReportsPendingRetry">
/// Reports whose processing failed without persisting anything and that stay pending (Init) to be
/// retried by the next run. Not included in <paramref name="ReportsErrored"/>, which counts only
/// reports that ended in Error and need attention.
/// </param>
/// <param name="FailedAttempts">Every failed attempt in this run, including those that exhausted the retry limit.</param>
public sealed record ProcessReportsResult(
    int FilingsCreated,
    int ReportsProcessed,
    int ReportsErrored,
    IReadOnlyList<FilingCreationError> EventErrors,
    int ReportsPartialError = 0,
    IReadOnlyList<ReportProcessingDetail>? ReportDetails = null,
    int ReportsPendingRetry = 0,
    IReadOnlyList<ReportFailedAttempt>? FailedAttempts = null);
