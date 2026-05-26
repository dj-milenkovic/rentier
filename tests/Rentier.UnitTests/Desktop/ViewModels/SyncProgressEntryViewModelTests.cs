using FluentAssertions;
using Rentier.Application.DTOs;
using Rentier.Desktop.ViewModels;
using Xunit;

namespace Rentier.UnitTests;

/// <summary>
/// Unit tests for SyncProgressEntryViewModel — covers Icon derivation for every
/// severity value, message pass-through, timestamp formatting, and severity pass-through.
/// </summary>
public class SyncProgressEntryViewModelTests
{
    private static SyncProgressEntryViewModel Make(
        SyncProgressSeverity severity,
        string message = "Test message",
        DateTimeOffset? timestamp = null) =>
        new(new SyncProgressEntry(
            timestamp ?? new DateTimeOffset(2025, 6, 15, 14, 30, 45, TimeSpan.Zero),
            message,
            severity));

    [Fact]
    public void Icon_ErrorSeverity_ReturnsXMark()
    {
        var vm = Make(SyncProgressSeverity.Error);

        vm.Icon.Should().Be("✕");
    }

    [Fact]
    public void Icon_WarningSeverity_ReturnsWarningSymbol()
    {
        var vm = Make(SyncProgressSeverity.Warning);

        vm.Icon.Should().Be("⚠");
    }

    [Fact]
    public void Icon_InfoSeverity_ReturnsBullet()
    {
        var vm = Make(SyncProgressSeverity.Info);

        vm.Icon.Should().Be("•");
    }

    [Fact]
    public void Icon_CursorTransitionSeverity_ReturnsBullet()
    {
        var vm = Make(SyncProgressSeverity.CursorTransition);

        vm.Icon.Should().Be("•");
    }

    [Fact]
    public void Icon_DuplicateHandledSeverity_ReturnsBullet()
    {
        var vm = Make(SyncProgressSeverity.DuplicateHandled);

        vm.Icon.Should().Be("•");
    }

    [Fact]
    public void Message_FromEntry_IsPassedThrough()
    {
        const string expectedMessage = "Synced 42 messages";
        var vm = Make(SyncProgressSeverity.Info, message: expectedMessage);

        vm.Message.Should().Be(expectedMessage);
    }

    [Fact]
    public void Timestamp_FromEntry_IsFormattedAsHHmmss()
    {
        var ts = new DateTimeOffset(2025, 6, 15, 14, 30, 45, TimeSpan.Zero);
        var vm = Make(SyncProgressSeverity.Info, timestamp: ts);

        vm.Timestamp.Should().Be("14:30:45");
    }

    [Fact]
    public void Severity_FromEntry_IsPassedThrough()
    {
        var vm = Make(SyncProgressSeverity.Warning);

        vm.Severity.Should().Be(SyncProgressSeverity.Warning);
    }
}
