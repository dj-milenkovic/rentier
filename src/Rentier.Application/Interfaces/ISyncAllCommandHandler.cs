using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.DTOs;

namespace Rentier.Application.Interfaces;

public interface ISyncAllCommandHandler
{
    Task<Result<SyncAllResult, Error>> HandleAsync(
        SyncAllCommand command,
        IProgress<SyncProgressEntry> progress,
        CancellationToken ct = default);
}
