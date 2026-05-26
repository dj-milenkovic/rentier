using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.Interfaces;
using Rentier.Application.Repositories;

namespace Rentier.Application.Handlers;

/// <summary>Deletes a filing by ID. No-op if the filing does not exist.</summary>
public sealed class DeleteFilingCommandHandler
    : ICommandHandler<DeleteFilingCommand, Result<VoidResult, Error>>
{
    private readonly IFilingRepository _filings;

    public DeleteFilingCommandHandler(IFilingRepository filings) => _filings = filings;

    public async Task<Result<VoidResult, Error>> HandleAsync(
        DeleteFilingCommand command, CancellationToken ct = default)
    {
        await _filings.DeleteAsync(command.FilingId, ct);
        return Result<VoidResult, Error>.Success(VoidResult.Value);
    }
}
