using FluentAssertions;
using Rentier.Domain.Entities;
using Rentier.Domain.Enums;
using Rentier.Domain.Exceptions;
using Xunit;
using Rentier.Tests.Common.Fakes;

namespace Rentier.UnitTests;

public class ReportTests
{
    private static readonly DateTimeOffset REVISED_AT = new(2025, 6, 16, 10, 30, 45, TimeSpan.Zero);

    [Fact]
    public void Create_ValidArgs_ReturnsReportWithInitStatus()
    {
        var importerId = Guid.NewGuid();

        var report = Report.Create(importerId, "statement_report.csv", [1, 2, 3], 42L, TestClock.TODAY);

        report.Should().NotBeNull();
        report.ImporterId.Should().Be(importerId);
        report.ReportName.Should().Be("statement_report.csv");
        report.Status.Should().Be(ReportStatus.Init);
        report.MailboxMessageId.Should().Be(42L);
    }

    [Fact]
    public void Create_EmptyReportName_ThrowsDomainException()
    {
        var act = () => Report.Create(Guid.NewGuid(), "  ", null, null, TestClock.TODAY);

        act.Should().Throw<DomainException>().WithMessage("*ReportName must not be empty*");
    }

    [Fact]
    public void Create_ReportNameOver500Chars_ThrowsDomainException()
    {
        var longName = new string('x', 501);

        var act = () => Report.Create(Guid.NewGuid(), longName, null, null, TestClock.TODAY);

        act.Should().Throw<DomainException>().WithMessage("*ReportName must not exceed 500 characters*");
    }

    [Fact]
    public void Create_NullAttachment_ReportContentIsNull()
    {
        var report = Report.Create(Guid.NewGuid(), "report.csv", null, null, TestClock.TODAY);

        report.AttachmentContent.Should().BeNull();
    }

    [Fact]
    public void SetStatus_ValidTransition_UpdatesStatus()
    {
        var report = Report.Create(Guid.NewGuid(), "report.csv", null, null, TestClock.TODAY);

        report.SetStatus(ReportStatus.Processed);

        report.Status.Should().Be(ReportStatus.Processed);
    }

    [Fact]
    public void Create_SetsImportDateToSuppliedDate()
    {
        var importDate = new DateOnly(2025, 12, 31);

        var report = Report.Create(Guid.NewGuid(), "report.csv", null, null, importDate);

        report.ImportDate.Should().Be(importDate);
    }

    [Fact]
    public void Create_ImportDateAndEmailDate_AreStoredIndependently()
    {
        var report = Report.Create(
            Guid.NewGuid(), "report.csv", null, null, new DateOnly(2026, 1, 1), new DateOnly(2025, 12, 31));

        (report.ImportDate, report.EmailDate).Should().Be((new DateOnly(2026, 1, 1), new DateOnly(2025, 12, 31)));
    }

    [Fact]
    public void CreateRevision_SetsImportDateToSuppliedDate()
    {
        var original = Report.Create(Guid.NewGuid(), "report.csv", null, null, new DateOnly(2025, 12, 31));

        var revision = Report.CreateRevision(original, null, new DateOnly(2026, 1, 1), REVISED_AT);

        revision.ImportDate.Should().Be(new DateOnly(2026, 1, 1));
    }

    [Fact]
    public void CreateRevision_NameSuffix_IsTheSuppliedInstant()
    {
        var original = Report.Create(Guid.NewGuid(), "report.csv", null, null, TestClock.TODAY);

        var revision = Report.CreateRevision(original, null, TestClock.TODAY, REVISED_AT);

        revision.ReportName.Should().Be("report.csv_rev20250616103045");
    }

    [Fact]
    public void CreateRevision_NameSuffix_IsRenderedInUtcAcrossYearBoundary()
    {
        // 2026-01-01 00:30 local (UTC+1) is still 2025-12-31 23:30 UTC: the suffix uses the UTC
        // instant while the import date is the caller's local calendar date.
        var original = Report.Create(Guid.NewGuid(), "report.csv", null, null, TestClock.TODAY);
        var revisedAt = new DateTimeOffset(2026, 1, 1, 0, 30, 0, TimeSpan.FromHours(1));

        var revision = Report.CreateRevision(original, null, new DateOnly(2026, 1, 1), revisedAt);

        revision.ReportName.Should().Be("report.csv_rev20251231233000");
    }

    [Fact]
    public void CreateRevision_SameInputs_ProduceSameName()
    {
        var original = Report.Create(Guid.NewGuid(), "report.csv", null, null, TestClock.TODAY);

        var first = Report.CreateRevision(original, null, TestClock.TODAY, REVISED_AT);
        var second = Report.CreateRevision(original, null, TestClock.TODAY, REVISED_AT);

        second.ReportName.Should().Be(first.ReportName);
    }

