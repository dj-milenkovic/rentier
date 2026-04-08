namespace Rentier.Domain.ValueObjects;

/// <summary>
/// Represents the last-synced position in a mailbox.
/// Both nullable — null means no sync has occurred.
/// DateOnly per constitution Principle III.
/// </summary>
public record MailboxCursor(DateOnly? LastSyncDate, long? LastUid);
