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
    public MailboxCursor Cursor { get; private set; } = null!;
    public DateOnly InitialSyncDate { get; private set; }

    private Mailbox() { }  // EF Core parameterless constructor

    public Mailbox(Guid id, string host, int port, string username, DateOnly initialSyncDate, MailboxCursor cursor)
    {
        if (string.IsNullOrWhiteSpace(host))
            throw new DomainException("Host must not be null or whitespace");
        if (port < 1 || port > 65535)
            throw new DomainException($"Port must be in range 1–65535, got {port}");
        if (string.IsNullOrWhiteSpace(username))
            throw new DomainException("Username must not be null or whitespace");
        ArgumentNullException.ThrowIfNull(cursor);

        Id = id;
        Host = host;
        Port = port;
        Username = username;
        InitialSyncDate = initialSyncDate;
        Cursor = cursor;
    }

    public static Mailbox Create(string host, int port, string username, DateOnly initialSyncDate)
    {
        return new Mailbox(
            Guid.NewGuid(),
            host,
            port,
            username,
            initialSyncDate,
            new MailboxCursor(LastSyncDate: initialSyncDate, LastUid: null));
    }

    public void UpdateDetails(string host, int port, string username, DateOnly initialSyncDate)
    {
        if (string.IsNullOrWhiteSpace(host))
            throw new DomainException("Host must not be null or whitespace");
        if (port < 1 || port > 65535)
            throw new DomainException($"Port must be in range 1–65535, got {port}");
        if (string.IsNullOrWhiteSpace(username))
            throw new DomainException("Username must not be null or whitespace");

        Host = host;
        Port = port;
        Username = username;
        InitialSyncDate = initialSyncDate;
    }

    public void UpdateCursor(MailboxCursor cursor)
    {
        ArgumentNullException.ThrowIfNull(cursor);
        Cursor = cursor;
    }
}
