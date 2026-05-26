using FluentAssertions;
using Rentier.Application.DTOs;
using Xunit;

namespace Rentier.UnitTests;

/// <summary>
/// T005 — Unit tests for ReportProcessingDetail.ClassifySeverity and ToLogMessage.
/// </summary>
public class ReportProcessingDetailTests
{
    // ── ClassifySeverity ─────────────────────────────────────────────────────

    [Fact]
    public void ClassifySeverity_AllSuccess_ReturnsInfo()
    {
        var severity = ReportProcessingDetail.ClassifySeverity(created: 3, failed: 0);
        severity.Should().Be(SyncProgressSeverity.Info);
    }

    [Fact]
    public void ClassifySeverity_EmptyReport_ReturnsInfo()
    {
        // Empty report: 0 created, 0 failed — treated as all-success
        var severity = ReportProcessingDetail.ClassifySeverity(created: 0, failed: 0);
        severity.Should().Be(SyncProgressSeverity.Info);
    }

    [Fact]
    public void ClassifySeverity_PartialFailure_ReturnsWarning()
    {
        var severity = ReportProcessingDetail.ClassifySeverity(created: 2, failed: 1);
        severity.Should().Be(SyncProgressSeverity.Warning);
    }

    [Fact]
    public void ClassifySeverity_TotalFailure_ReturnsError()
    {
        var severity = ReportProcessingDetail.ClassifySeverity(created: 0, failed: 2);
        severity.Should().Be(SyncProgressSeverity.Error);
    }

    [Theory]
    [InlineData(1, 0, SyncProgressSeverity.Info)]
    [InlineData(10, 0, SyncProgressSeverity.Info)]
    [InlineData(0, 0, SyncProgressSeverity.Info)]
    [InlineData(1, 1, SyncProgressSeverity.Warning)]
    [InlineData(5, 3, SyncProgressSeverity.Warning)]
    [InlineData(0, 1, SyncProgressSeverity.Error)]
    [InlineData(0, 5, SyncProgressSeverity.Error)]
    public void ClassifySeverity_AllCases(int created, int failed, SyncProgressSeverity expected)
    {
        ReportProcessingDetail.ClassifySeverity(created, failed).Should().Be(expected);
    }

    // ── ToLogMessage ─────────────────────────────────────────────────────────

    [Fact]
    public void ToLogMessage_FormatsCorrectly()
    {
        var detail = new ReportProcessingDetail("foo.csv", 3, 0, SyncProgressSeverity.Info);
        detail.ToLogMessage().Should().Be("Report 'foo.csv': 3 filing(s) created, 0 failed.");
    }

    [Fact]
    public void ToLogMessage_PartialFailure_FormatsCorrectly()
    {
        var detail = new ReportProcessingDetail("bar.csv", 2, 1, SyncProgressSeverity.Warning);
        detail.ToLogMessage().Should().Be("Report 'bar.csv': 2 filing(s) created, 1 failed.");
    }

    [Fact]
    public void ToLogMessage_TotalFailure_FormatsCorrectly()
    {
        var detail = new ReportProcessingDetail("error.csv", 0, 2, SyncProgressSeverity.Error);
        detail.ToLogMessage().Should().Be("Report 'error.csv': 0 filing(s) created, 2 failed.");
    }

    [Fact]
    public void ToLogMessage_EmptyReport_FormatsCorrectly()
    {
        var detail = new ReportProcessingDetail("empty.csv", 0, 0, SyncProgressSeverity.Info);
        detail.ToLogMessage().Should().Be("Report 'empty.csv': 0 filing(s) created, 0 failed.");
    }
}
