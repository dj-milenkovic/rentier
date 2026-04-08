using FluentAssertions;
using Rentier.Domain.Enums;
using Rentier.Domain.Exceptions;
using Rentier.Domain.ValueObjects;
using Xunit;

namespace Rentier.Domain.Tests;

public class SyncParametersTests
{
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
        var futureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        var act = () => new SyncParameters(SyncMode.ReplayFromDate, replayFromDate: futureDate);
        act.Should().Throw<DomainException>().WithMessage("*future*");
    }

    [Fact]
    public void Constructor_ReplayFromDateMode_PastDate_Succeeds()
    {
        var pastDate = new DateOnly(2024, 1, 1);
        var p = new SyncParameters(SyncMode.ReplayFromDate, replayFromDate: pastDate);
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
        var cursor = new MailboxCursor(new DateOnly(2024, 6, 1), null);
        var p = SyncParameters.Default;

        var result = p.GetEffectiveStartDate(cursor);

        result.Should().Be(new DateOnly(2024, 6, 1));
    }

    [Fact]
    public void GetEffectiveStartDate_ReplayFromDateMode_ReturnsReplayDate()
    {
        var replayDate = new DateOnly(2024, 3, 15);
        var cursor = new MailboxCursor(new DateOnly(2024, 6, 1), 100L);
        var p = new SyncParameters(SyncMode.ReplayFromDate, replayFromDate: replayDate);

        var result = p.GetEffectiveStartDate(cursor);

        result.Should().Be(replayDate);
    }

    [Fact]
    public void GetEffectiveStartDate_FullReplayMode_ReturnsNull()
    {
        var cursor = new MailboxCursor(new DateOnly(2024, 6, 1), 100L);
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
}
