namespace Rentier.Application.DTOs;

public sealed record MailboxDto(
    Guid Id,
    string Host,
    int Port,
    string Username,
    DateOnly? LastSyncDate,
    long? LastUid);
