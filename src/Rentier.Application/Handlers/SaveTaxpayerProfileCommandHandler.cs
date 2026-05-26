using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.Interfaces;
using Rentier.Application.Repositories;
using Rentier.Domain.Entities;
using Rentier.Domain.Exceptions;

namespace Rentier.Application.Handlers;

public sealed class SaveTaxpayerProfileCommandHandler
    : ICommandHandler<SaveTaxpayerProfileCommand, Result<VoidResult, Error>>
{
    private readonly ITaxpayerProfileRepository _repository;

    public SaveTaxpayerProfileCommandHandler(ITaxpayerProfileRepository repository)
    {
        _repository = repository;
    }

    public Task<Result<VoidResult, Error>> HandleAsync(
        SaveTaxpayerProfileCommand command, CancellationToken ct = default) =>
        HandlerHelper.ExecuteAsync<VoidResult>(
            async () =>
            {
                var existing = await _repository.GetAsync(ct);
                var id = existing?.Id ?? Guid.NewGuid();
                var profile = new TaxpayerProfile(
                    id,
                    command.Jmbg,
                    command.FullName,
                    command.Address,
                    command.OpstinaCode,
                    command.PhoneNumber,
                    command.Email);

                await _repository.SaveAsync(profile, ct);
                return Result<VoidResult, Error>.Success(VoidResult.Value);
            },
            ErrorCodes.DOMAIN_ERROR);
}
