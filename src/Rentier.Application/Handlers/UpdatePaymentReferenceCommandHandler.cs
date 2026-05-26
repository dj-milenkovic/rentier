using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.Interfaces;
using Rentier.Application.Repositories;
using Rentier.Domain.Exceptions;

namespace Rentier.Application.Handlers;

/// <summary>Sets or clears the payment reference on a filing.</summary>
public sealed class UpdatePaymentReferenceCommandHandler
    : ICommandHandler<UpdatePaymentReferenceCommand, Result<VoidResult, Error>>
{
    private readonly IFilingRepository _filings;

    public UpdatePaymentReferenceCommandHandler(IFilingRepository filings) => _filings = filings;

    public async Task<Result<VoidResult, Error>> HandleAsync(
        UpdatePaymentReferenceCommand command, CancellationToken ct = default)
    {
        var filing = await _filings.GetByIdAsync(command.FilingId, ct);
        if (filing is null)
            return Result<VoidResult, Error>.Failure(
                Error.NotFound($"Filing {command.FilingId} not found."));

        try
        {
            filing.SetPaymentReference(command.PaymentReference);
        }
        catch (DomainException ex)
        {
            return Result<VoidResult, Error>.Failure(Error.Domain(ex.Message));
        }

        await _filings.UpdateAsync(filing, ct);
        return Result<VoidResult, Error>.Success(VoidResult.Value);
    }
}
