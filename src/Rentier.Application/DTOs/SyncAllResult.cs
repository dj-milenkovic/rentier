namespace Rentier.Application.DTOs;

public sealed record SyncAllResult(
    int MailboxesSynced,
    int AttachmentsDownloaded,
    int ReportsProcessed,
    int FilingsCreated,
    IReadOnlyList<string> Errors);
