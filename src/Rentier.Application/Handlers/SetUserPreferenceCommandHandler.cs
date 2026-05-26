using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.Interfaces;
using Rentier.Application.Repositories;
using Rentier.Domain.Entities;
using Rentier.Domain.Exceptions;

namespace Rentier.Application.Handlers;

public sealed class SetUserPreferenceCommandHandler
    : ICommandHandler<SetUserPreferenceCommand, Result<VoidResult, Error>>
{
    private readonly IUserPreferenceRepository _repository;

    public SetUserPreferenceCommandHandler(IUserPreferenceRepository repository)
    {
        _repository = repository;
    }

    public Task<Result<VoidResult, Error>> HandleAsync(
        SetUserPreferenceCommand command, CancellationToken ct = default) =>
        HandlerHelper.ExecuteAsync<VoidResult>(
            async () =>
            {
                var existing = await _repository.GetAsync(command.Key, ct);
                if (existing is not null)
                {
                    existing.UpdateValue(command.Value);
                    await _repository.SaveAsync(existing, ct);
                }
                else
                {
                    var preference = new UserPreference(command.Key, command.Value);
                    await _repository.SaveAsync(preference, ct);
                }
                return Result<VoidResult, Error>.Success(VoidResult.Value);
            },
            ErrorCodes.INFRASTRUCTURE_ERROR);
}
