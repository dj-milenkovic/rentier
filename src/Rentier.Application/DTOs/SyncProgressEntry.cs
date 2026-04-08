namespace Rentier.Application.DTOs;

public enum SyncProgressSeverity { Info, Warning, Error, CursorTransition, DuplicateHandled }

public sealed record SyncProgressEntry(
    DateTimeOffset Timestamp,
    string Message,
    SyncProgressSeverity Severity);
