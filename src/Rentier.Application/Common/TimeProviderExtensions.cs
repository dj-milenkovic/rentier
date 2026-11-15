namespace Rentier.Application.Common;

/// <summary>
/// The single place where an instant from <see cref="TimeProvider"/> is turned into a
/// calendar date. Handlers and services must go through these helpers (never
/// <c>DateTime.Now</c>/<c>UtcNow</c>/<c>Today</c>) so that the choice between the local and
/// the UTC calendar is explicit at every call site.
/// </summary>
public static class TimeProviderExtensions
{
    /// <summary>
    /// Today's date on the user's <b>local</b> calendar (<see cref="TimeProvider.LocalTimeZone"/>).
    /// Use for anything the taxpayer reasons about as "today": filing deadlines (overdue /
    /// upcoming), the holiday-seeding year, report import dates, and date-picker bounds.
    /// PP-OPO deadlines are Serbian calendar dates and the app runs on the taxpayer's machine,
    /// so the local date - not the UTC date, which lags by 1-2 hours in Serbia - is authoritative.
    /// </summary>
    public static DateOnly GetLocalToday(this TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        return DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
    }

    /// <summary>
    /// Today's date on the <b>UTC</b> calendar. Use only for machine-facing bookkeeping that is
    /// defined in UTC - currently the IMAP mailbox cursor (email dates are normalised to UTC).
    /// </summary>
    public static DateOnly GetUtcToday(this TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        return DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
    }
}
