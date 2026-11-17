using System.Globalization;
using Rentier.Domain.Enums;
using Rentier.Domain.Exceptions;

namespace Rentier.Domain.Entities;

/// <summary>
/// Represents a raw activity statement email attachment awaiting processing.
/// ImportDate is DateOnly per constitution Principle III.
/// </summary>
/// <remarks>
/// The entity never reads the system clock: callers supply "now" explicitly, so behaviour
/// is deterministic and the Domain stays free of ambient time.
/// </remarks>
public sealed class Report
{
    public Guid Id { get; private set; }
    public DateOnly ImportDate { get; private set; }
    public Guid ImporterId { get; private set; }
    public ReportStatus Status { get; private set; }
    public string ReportName { get; private set; } = string.Empty;
    public byte[]? AttachmentContent { get; private set; }
    public long? MailboxMessageId { get; private set; }
    public Guid? OriginalReportId { get; private set; }
    public DateOnly? EmailDate { get; private set; }

    /// <summary>Maximum stored length of <see cref="LastProcessingError"/>.</summary>
    public const int MAX_PROCESSING_ERROR_LENGTH = 1000;

    /// <summary>
    /// Number of processing attempts that failed without persisting anything (the report stayed
    /// Init so it could be retried). Reaching the caller's limit moves the report to Error.
    /// </summary>
    public int ProcessingAttempts { get; private set; }

    /// <summary>
    /// Why the most recent processing attempt did not succeed — a failed attempt or the reason the
    /// report was rejected. Cleared when the report is processed successfully.
    /// </summary>
    public string? LastProcessingError { get; private set; }

    // EF Core parameterless constructor
    private Report() { }

    /// <summary>Creates a new Report from an email attachment or a manual CSV import.</summary>
    /// <param name="importDate">
    /// The calendar date on which the report was imported. Callers pass the user's
    /// <b>local</b> date (it is shown to the user and filtered by year in the Reports page).
    /// </param>
    public static Report Create(
        Guid importerId,
        string reportName,
        byte[]? attachmentContent,
        long? mailboxMessageId,
        DateOnly importDate,
        DateOnly? emailDate = null)
    {
        if (string.IsNullOrWhiteSpace(reportName))
            throw new DomainException("ReportName must not be empty");
        if (reportName.Length > 500)
            throw new DomainException("ReportName must not exceed 500 characters");

        return new Report
        {
            Id = Guid.NewGuid(),
            ImporterId = importerId,
            ImportDate = importDate,
            Status = ReportStatus.Init,
            ReportName = reportName.Trim(),
            AttachmentContent = attachmentContent,
            MailboxMessageId = mailboxMessageId,
            EmailDate = emailDate
        };
    }

    /// <summary>
    /// Transitions the report to a new processing status.
    /// Only Init → Processed / PartialError / Error are valid.
    /// </summary>
    public void SetStatus(ReportStatus status)
    {
        var isValid = (Status, status) switch
        {
            (ReportStatus.Init, ReportStatus.Processed) => true,
            (ReportStatus.Init, ReportStatus.PartialError) => true,
            (ReportStatus.Init, ReportStatus.Error) => true,
            _ => false
        };

        if (!isValid)
            throw new DomainException($"Invalid Report status transition: {Status} → {status}");

        Status = status;
        if (status is ReportStatus.Processed or ReportStatus.PartialError)
            LastProcessingError = null;
    }

    /// <summary>Rejects the report as a whole (Init → Error), recording why.</summary>
    public void Reject(string reason)
    {
        SetStatus(ReportStatus.Error);
        LastProcessingError = TruncateReason(reason);
    }

    /// <summary>
    /// Records a processing attempt that failed without persisting anything. The report stays
    /// Init so a later run retries it, until <paramref name="maxAttempts"/> failed attempts have
    /// been recorded; then it moves to Error so it is no longer retried and needs attention.
    /// </summary>
    /// <returns>True when the attempt limit was reached and the report is now Error.</returns>
    public bool RecordFailedAttempt(string reason, int maxAttempts)
    {
        if (maxAttempts < 1)
            throw new DomainException("maxAttempts must be at least 1");
        if (Status != ReportStatus.Init)
            throw new DomainException($"Only a pending (Init) report can record a failed attempt; status is {Status}");

        ProcessingAttempts++;
        LastProcessingError = TruncateReason(reason);

        if (ProcessingAttempts < maxAttempts)
            return false;

        SetStatus(ReportStatus.Error);
        return true;
    }

    private static string TruncateReason(string reason)
    {
        var text = string.IsNullOrWhiteSpace(reason) ? "Unknown error" : reason.Trim();
        return text.Length <= MAX_PROCESSING_ERROR_LENGTH ? text : text[..MAX_PROCESSING_ERROR_LENGTH];
    }

    /// <summary>Creates a new revision of an existing report linked via OriginalReportId.</summary>
    /// <param name="original">The report being revised.</param>
    /// <param name="newContent">Attachment content of the revision.</param>
    /// <param name="importDate">
    /// The calendar date of the revision — the user's <b>local</b> date, same semantics as
    /// <see cref="Create"/>.
    /// </param>
    /// <param name="revisedAt">
    /// The instant of the revision. Only used to build the <c>_revyyyyMMddHHmmss</c> name
    /// suffix, which is always rendered in <b>UTC</b> so revision names sort chronologically
    /// and are unaffected by time zone or DST changes.
    /// </param>
    public static Report CreateRevision(
        Report original, byte[]? newContent, DateOnly importDate, DateTimeOffset revisedAt)
    {
        if (original is null) throw new DomainException("Original report must not be null");
        var suffix = string.Create(CultureInfo.InvariantCulture, $"_rev{revisedAt.UtcDateTime:yyyyMMddHHmmss}");
        var baseName = original.ReportName.Length + suffix.Length > 500
            ? original.ReportName[..(500 - suffix.Length)]
            : original.ReportName;
        var revName = baseName + suffix;

        return new Report
        {
            Id = Guid.NewGuid(),
            ImporterId = original.ImporterId,
            ImportDate = importDate,
            Status = ReportStatus.Init,
            ReportName = revName,
            AttachmentContent = newContent,
            MailboxMessageId = original.MailboxMessageId,
            OriginalReportId = original.Id,
            EmailDate = original.EmailDate
        };
    }
}
