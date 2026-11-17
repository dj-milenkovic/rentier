using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Application.Interfaces;

namespace Rentier.Application.Handlers;

public sealed class SyncAllCommandHandler : ISyncAllCommandHandler
{
    private readonly ISyncMailboxCommandHandler _syncMailboxHandler;
    private readonly ICommandHandler<ProcessReportsCommand, Result<ProcessReportsResult, Error>> _processReportsHandler;
    private readonly TimeProvider _timeProvider;

    public SyncAllCommandHandler(
        ISyncMailboxCommandHandler syncMailboxHandler,
        ICommandHandler<ProcessReportsCommand, Result<ProcessReportsResult, Error>> processReportsHandler,
        TimeProvider timeProvider)
    {
        _syncMailboxHandler = syncMailboxHandler;
        _processReportsHandler = processReportsHandler;
        _timeProvider = timeProvider;
    }

    public async Task<Result<SyncAllResult, Error>> HandleAsync(
        SyncAllCommand command,
        IProgress<SyncProgressEntry> progress,
        CancellationToken ct = default)
    {
        var errors = new List<string>();
        int mailboxesSynced = 0, attachmentsDownloaded = 0, reportsProcessed = 0, filingsCreated = 0;

        // Phase 1: Mailbox sync
        progress.Report(new SyncProgressEntry(_timeProvider.GetLocalNow(), "Starting mailbox sync...", SyncProgressSeverity.Info));

        var syncResult = await _syncMailboxHandler.HandleAsync(
            new SyncMailboxCommand(command.Parameters), progress, ct);

        if (syncResult.IsSuccess)
        {
            mailboxesSynced = 1;
            attachmentsDownloaded = syncResult.Value.ReportsCreated;
            foreach (var e in syncResult.Value.Errors)
            {
                errors.Add(e);
                progress.Report(new SyncProgressEntry(_timeProvider.GetLocalNow(), e, SyncProgressSeverity.Warning));
            }
            progress.Report(new SyncProgressEntry(_timeProvider.GetLocalNow(),
                $"Mailbox sync complete. {attachmentsDownloaded} attachment(s) downloaded.", SyncProgressSeverity.Info));
        }
        else
        {
            errors.Add(syncResult.Error.Message);
            progress.Report(new SyncProgressEntry(_timeProvider.GetLocalNow(), syncResult.Error.Message, SyncProgressSeverity.Error));
        }

        // Phase 2: Process reports (always runs)
        progress.Report(new SyncProgressEntry(_timeProvider.GetLocalNow(), "Processing reports...", SyncProgressSeverity.Info));

        var processResult = await _processReportsHandler.HandleAsync(new ProcessReportsCommand(Progress: progress), ct);

        if (processResult.IsSuccess)
        {
            filingsCreated = processResult.Value.FilingsCreated;
            reportsProcessed = processResult.Value.ReportsProcessed;
            foreach (var e in processResult.Value.EventErrors)
            {
                var eventMsg = $"{e.EntityName} {e.IncomeDate:yyyy-MM-dd}: {e.ErrorCode} - {e.Message}";
                errors.Add(eventMsg);
                progress.Report(new SyncProgressEntry(_timeProvider.GetLocalNow(), eventMsg, SyncProgressSeverity.Warning));
            }

            // Failed attempts persisted nothing; they must reach the result (not just the live
            // log) so they stay visible after the sync screen is closed. The live log already
            // received each message from the processing handler.
            foreach (var attempt in processResult.Value.FailedAttempts ?? [])
                errors.Add(attempt.ToLogMessage());

            var statusMsg = BuildProcessingSummary(processResult.Value);
            progress.Report(new SyncProgressEntry(_timeProvider.GetLocalNow(), statusMsg,
                processResult.Value.ReportsPendingRetry > 0 || processResult.Value.ReportsErrored > 0
                    ? SyncProgressSeverity.Warning
                    : SyncProgressSeverity.Info));
        }
        else
        {
            errors.Add(processResult.Error.Message);
            progress.Report(new SyncProgressEntry(_timeProvider.GetLocalNow(), processResult.Error.Message, SyncProgressSeverity.Error));
        }

        return Result<SyncAllResult, Error>.Success(
            new SyncAllResult(mailboxesSynced, attachmentsDownloaded, reportsProcessed, filingsCreated, errors.AsReadOnly()));
    }

    /// <summary>One-line summary that never reads as a clean run when reports failed or were deferred.</summary>
    private static string BuildProcessingSummary(ProcessReportsResult result)
    {
        var parts = new List<string>();
        if (result.ReportsProcessed > 0)
            parts.Add($"Processed {result.ReportsProcessed} report(s), created {result.FilingsCreated} filing(s).");
        if (result.ReportsPartialError > 0)
            parts.Add($"{result.ReportsPartialError} report(s) partially processed.");
        if (result.ReportsErrored > 0)
            parts.Add($"{result.ReportsErrored} report(s) failed and need attention.");
        if (result.ReportsPendingRetry > 0)
            parts.Add($"{result.ReportsPendingRetry} report(s) could not be processed and will be retried on the next sync.");

        return parts.Count == 0 ? "No new reports to process." : string.Join(" ", parts);
    }
}
