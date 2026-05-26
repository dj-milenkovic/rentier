using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Application.Interfaces;

namespace Rentier.Application.Handlers;

public sealed class SyncAllCommandHandler : ISyncAllCommandHandler
{
    private readonly ICommandHandler<SyncMailboxCommand, Result<SyncResult, Error>> _syncMailboxHandler;
    private readonly ICommandHandler<ProcessReportsCommand, Result<ProcessReportsResult, Error>> _processReportsHandler;

    public SyncAllCommandHandler(
        ICommandHandler<SyncMailboxCommand, Result<SyncResult, Error>> syncMailboxHandler,
        ICommandHandler<ProcessReportsCommand, Result<ProcessReportsResult, Error>> processReportsHandler)
    {
        _syncMailboxHandler = syncMailboxHandler;
        _processReportsHandler = processReportsHandler;
    }

    public async Task<Result<SyncAllResult, Error>> HandleAsync(
        SyncAllCommand command,
        IProgress<SyncProgressEntry> progress,
        CancellationToken ct = default)
    {
        var errors = new List<string>();
        int mailboxesSynced = 0, attachmentsDownloaded = 0, reportsProcessed = 0, filingsCreated = 0;

        // Phase 1: Mailbox sync
        progress.Report(new SyncProgressEntry(DateTimeOffset.Now, "Starting mailbox sync...", SyncProgressSeverity.Info));

        var syncResult = await _syncMailboxHandler.HandleAsync(
            new SyncMailboxCommand(command.Parameters), ct);

        if (syncResult.IsSuccess)
        {
            mailboxesSynced = 1;
            attachmentsDownloaded = syncResult.Value.ReportsCreated;
            foreach (var e in syncResult.Value.Errors)
            {
                errors.Add(e);
                progress.Report(new SyncProgressEntry(DateTimeOffset.Now, e, SyncProgressSeverity.Warning));
            }
            progress.Report(new SyncProgressEntry(DateTimeOffset.Now,
                $"Mailbox sync complete. {attachmentsDownloaded} attachment(s) downloaded.", SyncProgressSeverity.Info));
        }
        else
        {
            errors.Add(syncResult.Error.Message);
            progress.Report(new SyncProgressEntry(DateTimeOffset.Now, syncResult.Error.Message, SyncProgressSeverity.Error));
        }

        // Phase 2: Process reports (always runs)
        progress.Report(new SyncProgressEntry(DateTimeOffset.Now, "Processing reports...", SyncProgressSeverity.Info));

        var processResult = await _processReportsHandler.HandleAsync(new ProcessReportsCommand(Progress: progress), ct);

        if (processResult.IsSuccess)
        {
            filingsCreated = processResult.Value.FilingsCreated;
            reportsProcessed = processResult.Value.ReportsProcessed;
            foreach (var e in processResult.Value.EventErrors)
            {
                var eventMsg = $"{e.EntityName} {e.IncomeDate:yyyy-MM-dd}: {e.ErrorCode} - {e.Message}";
                errors.Add(eventMsg);
                progress.Report(new SyncProgressEntry(DateTimeOffset.Now, eventMsg, SyncProgressSeverity.Warning));
            }
            var statusMsg = reportsProcessed == 0
                ? "No new reports to process."
                : $"Processed {reportsProcessed} report(s), created {filingsCreated} filing(s).";
            progress.Report(new SyncProgressEntry(DateTimeOffset.Now, statusMsg, SyncProgressSeverity.Info));
        }
        else
        {
            errors.Add(processResult.Error.Message);
            progress.Report(new SyncProgressEntry(DateTimeOffset.Now, processResult.Error.Message, SyncProgressSeverity.Error));
        }

        return Result<SyncAllResult, Error>.Success(
            new SyncAllResult(mailboxesSynced, attachmentsDownloaded, reportsProcessed, filingsCreated, errors.AsReadOnly()));
    }
}
