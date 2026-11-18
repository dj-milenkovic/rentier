using FluentAssertions;
using Rentier.Domain.Enums;
using Rentier.Domain.Exceptions;
using Rentier.Domain.ValueObjects;
using Xunit;
using Rentier.Tests.Common.Fakes;

namespace Rentier.UnitTests;

public class SyncParametersTests
{
    private static readonly DateOnly TODAY = TestClock.TODAY;

    [Fact]
    public void Default_CreatesIncremental()
    {
        var p = SyncParameters.Default;

        p.Mode.Should().Be(SyncMode.Incremental);
        p.ReplayFromDate.Should().BeNull();
    }

    [Fact]
    public void Constructor_ReplayFromDateMode_ThrowsDomainException()
    {
        var act = () => new SyncParameters(SyncMode.ReplayFromDate);
        act.Should().Throw<DomainException>().WithMessage("*ReplayFrom*");
    }

    [Fact]
    public void ReplayFrom_ValidDate_SetsModeAndDate()
    {
        var p = SyncParameters.ReplayFrom(new DateOnly(2024, 1, 1), TODAY);

        p.Mode.Should().Be(SyncMode.ReplayFromDate);
        p.ReplayFromDate.Should().Be(new DateOnly(2024, 1, 1));
    }

    [Fact]
    public void ReplayFrom_FutureDate_ThrowsDomainException()
    {
        var act = () => SyncParameters.ReplayFrom(TODAY.AddDays(1), TODAY);
        act.Should().Throw<DomainException>().WithMessage("*future*");
    }

    [Fact]
    public void ReplayFrom_Today_Succeeds()
    {
        var p = SyncParameters.ReplayFrom(TODAY, TODAY);
        p.ReplayFromDate.Should().Be(TODAY);
    }

    [Fact]
    public void ReplayFrom_NewYearsDayWhenTodayIsNewYearsEve_ThrowsDomainException()
    {
        var act = () => SyncParameters.ReplayFrom(new DateOnly(2026, 1, 1), new DateOnly(2025, 12, 31));
        act.Should().Throw<DomainException>().WithMessage("*future*");
    }

    [Fact]
    public void ReplayFrom_NewYearsEveWhenTodayIsNewYearsDay_Succeeds()
    {
        var p = SyncParameters.ReplayFrom(new DateOnly(2025, 12, 31), new DateOnly(2026, 1, 1));
        p.ReplayFromDate.Should().Be(new DateOnly(2025, 12, 31));
    }

    [Fact]
    public void Equality_IgnoresTodayUsedForValidation()
    {
        var replayDate = new DateOnly(2024, 1, 1);
        var a = SyncParameters.ReplayFrom(replayDate, TODAY);
        var b = SyncParameters.ReplayFrom(replayDate, TODAY.AddDays(1));
        a.Should().Be(b);
    }

    [Fact]
    public void ReplayFrom_PastDate_Succeeds()
    {
        var pastDate = new DateOnly(2024, 1, 1);
        var p = SyncParameters.ReplayFrom(pastDate, TODAY);
        p.ReplayFromDate.Should().Be(pastDate);
    }

    [Fact]
    public void GetEffectiveStartDate_IncrementalWithCursorDate_ReturnsCursorDate()
    {
        var cursor = new MailboxCursor.SyncedTo(new DateOnly(2024, 6, 1), null);
        var p = SyncParameters.Default;

        var result = p.GetEffectiveStartDate(cursor);

        result.Should().Be(new DateOnly(2024, 6, 1));
    }

    [Fact]
    public void GetEffectiveStartDate_ReplayFromDateMode_ReturnsReplayDate()
    {
        var replayDate = new DateOnly(2024, 3, 15);
        var cursor = new MailboxCursor.SyncedTo(new DateOnly(2024, 6, 1), 100L);
        var p = SyncParameters.ReplayFrom(replayDate, TODAY);

        var result = p.GetEffectiveStartDate(cursor);

        result.Should().Be(replayDate);
    }

    [Fact]
    public void GetEffectiveStartDate_FullReplayMode_ReturnsNull()
    {
        var cursor = new MailboxCursor.SyncedTo(new DateOnly(2024, 6, 1), 100L);
        var p = new SyncParameters(SyncMode.FullReplay);

        var result = p.GetEffectiveStartDate(cursor);

        result.Should().BeNull();
    }

    [Fact]
    public void GetEffectiveStartDate_UnknownSyncMode_ThrowsInvalidOperationException()
    {
        // Cast to an undefined enum value; constructor only guards specific branches so this succeeds.
        var p = new SyncParameters((SyncMode)999);

        var act = () => p.GetEffectiveStartDate(null);

        act.Should().Throw<InvalidOperationException>().WithMessage("*Unknown SyncMode*");
    }
}
