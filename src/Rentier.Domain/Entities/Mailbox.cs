using Rentier.Domain.Exceptions;
using Rentier.Domain.ValueObjects;

namespace Rentier.Domain.Entities;

/// <summary>
/// Represents an IMAP mailbox connection configuration.
/// </summary>
public sealed class Mailbox
{
    public Guid Id { get; private set; }
    public string Host { get; private set; } = string.Empty;
    public int Port { get; private set; }
    public string Username { get; private set; } = string.Empty;

    // Private backing fields persisted by EF Core via shadow properties.
    // EF Core reads/writes these directly; the Cursor property computes the union type.
#pragma warning disable CS0169, CS0649
    private DateOnly? _cursorDate;
    private long? _cursorUid;
#pragma warning restore CS0169, CS0649

    /// <summary>
    /// The current synchronization position in the mailbox.
    /// Returns <see cref="MailboxCursor.SyncedTo"/> when a sync date is recorded,
    /// or <see cref="MailboxCursor.NeverSynced"/> when no sync has occurred.
    /// </summary>
    public MailboxCursor Cursor =>
        _cursorDate.HasValue
            ? new MailboxCursor.SyncedTo(_cursorDate.Value, _cursorUid)
            : MailboxCursor.NeverSynced.Instance;

    private Mailbox() { }  // EF Core parameterless constructor

    public Mailbox(Guid id, string host, int port, string username, MailboxCursor cursor)
    {
        if (string.IsNullOrWhiteSpace(host))
            throw new DomainException("Host must not be null or whitespace");
        if (port < 1 || port > 65535)
            throw new DomainException($"Port must be in range 1-65535, got {port}");
        if (string.IsNullOrWhiteSpace(username))
            throw new DomainException("Username must not be null or whitespace");
        if (cursor is null) throw new DomainException("Cursor must not be null");
        Id = id;
        Host = host;
        Port = port;
        Username = username;
        UpdateCursor(cursor);
    }

    public static Mailbox Create(string host, int port, string username)
    {
        var defaultStart = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-90);
        return new Mailbox(
            Guid.NewGuid(),
            host,
            port,
            username,
            new MailboxCursor.SyncedTo(defaultStart, null));
    }

    public void UpdateDetails(string host, int port, string username)
    {
        if (string.IsNullOrWhiteSpace(host))
            throw new DomainException("Host must not be null or whitespace");
        if (port < 1 || port > 65535)
            throw new DomainException($"Port must be in range 1-65535, got {port}");
        if (string.IsNullOrWhiteSpace(username))
            throw new DomainException("Username must not be null or whitespace");

        Host = host;
        Port = port;
        Username = username;
    }

    public void UpdateCursor(MailboxCursor cursor)
    {
        if (cursor is null) throw new DomainException("Cursor must not be null");
        (_cursorDate, _cursorUid) = cursor switch
        {
            MailboxCursor.NeverSynced => ((DateOnly?)null, (long?)null),
            MailboxCursor.SyncedTo s => (s.Date, s.Uid),
            _ => throw new DomainException("Unknown MailboxCursor subtype")
        };
    }
}
