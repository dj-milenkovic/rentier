using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Application.Interfaces;
using Rentier.Application.Repositories;

namespace Rentier.Application.Handlers;

public sealed class SyncMailboxCommandHandler
    : ICommandHandler<SyncMailboxCommand, Result<SyncResult, Error>>
{
    private readonly IImporterRepository _importerRepository;
    private readonly IMailboxRepository _mailboxRepository;
    private readonly IMailboxSyncService _syncService;

    public SyncMailboxCommandHandler(
        IImporterRepository importerRepository,
        IMailboxRepository mailboxRepository,
        IMailboxSyncService syncService)
    {
        _importerRepository = importerRepository;
        _mailboxRepository = mailboxRepository;
        _syncService = syncService;
    }

    public async Task<Result<SyncResult, Error>> HandleAsync(
        SyncMailboxCommand command, CancellationToken ct = default)
    {
        var allImporters = await _importerRepository.GetAllAsync(ct);

        // Group importers by mailbox — skip importers with no mailbox assigned
        var byMailbox = allImporters
            .Where(i => i.MailboxId != null)
            .GroupBy(i => i.MailboxId!.Value)
            .ToList();

        var totalCreated = 0;
        var errors = new List<string>();

        foreach (var group in byMailbox)
        {
            ct.ThrowIfCancellationRequested();

            var mailbox = await _mailboxRepository.GetByIdAsync(group.Key, ct);
            if (mailbox is null)
            {
                errors.Add($"Mailbox {group.Key} not found");
                continue;
            }

            var result = await _syncService.SyncAsync(
                mailbox,
                group.ToList().AsReadOnly(),
                command.Parameters,
                progress: null,
                ct);

            if (result.IsSuccess)
            {
                totalCreated += result.Value.ReportsCreated;
                errors.AddRange(result.Value.Errors);
            }
            else
            {
                errors.Add(result.Error.Message);
            }
        }

        return Result<SyncResult, Error>.Success(new SyncResult(totalCreated, errors));
    }
}
