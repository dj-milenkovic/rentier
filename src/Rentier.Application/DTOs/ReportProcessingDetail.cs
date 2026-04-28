namespace Rentier.Application.DTOs;

/// <summary>
/// Per-report outcome emitted during sync report processing.
/// Captures the filename, filing counts, and computed severity for a single report.
/// </summary>
public sealed record ReportProcessingDetail(
    string ReportName,
    int FilingsCreated,
    int FilingsFailed,
    SyncProgressSeverity Severity)
{
    /// <summary>
    /// Determines severity from filing outcome counts.
    /// </summary>
    public static SyncProgressSeverity ClassifySeverity(int created, int failed)
        => (created, failed) switch
        {
            (_, 0)     => SyncProgressSeverity.Info,    // All success or empty report
            (> 0, > 0) => SyncProgressSeverity.Warning, // Mixed results
            (0, > 0)   => SyncProgressSeverity.Error,   // Total failure
            _          => SyncProgressSeverity.Info      // Unreachable, defensive
        };

    /// <summary>
    /// Formats the standard log message for this report.
    /// </summary>
    public string ToLogMessage()
        => $"Report '{ReportName}': {FilingsCreated} filing(s) created, {FilingsFailed} failed.";
}
