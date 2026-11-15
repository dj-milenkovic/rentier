using FluentAssertions;
using Rentier.Domain.Entities;
using Rentier.Domain.Enums;
using Rentier.Domain.Exceptions;
using Xunit;

namespace Rentier.UnitTests;

public class ReportTests
{
    [Fact]
    public void Create_ValidArgs_ReturnsReportWithInitStatus()
    {
        var importerId = Guid.NewGuid();

        var report = Report.Create(importerId, "statement_report.csv", [1, 2, 3], 42L);

        report.Should().NotBeNull();
        report.ImporterId.Should().Be(importerId);
        report.ReportName.Should().Be("statement_report.csv");
        report.Status.Should().Be(ReportStatus.Init);
        report.MailboxMessageId.Should().Be(42L);
    }

    [Fact]
    public void Create_EmptyReportName_ThrowsDomainException()
    {
        var act = () => Report.Create(Guid.NewGuid(), "  ", null, null);

        act.Should().Throw<DomainException>().WithMessage("*ReportName must not be empty*");
    }

    [Fact]
    public void Create_ReportNameOver500Chars_ThrowsDomainException()
    {
        var longName = new string('x', 501);

        var act = () => Report.Create(Guid.NewGuid(), longName, null, null);

        act.Should().Throw<DomainException>().WithMessage("*ReportName must not exceed 500 characters*");
    }

    [Fact]
    public void Create_NullAttachment_ReportContentIsNull()
    {
        var report = Report.Create(Guid.NewGuid(), "report.csv", null, null);

        report.AttachmentContent.Should().BeNull();
    }

    [Fact]
    public void SetStatus_ValidTransition_UpdatesStatus()
    {
        var report = Report.Create(Guid.NewGuid(), "report.csv", null, null);

        report.SetStatus(ReportStatus.Processed);

        report.Status.Should().Be(ReportStatus.Processed);
    }

