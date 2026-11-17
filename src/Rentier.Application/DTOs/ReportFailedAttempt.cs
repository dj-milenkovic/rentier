namespace Rentier.Application.DTOs;

/// <summary>What happened to a report whose processing attempt failed without persisting anything.</summary>
public enum FailedAttemptKind
{
    /// <summary>The attempt was recorded; the report stays pending (Init) and is retried by the next run.</summary>
    PendingRetry,

    /// <summary>The attempt limit was reached; the report was moved to Error and is no longer retried.</summary>
    Exhausted,

    /// <summary>The attempt could not be recorded (e.g. the database was unavailable); the report stays pending.</summary>
    NotRecorded,

    /// <summary>The report was deleted (or processed elsewhere) during the run; there is nothing to retry.</summary>
    ReportGone,
}

/// <summary>A report processing attempt that failed and persisted nothing.</summary>
/// <param name="Attempt">The report's failed-attempt count after this attempt (0 when not recorded).</param>
public sealed record ReportFailedAttempt(
    Guid ReportId,
    string ReportName,
    string Reason,
    int Attempt,
    FailedAttemptKind Kind)
{
    public bool WillBeRetried => Kind is FailedAttemptKind.PendingRetry or FailedAttemptKind.NotRecorded;

    public string ToLogMessage() => Kind switch
    {
        FailedAttemptKind.PendingRetry =>
            $"Report '{ReportName}': processing failed (attempt {Attempt}) — {Reason}. " +
            "Nothing was saved; it will be retried on the next sync.",
        FailedAttemptKind.Exhausted =>
            $"Report '{ReportName}': processing failed {Attempt} times — {Reason}. " +
            "It was marked as Error and will not be retried automatically.",
        FailedAttemptKind.NotRecorded =>
            $"Report '{ReportName}': processing failed — {Reason}. " +
            "Nothing was saved and the failure could not be recorded; it will be retried on the next sync.",
        _ =>
            $"Report '{ReportName}': processing failed — {Reason}. The report no longer exists; nothing to retry.",
    };
}
