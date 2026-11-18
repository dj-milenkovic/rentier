namespace Rentier.Domain.Enums;

/// <summary>
/// Controls how the IMAP query start position is determined for a sync run.
/// </summary>
public enum SyncMode
{
    /// <summary>Fetch only messages newer than the mailbox cursor (default behaviour).</summary>
    Incremental = 0,
    /// <summary>Fetch all messages delivered on or after the specified <c>ReplayFromDate</c>, ignoring the cursor.</summary>
    ReplayFromDate = 1,
    /// <summary>Fetch all messages in the mailbox (no date filter).</summary>
    FullReplay = 2
}