    [Fact]
    public void CreateRevision_LinksToOriginalViaOriginalReportId()
    {
        var original = Report.Create(Guid.NewGuid(), "report.csv", [1, 2, 3], 42L, TestClock.TODAY);

        var revision = Report.CreateRevision(original, [4, 5, 6], TestClock.TODAY, REVISED_AT);

        revision.OriginalReportId.Should().Be(original.Id);
    }

    [Fact]
    public void CreateRevision_HasInitStatus()
    {
        var original = Report.Create(Guid.NewGuid(), "report.csv", null, null, TestClock.TODAY);
        original.SetStatus(ReportStatus.Processed);

        var revision = Report.CreateRevision(original, null, TestClock.TODAY, REVISED_AT);

        revision.Status.Should().Be(ReportStatus.Init);
    }

    [Fact]
    public void CreateRevision_HasNewId()
    {
        var original = Report.Create(Guid.NewGuid(), "report.csv", null, null, TestClock.TODAY);

        var revision = Report.CreateRevision(original, null, TestClock.TODAY, REVISED_AT);

        revision.Id.Should().NotBe(original.Id);
        revision.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void CreateRevision_LongName_TruncatesWithSuffix()
    {
        var longName = new string('x', 490);
        var original = Report.Create(Guid.NewGuid(), longName, null, null, TestClock.TODAY);

        var revision = Report.CreateRevision(original, null, TestClock.TODAY, REVISED_AT);

        revision.ReportName.Length.Should().BeLessThanOrEqualTo(500);
        revision.ReportName.Should().Contain("_rev");
    }

    [Fact]
    public void SetStatus_ToError_WhenInit_SetsStatusToError()
    {
        var report = Report.Create(Guid.NewGuid(), "report.csv", null, null, TestClock.TODAY);

        report.SetStatus(ReportStatus.Error);

        report.Status.Should().Be(ReportStatus.Error);
    }

    [Fact]
    public void SetStatus_ToPartialError_WhenInit_SetsStatusToPartialError()
    {
        var report = Report.Create(Guid.NewGuid(), "report.csv", null, null, TestClock.TODAY);

        report.SetStatus(ReportStatus.PartialError);

        report.Status.Should().Be(ReportStatus.PartialError);
    }

    [Fact]
    public void SetStatus_WhenProcessed_ThrowsDomainException()
    {
        var report = Report.Create(Guid.NewGuid(), "report.csv", null, null, TestClock.TODAY);
        report.SetStatus(ReportStatus.Processed);

        var act = () => report.SetStatus(ReportStatus.Processed);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void SetStatus_WhenError_ThrowsDomainException()
    {
        var report = Report.Create(Guid.NewGuid(), "report.csv", null, null, TestClock.TODAY);
        report.SetStatus(ReportStatus.Error);

        var act = () => report.SetStatus(ReportStatus.Processed);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void SetStatus_WhenPartialError_ThrowsDomainException()
    {
        var report = Report.Create(Guid.NewGuid(), "report.csv", null, null, TestClock.TODAY);
        report.SetStatus(ReportStatus.PartialError);

        var act = () => report.SetStatus(ReportStatus.Processed);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_WithEmailDate_SetsEmailDate()
    {
        var emailDate = new DateOnly(2024, 3, 15);

        var report = Report.Create(Guid.NewGuid(), "report.csv", null, null, TestClock.TODAY, emailDate);

        report.EmailDate.Should().Be(emailDate);
    }

    [Fact]
    public void Create_WithNameRequiringTrim_TrimsReportName()
    {
        var report = Report.Create(Guid.NewGuid(), "  report.csv  ", null, null, TestClock.TODAY);

        report.ReportName.Should().Be("report.csv");
    }

    [Fact]
    public void Create_WithNameExactly500Chars_Succeeds()
    {
        var name = new string('x', 500);

        var act = () => Report.Create(Guid.NewGuid(), name, null, null, TestClock.TODAY);

        act.Should().NotThrow();
    }

    [Fact]
    public void CreateRevision_WithNullOriginal_ThrowsDomainException()
    {
        var act = () => Report.CreateRevision(null!, null, TestClock.TODAY, REVISED_AT);
        act.Should().Throw<DomainException>();
    }

    // ── Bounded retry: failed attempts, rejection reason ────────────────────

    private const int MAX_ATTEMPTS = 3;

    private static Report NewReport() => Report.Create(Guid.NewGuid(), "statement.csv", [1, 2, 3], null, TestClock.TODAY);

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
