namespace Rentier.Application.DTOs;

public sealed record SyncAllResult(
    int MailboxesSynced,
    int AttachmentsDownloaded,
    int ReportsProcessed,
    int FilingsCreated,
    int ReportsSkipped,
    int RevisionsCreated,
    int ReportsReprocessed,
    IReadOnlyList<string> Errors)
{
    public SyncAllResult(int mailboxesSynced, int attachmentsDownloaded, int reportsProcessed, int filingsCreated, IReadOnlyList<string> errors)
        : this(mailboxesSynced, attachmentsDownloaded, reportsProcessed, filingsCreated, 0, 0, 0, errors) { }
}