    [Fact]
    public void Create_SetsImportDateToToday()
    {
        var before = DateOnly.FromDateTime(DateTime.UtcNow);

        var report = Report.Create(Guid.NewGuid(), "report.csv", null, null);

        var after = DateOnly.FromDateTime(DateTime.UtcNow);
        report.ImportDate.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public void CreateRevision_LinksToOriginalViaOriginalReportId()
    {
        var original = Report.Create(Guid.NewGuid(), "report.csv", [1, 2, 3], 42L);

        var revision = Report.CreateRevision(original, [4, 5, 6]);

        revision.OriginalReportId.Should().Be(original.Id);
    }

    [Fact]
    public void CreateRevision_HasInitStatus()
    {
        var original = Report.Create(Guid.NewGuid(), "report.csv", null, null);
        original.SetStatus(ReportStatus.Processed);

        var revision = Report.CreateRevision(original, null);

        revision.Status.Should().Be(ReportStatus.Init);
    }

    [Fact]
    public void CreateRevision_HasNewId()
    {
        var original = Report.Create(Guid.NewGuid(), "report.csv", null, null);

        var revision = Report.CreateRevision(original, null);

        revision.Id.Should().NotBe(original.Id);
        revision.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void CreateRevision_LongName_TruncatesWithSuffix()
    {
        var longName = new string('x', 490);
        var original = Report.Create(Guid.NewGuid(), longName, null, null);

        var revision = Report.CreateRevision(original, null);

        revision.ReportName.Length.Should().BeLessThanOrEqualTo(500);
        revision.ReportName.Should().Contain("_rev");
    }

    [Fact]
    public void SetStatus_ToError_WhenInit_SetsStatusToError()
    {
        var report = Report.Create(Guid.NewGuid(), "report.csv", null, null);

        report.SetStatus(ReportStatus.Error);

        report.Status.Should().Be(ReportStatus.Error);
    }

    [Fact]
    public void SetStatus_ToPartialError_WhenInit_SetsStatusToPartialError()
    {
        var report = Report.Create(Guid.NewGuid(), "report.csv", null, null);

        report.SetStatus(ReportStatus.PartialError);

        report.Status.Should().Be(ReportStatus.PartialError);
    }

    [Fact]
    public void SetStatus_WhenProcessed_ThrowsDomainException()
    {
        var report = Report.Create(Guid.NewGuid(), "report.csv", null, null);
        report.SetStatus(ReportStatus.Processed);

        var act = () => report.SetStatus(ReportStatus.Processed);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void SetStatus_WhenError_ThrowsDomainException()
    {
        var report = Report.Create(Guid.NewGuid(), "report.csv", null, null);
        report.SetStatus(ReportStatus.Error);

        var act = () => report.SetStatus(ReportStatus.Processed);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void SetStatus_WhenPartialError_ThrowsDomainException()
    {
        var report = Report.Create(Guid.NewGuid(), "report.csv", null, null);
        report.SetStatus(ReportStatus.PartialError);

        var act = () => report.SetStatus(ReportStatus.Processed);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_WithEmailDate_SetsEmailDate()
    {
        var emailDate = new DateOnly(2024, 3, 15);

        var report = Report.Create(Guid.NewGuid(), "report.csv", null, null, emailDate);

        report.EmailDate.Should().Be(emailDate);
    }

    [Fact]
    public void Create_WithNameRequiringTrim_TrimsReportName()
    {
        var report = Report.Create(Guid.NewGuid(), "  report.csv  ", null, null);

        report.ReportName.Should().Be("report.csv");
    }

    [Fact]
    public void Create_WithNameExactly500Chars_Succeeds()
    {
        var name = new string('x', 500);

        var act = () => Report.Create(Guid.NewGuid(), name, null, null);

        act.Should().NotThrow();
    }

    [Fact]
    public void CreateRevision_WithNullOriginal_ThrowsDomainException()
    {
        var act = () => Report.CreateRevision(null!, null);
        act.Should().Throw<DomainException>();
    }

    // ── Bounded retry: failed attempts, rejection reason ────────────────────

    private const int MAX_ATTEMPTS = 3;

    private static Report NewReport() => Report.Create(Guid.NewGuid(), "statement.csv", [1, 2, 3], null);

    [Fact]
    public void RecordFailedAttempt_BelowLimit_KeepsReportInit()
    {
        var report = NewReport();

        report.RecordFailedAttempt("NBS unreachable", MAX_ATTEMPTS);

        report.Status.Should().Be(ReportStatus.Init);
    }

    [Fact]
    public void RecordFailedAttempt_BelowLimit_ReturnsFalse()
    {
        var report = NewReport();

        var exhausted = report.RecordFailedAttempt("NBS unreachable", MAX_ATTEMPTS);

        exhausted.Should().BeFalse();
    }

    [Fact]
    public void RecordFailedAttempt_EachCall_IncrementsAttempts()
    {
        var report = NewReport();

        report.RecordFailedAttempt("first", MAX_ATTEMPTS);
        report.RecordFailedAttempt("second", MAX_ATTEMPTS);

        report.ProcessingAttempts.Should().Be(2);
    }

    [Fact]
    public void RecordFailedAttempt_EachCall_KeepsLatestReason()
    {
        var report = NewReport();

        report.RecordFailedAttempt("first", MAX_ATTEMPTS);
        report.RecordFailedAttempt("second", MAX_ATTEMPTS);

        report.LastProcessingError.Should().Be("second");
    }

    [Fact]
    public void RecordFailedAttempt_ReachingLimit_MovesReportToError()
    {
        var report = NewReport();

        for (var i = 0; i < MAX_ATTEMPTS; i++)
            report.RecordFailedAttempt("still failing", MAX_ATTEMPTS);

        report.Status.Should().Be(ReportStatus.Error);
    }

    [Fact]
    public void RecordFailedAttempt_ReachingLimit_ReturnsTrue()
    {
        var report = NewReport();
        for (var i = 1; i < MAX_ATTEMPTS; i++)
            report.RecordFailedAttempt("still failing", MAX_ATTEMPTS);

        var exhausted = report.RecordFailedAttempt("still failing", MAX_ATTEMPTS);

        exhausted.Should().BeTrue();
    }

    [Fact]
    public void RecordFailedAttempt_ReportNotInit_ThrowsDomainException()
    {
        var report = NewReport();
        report.SetStatus(ReportStatus.Processed);

        var act = () => report.RecordFailedAttempt("late failure", MAX_ATTEMPTS);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void RecordFailedAttempt_MaxAttemptsBelowOne_ThrowsDomainException()
    {
        var report = NewReport();

        var act = () => report.RecordFailedAttempt("failure", 0);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void RecordFailedAttempt_OverlongReason_TruncatesToMaxLength()
    {
        var report = NewReport();

        report.RecordFailedAttempt(new string('x', Report.MAX_PROCESSING_ERROR_LENGTH + 50), MAX_ATTEMPTS);

        report.LastProcessingError!.Length.Should().Be(Report.MAX_PROCESSING_ERROR_LENGTH);
    }

    [Fact]
    public void RecordFailedAttempt_BlankReason_StoresPlaceholder()
    {
        var report = NewReport();

        report.RecordFailedAttempt("   ", MAX_ATTEMPTS);

        report.LastProcessingError.Should().Be("Unknown error");
    }

    [Fact]
    public void Reject_InitReport_MovesToError()
    {
        var report = NewReport();

        report.Reject("parse failed");

        report.Status.Should().Be(ReportStatus.Error);
    }

    [Fact]
    public void Reject_InitReport_StoresReason()
    {
        var report = NewReport();

        report.Reject("parse failed");

        report.LastProcessingError.Should().Be("parse failed");
    }

    [Fact]
    public void SetStatus_ProcessedAfterFailedAttempt_ClearsLastProcessingError()
    {
        var report = NewReport();
        report.RecordFailedAttempt("NBS unreachable", MAX_ATTEMPTS);

        report.SetStatus(ReportStatus.Processed);

        report.LastProcessingError.Should().BeNull();
    }

    [Fact]
    public void SetStatus_ProcessedAfterFailedAttempt_KeepsAttemptCount()
    {
        var report = NewReport();
        report.RecordFailedAttempt("NBS unreachable", MAX_ATTEMPTS);

        report.SetStatus(ReportStatus.Processed);

        report.ProcessingAttempts.Should().Be(1);
    }
}
