namespace Rentier.Application.Commands;

public sealed record UpdateMailboxCommand(
    Guid Id,
    string Host,
    int Port,
    string Username,
    string? Password);
