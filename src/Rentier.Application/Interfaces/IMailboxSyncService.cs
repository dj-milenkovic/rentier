using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Domain.Entities;
using Rentier.Domain.ValueObjects;

namespace Rentier.Application.Interfaces;

public interface IMailboxSyncService
{
    Task<Result<SyncResult, Error>> SyncAsync(
        Mailbox mailbox,
        IReadOnlyList<Importer> importers,
        SyncParameters parameters,
        IProgress<SyncProgress>? progress,
        CancellationToken ct);
}
