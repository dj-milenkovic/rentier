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
