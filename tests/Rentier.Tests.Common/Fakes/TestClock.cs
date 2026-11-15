namespace Rentier.Tests.Common.Fakes;

/// <summary>
/// The default "today" for tests that need a date or a clock but do not care which one.
/// Tests that pin time-dependent behaviour should build their own <see cref="FixedTimeProvider"/>.
/// </summary>
public static class TestClock
{
    /// <summary>An arbitrary fixed Monday, far from any year boundary.</summary>
    public static readonly DateOnly TODAY = new(2025, 6, 16);

    /// <summary>A new clock fixed at noon UTC on <see cref="TODAY"/>, with UTC as the local zone.</summary>
    public static FixedTimeProvider Provider() => FixedTimeProvider.AtUtcNoon(TODAY);
}
