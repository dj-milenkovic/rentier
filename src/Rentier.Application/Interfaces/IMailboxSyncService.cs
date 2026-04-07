using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Domain.Entities;

namespace Rentier.Application.Interfaces;

public interface IMailboxSyncService
{
    Task<Result<SyncResult, Error>> SyncAsync(
        Mailbox mailbox,
        IReadOnlyList<Importer> importers,
        IProgress<SyncProgress>? progress,
        CancellationToken ct);
}
