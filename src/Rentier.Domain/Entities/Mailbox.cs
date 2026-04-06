using Rentier.Domain.Exceptions;
using Rentier.Domain.ValueObjects;

namespace Rentier.Domain.Entities;

/// <summary>
/// Represents an IMAP mailbox connection configuration.
/// </summary>
public sealed class Mailbox
{
    public Guid Id { get; }
    public string Host { get; }
    public int Port { get; }
    public string Username { get; }
    public MailboxCursor Cursor { get; }

    public Mailbox(Guid id, string host, int port, string username, MailboxCursor cursor)
    {
        if (string.IsNullOrWhiteSpace(host))
            throw new DomainException("Host must not be null or whitespace");
        if (port < 1 || port > 65535)
            throw new DomainException($"Port must be in range 1–65535, got {port}");
        if (string.IsNullOrWhiteSpace(username))
            throw new DomainException("Username must not be null or whitespace");

        Id = id;
        Host = host;
        Port = port;
        Username = username;
        Cursor = cursor ?? throw new DomainException("Cursor must not be null");
    }
}
