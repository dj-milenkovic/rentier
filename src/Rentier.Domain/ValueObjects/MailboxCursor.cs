namespace Rentier.Domain.ValueObjects;

/// <summary>
/// Discriminated union representing the last-synced position in a mailbox.
/// Use <see cref="NeverSynced"/> when no sync has occurred, <see cref="SyncedTo"/> otherwise.
/// DateOnly per constitution Principle III.
/// Properties are get-only; <c>with</c> expressions on the abstract base are not possible.
/// </summary>
public abstract record MailboxCursor
{
    // Sealed to prevent external subtypes.
    private MailboxCursor() { }

    /// <summary>No sync has ever occurred for this mailbox.</summary>
    public sealed record NeverSynced : MailboxCursor
    {
        /// <summary>Singleton — never-synced cursors carry no data.</summary>
        public static readonly NeverSynced Instance = new();

        private NeverSynced() { }
    }

    /// <summary>Sync has occurred up to the given date, and optionally up to the given UID.</summary>
    public sealed record SyncedTo(DateOnly Date, long? Uid) : MailboxCursor;
}
