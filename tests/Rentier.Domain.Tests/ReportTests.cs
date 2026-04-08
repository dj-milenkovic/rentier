using FluentAssertions;
using Rentier.Domain.Entities;
using Rentier.Domain.Enums;
using Rentier.Domain.Exceptions;
using Xunit;

namespace Rentier.Domain.Tests;

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
}
