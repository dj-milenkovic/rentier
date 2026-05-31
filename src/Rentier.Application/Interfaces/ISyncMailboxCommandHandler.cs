using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.DTOs;

namespace Rentier.Application.Interfaces;

public interface ISyncMailboxCommandHandler
{
    Task<Result<SyncResult, Error>> HandleAsync(
        SyncMailboxCommand command,
        IProgress<SyncProgressEntry>? progress,
        CancellationToken ct = default);
}
