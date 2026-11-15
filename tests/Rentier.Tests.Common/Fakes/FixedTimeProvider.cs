namespace Rentier.Tests.Common.Fakes;

/// <summary>
/// Deterministic <see cref="TimeProvider"/> for tests: a settable UTC instant and an explicit
/// local time zone, so tests can pin both "now" and the UTC-vs-local calendar date without
/// depending on the machine clock or the machine's time zone.
/// </summary>
public sealed class FixedTimeProvider : TimeProvider
{
    private readonly TimeZoneInfo _localTimeZone;
    private DateTimeOffset _utcNow;

    /// <param name="utcNow">The instant to report as "now".</param>
    /// <param name="localTimeZone">The zone reported as local. Defaults to UTC.</param>
    public FixedTimeProvider(DateTimeOffset utcNow, TimeZoneInfo? localTimeZone = null)
    {
        _utcNow = utcNow.ToUniversalTime();
        _localTimeZone = localTimeZone ?? TimeZoneInfo.Utc;
    }

    /// <summary>A clock at noon UTC on <paramref name="date"/> with UTC as the local zone.</summary>
    public static FixedTimeProvider AtUtcNoon(DateOnly date) =>
        new(new DateTimeOffset(date.ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero));

    /// <summary>
    /// A fixed-offset local zone (no DST), e.g. <c>FixedOffsetZone(TimeSpan.FromHours(1))</c>
    /// for Serbian winter time. Avoids relying on the OS time zone database.
    /// </summary>
    public static TimeZoneInfo FixedOffsetZone(TimeSpan offset) =>
        TimeZoneInfo.CreateCustomTimeZone($"Fixed{offset}", offset, $"Fixed{offset}", $"Fixed{offset}");

    public override TimeZoneInfo LocalTimeZone => _localTimeZone;

    public override DateTimeOffset GetUtcNow() => _utcNow;

    public void SetUtcNow(DateTimeOffset utcNow) => _utcNow = utcNow.ToUniversalTime();

    public void Advance(TimeSpan delta) => _utcNow += delta;
}
