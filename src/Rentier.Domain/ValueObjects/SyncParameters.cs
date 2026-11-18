using Rentier.Domain.Enums;
using Rentier.Domain.Exceptions;

namespace Rentier.Domain.ValueObjects;

/// <summary>
/// Immutable value object that controls how a single sync run behaves: which emails to fetch
/// (<see cref="Mode"/>) and, for ReplayFromDate mode, the date to start from. Reports already
/// imported are always skipped (de-duplicated by importer and name).
/// </summary>
public sealed record SyncParameters
{
    public SyncMode Mode { get; }
    public DateOnly? ReplayFromDate { get; }

    /// <summary>
    /// Creates parameters for an <see cref="SyncMode.Incremental"/> or
    /// <see cref="SyncMode.FullReplay"/> run. Replay-from-date runs need a date and the
    /// current date, so they must be created through <see cref="ReplayFrom"/>.
    /// </summary>
    /// <param name="mode">Incremental or FullReplay.</param>
    public SyncParameters(SyncMode mode = SyncMode.Incremental)
        : this(mode, null)
    {
        if (mode == SyncMode.ReplayFromDate)
            throw new DomainException("ReplayFromDate mode must be created with SyncParameters.ReplayFrom");
    }

    private SyncParameters(SyncMode mode, DateOnly? replayFromDate)
    {
        Mode = mode;
        ReplayFromDate = replayFromDate;
    }

    /// <summary>Creates parameters that re-fetch emails starting at <paramref name="replayFromDate"/>.</summary>
    /// <param name="replayFromDate">First day to re-fetch; must not be after <paramref name="today"/>.</param>
    /// <param name="today">
    /// The current calendar date, supplied by the caller (the value object never reads the
    /// clock) and not part of the value. The replay date is picked by the user on a local
    /// calendar, so callers pass the user's <b>local</b> date.
    /// </param>
    public static SyncParameters ReplayFrom(DateOnly replayFromDate, DateOnly today)
    {
        if (replayFromDate > today)
            throw new DomainException("ReplayFromDate cannot be in the future");
        return new SyncParameters(SyncMode.ReplayFromDate, replayFromDate);
    }

    /// <summary>
    /// Returns the effective IMAP query start date for this sync run given the current mailbox cursor.
    /// </summary>
    public DateOnly? GetEffectiveStartDate(MailboxCursor? cursor) =>
        Mode switch
        {
            SyncMode.Incremental => cursor is MailboxCursor.SyncedTo s ? s.Date : null,
            SyncMode.ReplayFromDate => ReplayFromDate,
            SyncMode.FullReplay => null,
            _ => throw new InvalidOperationException($"Unknown SyncMode: {Mode}")
        };

    /// <summary>Default incremental sync parameters.</summary>
    public static SyncParameters Default => new();
}
