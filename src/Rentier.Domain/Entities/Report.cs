using Rentier.Domain.Enums;
using Rentier.Domain.Exceptions;

namespace Rentier.Domain.Entities;

/// <summary>
/// Represents a raw activity statement email attachment awaiting processing.
/// ImportDate is DateOnly per constitution Principle III.
/// </summary>
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

    /// <summary>Creates a new Report from an email attachment. ImportDate is always today (UTC).</summary>
    public static Report Create(
        Guid importerId,
        string reportName,
        byte[]? attachmentContent,
        long? mailboxMessageId,
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
            ImportDate = DateOnly.FromDateTime(DateTime.UtcNow),
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
            (ReportStatus.Init, ReportStatus.Processed)    => true,
            (ReportStatus.Init, ReportStatus.PartialError) => true,
            (ReportStatus.Init, ReportStatus.Error)        => true,
            _ => false
        };

        if (!isValid)
            throw new DomainException($"Invalid Report status transition: {Status} → {status}");

        Status = status;
    }

    /// <summary>Creates a new revision of an existing report linked via OriginalReportId.</summary>
    public static Report CreateRevision(Report original, byte[]? newContent)
    {
        if (original is null) throw new DomainException("Original report must not be null");
        var suffix = $"_rev{DateTime.UtcNow:yyyyMMddHHmmss}";
        var baseName = original.ReportName.Length + suffix.Length > 500
            ? original.ReportName[..(500 - suffix.Length)]
            : original.ReportName;
        var revName = baseName + suffix;

        return new Report
        {
            Id = Guid.NewGuid(),
            ImporterId = original.ImporterId,
            ImportDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Status = ReportStatus.Init,
            ReportName = revName,
            AttachmentContent = newContent,
            MailboxMessageId = original.MailboxMessageId,
            OriginalReportId = original.Id,
            EmailDate = original.EmailDate
        };
    }
}
