namespace Rentier.Application.Commands;

public sealed record AddMailboxCommand(
    string Host,
    int Port,
    string Username,
    string? Password);
