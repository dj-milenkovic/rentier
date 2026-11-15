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
    public void Default_CreatesIncrementalSkipExisting()
    {
        var p = SyncParameters.Default;

        p.Mode.Should().Be(SyncMode.Incremental);
        p.Strategy.Should().Be(DuplicateStrategy.SkipExisting);
        p.ReplayFromDate.Should().BeNull();
        p.ScopeImporterId.Should().BeNull();
    }

    [Fact]
    public void Constructor_ReplayFromDateMode_WithoutDate_ThrowsDomainException()
    {
        var act = () => new SyncParameters(SyncMode.ReplayFromDate);
        act.Should().Throw<DomainException>().WithMessage("*ReplayFromDate*required*");
    }

    [Fact]
    public void Constructor_ReplayFromDateMode_FutureDate_ThrowsDomainException()
    {
        var act = () => new SyncParameters(
            SyncMode.ReplayFromDate, replayFromDate: TODAY.AddDays(1), today: TODAY);
        act.Should().Throw<DomainException>().WithMessage("*future*");
    }

    [Fact]
    public void Constructor_ReplayFromDateMode_Today_Succeeds()
    {
        var p = new SyncParameters(SyncMode.ReplayFromDate, replayFromDate: TODAY, today: TODAY);
        p.ReplayFromDate.Should().Be(TODAY);
    }

    [Fact]
    public void Constructor_ReplayFromDateMode_WithoutToday_ThrowsDomainException()
    {
        var act = () => new SyncParameters(SyncMode.ReplayFromDate, replayFromDate: new DateOnly(2024, 1, 1));
        act.Should().Throw<DomainException>().WithMessage("*Today*required*");
    }

    [Fact]
    public void Constructor_ReplayFromDateMode_NewYearsDayWhenTodayIsNewYearsEve_ThrowsDomainException()
    {
        var act = () => new SyncParameters(
            SyncMode.ReplayFromDate, replayFromDate: new DateOnly(2026, 1, 1), today: new DateOnly(2025, 12, 31));
        act.Should().Throw<DomainException>().WithMessage("*future*");
    }

    [Fact]
    public void Constructor_ReplayFromDateMode_NewYearsEveWhenTodayIsNewYearsDay_Succeeds()
    {
        var p = new SyncParameters(
            SyncMode.ReplayFromDate, replayFromDate: new DateOnly(2025, 12, 31), today: new DateOnly(2026, 1, 1));
        p.ReplayFromDate.Should().Be(new DateOnly(2025, 12, 31));
    }

    [Fact]
    public void Equality_IgnoresTodayUsedForValidation()
    {
        var replayDate = new DateOnly(2024, 1, 1);
        var a = new SyncParameters(SyncMode.ReplayFromDate, replayFromDate: replayDate, today: TODAY);
        var b = new SyncParameters(SyncMode.ReplayFromDate, replayFromDate: replayDate, today: TODAY.AddDays(1));
        a.Should().Be(b);
    }

    [Fact]
    public void Constructor_ReplayFromDateMode_PastDate_Succeeds()
    {
        var pastDate = new DateOnly(2024, 1, 1);
        var p = new SyncParameters(SyncMode.ReplayFromDate, replayFromDate: pastDate, today: TODAY);
        p.ReplayFromDate.Should().Be(pastDate);
    }

    [Fact]
    public void Constructor_IncrementalMode_WithDate_ThrowsDomainException()
    {
        var date = new DateOnly(2024, 1, 1);
        var act = () => new SyncParameters(SyncMode.Incremental, replayFromDate: date);
        act.Should().Throw<DomainException>().WithMessage("*must be null*");
    }

    [Fact]
    public void Constructor_FullReplayMode_WithDate_ThrowsDomainException()
    {
        var date = new DateOnly(2024, 1, 1);
        var act = () => new SyncParameters(SyncMode.FullReplay, replayFromDate: date);
        act.Should().Throw<DomainException>().WithMessage("*must be null*");
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
        var p = new SyncParameters(SyncMode.ReplayFromDate, replayFromDate: replayDate, today: TODAY);

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
    public void Constructor_IncrementalWithScopeImporterId_Succeeds()
    {
        var importerId = Guid.NewGuid();
        var p = new SyncParameters(SyncMode.Incremental, scopeImporterId: importerId);
        p.ScopeImporterId.Should().Be(importerId);
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
