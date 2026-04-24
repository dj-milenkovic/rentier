using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.Interfaces;
using Rentier.Application.Repositories;
using Rentier.Domain.Exceptions;

namespace Rentier.Application.Handlers;

/// <summary>Advances a filing status through the valid state machine transitions.</summary>
public sealed class UpdateFilingStatusCommandHandler
    : ICommandHandler<UpdateFilingStatusCommand, Result<VoidResult, Error>>
{
    private readonly IFilingRepository _filings;

    public UpdateFilingStatusCommandHandler(IFilingRepository filings) => _filings = filings;

    public async Task<Result<VoidResult, Error>> HandleAsync(
        UpdateFilingStatusCommand command, CancellationToken ct = default)
    {
        var filing = await _filings.GetByIdAsync(command.FilingId, ct);
        if (filing is null)
            return Result<VoidResult, Error>.Failure(
                Error.NotFound($"Filing {command.FilingId} not found."));

        try
        {
            filing.AdvanceStatus(command.NewStatus);
        }
        catch (DomainException ex)
        {
            return Result<VoidResult, Error>.Failure(Error.Domain(ex.Message));
        }

        await _filings.UpdateAsync(filing, ct);
        return Result<VoidResult, Error>.Success(VoidResult.Value);
    }
}
