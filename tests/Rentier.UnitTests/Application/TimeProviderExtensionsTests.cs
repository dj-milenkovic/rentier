using FluentAssertions;
using Rentier.Application.Common;
using Rentier.Tests.Common.Fakes;
using Xunit;

namespace Rentier.UnitTests.Application;

/// <summary>
/// Pins the UTC-vs-local calendar semantics of "today". Serbia is UTC+1 (winter) / UTC+2
/// (summer), so for 1–2 hours after local midnight the UTC date is still yesterday.
/// </summary>
public class TimeProviderExtensionsTests
{
    private static FixedTimeProvider Clock(DateTimeOffset utcNow, double localOffsetHours) =>
        new(utcNow, FixedTimeProvider.FixedOffsetZone(TimeSpan.FromHours(localOffsetHours)));

    private static DateTimeOffset Utc(int year, int month, int day, int hour, int minute, int second = 0) =>
        new(year, month, day, hour, minute, second, TimeSpan.Zero);

    [Fact]
    public void GetLocalToday_LocalNewYearAlreadyPassedButUtcHasNot_ReturnsJanuaryFirst()
    {
        var clock = Clock(Utc(2025, 12, 31, 23, 30), localOffsetHours: 1);

        clock.GetLocalToday().Should().Be(new DateOnly(2026, 1, 1));
    }

    [Fact]
    public void GetUtcToday_LocalNewYearAlreadyPassedButUtcHasNot_ReturnsDecemberThirtyFirst()
    {
        var clock = Clock(Utc(2025, 12, 31, 23, 30), localOffsetHours: 1);

        clock.GetUtcToday().Should().Be(new DateOnly(2025, 12, 31));
    }

    [Fact]
    public void GetLocalToday_LastLocalSecondOfYear_ReturnsDecemberThirtyFirst()
    {
        var clock = Clock(Utc(2025, 12, 31, 22, 59, 59), localOffsetHours: 1);

        clock.GetLocalToday().Should().Be(new DateOnly(2025, 12, 31));
    }

    [Fact]
    public void GetLocalToday_FirstLocalInstantOfYear_ReturnsJanuaryFirst()
    {
        var clock = Clock(Utc(2025, 12, 31, 23, 0), localOffsetHours: 1);

        clock.GetLocalToday().Should().Be(new DateOnly(2026, 1, 1));
    }

    [Fact]
    public void GetLocalToday_LocalZoneBehindUtc_ReturnsEarlierLocalDate()
    {
        var clock = Clock(Utc(2026, 1, 1, 3, 0), localOffsetHours: -5);

        clock.GetLocalToday().Should().Be(new DateOnly(2025, 12, 31));
    }

    [Fact]
    public void GetLocalToday_SummerTimeOffset_UsesTwoHourOffset()
    {
        // 22:30 UTC on 30 June is 00:30 on 1 July in Serbian summer time (UTC+2).
        var clock = Clock(Utc(2025, 6, 30, 22, 30), localOffsetHours: 2);

        clock.GetLocalToday().Should().Be(new DateOnly(2025, 7, 1));
    }

    [Fact]
    public void GetLocalToday_LeapDay_ReturnsFebruaryTwentyNinth()
    {
        var clock = Clock(Utc(2028, 2, 28, 23, 30), localOffsetHours: 1);

        clock.GetLocalToday().Should().Be(new DateOnly(2028, 2, 29));
    }

    [Fact]
    public void GetLocalToday_Midday_MatchesUtcToday()
    {
        var clock = Clock(Utc(2025, 6, 16, 12, 0), localOffsetHours: 2);

        clock.GetLocalToday().Should().Be(clock.GetUtcToday());
    }

    [Fact]
    public void GetLocalToday_AfterClockAdvancesPastMidnight_ReturnsNextDay()
    {
        var clock = Clock(Utc(2025, 12, 31, 22, 59, 59), localOffsetHours: 1);

        clock.Advance(TimeSpan.FromSeconds(1));

        clock.GetLocalToday().Should().Be(new DateOnly(2026, 1, 1));
    }

    [Fact]
    public void GetLocalToday_NullProvider_Throws()
    {
        var act = () => ((TimeProvider)null!).GetLocalToday();

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GetUtcToday_NullProvider_Throws()
    {
        var act = () => ((TimeProvider)null!).GetUtcToday();

        act.Should().Throw<ArgumentNullException>();
    }
}
