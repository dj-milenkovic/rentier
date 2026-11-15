using Rentier.Domain.Enums;
using Rentier.Domain.Exceptions;

namespace Rentier.Domain.ValueObjects;

/// <summary>
/// Immutable value object that controls how a single sync run behaves:
/// which emails to fetch (<see cref="Mode"/>), how duplicates are handled (<see cref="Strategy"/>),
/// an optional date override for ReplayFromDate mode, and an optional importer scope for FullReplay mode.
/// </summary>
public sealed record SyncParameters
{
    public SyncMode Mode { get; }
    public DuplicateStrategy Strategy { get; }
    public DateOnly? ReplayFromDate { get; }
    public Guid? ScopeImporterId { get; }

    /// <param name="mode">Which emails to fetch.</param>
    /// <param name="strategy">How duplicates are handled.</param>
    /// <param name="replayFromDate">Required for (and only allowed in) ReplayFromDate mode.</param>
    /// <param name="scopeImporterId">Optional importer scope.</param>
    /// <param name="today">
    /// The current calendar date, supplied by the caller (the value object never reads the
    /// clock). Required in ReplayFromDate mode, where it bounds <paramref name="replayFromDate"/>;
    /// ignored otherwise and not part of the value. The replay date is picked by the user on a
    /// local calendar, so callers pass the user's <b>local</b> date.
    /// </param>
    public SyncParameters(
        SyncMode mode = SyncMode.Incremental,
        DuplicateStrategy strategy = DuplicateStrategy.SkipExisting,
        DateOnly? replayFromDate = null,
        Guid? scopeImporterId = null,
        DateOnly? today = null)
    {
        if (mode == SyncMode.ReplayFromDate)
        {
            if (replayFromDate is null)
                throw new DomainException("ReplayFromDate is required for ReplayFromDate mode");
            if (today is null)
                throw new DomainException("Today is required to validate ReplayFromDate mode");
            if (replayFromDate.Value > today.Value)
                throw new DomainException("ReplayFromDate cannot be in the future");
        }
        else
        {
            if (replayFromDate is not null)
                throw new DomainException("ReplayFromDate must be null for non-ReplayFromDate modes");
        }

        Mode = mode;
        Strategy = strategy;
        ReplayFromDate = replayFromDate;
        ScopeImporterId = scopeImporterId;
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

    /// <summary>Default incremental sync parameters (SkipExisting duplicate strategy).</summary>
    public static SyncParameters Default => new();
}
