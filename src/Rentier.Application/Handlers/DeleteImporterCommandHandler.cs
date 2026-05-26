using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.Interfaces;
using Rentier.Application.Repositories;

namespace Rentier.Application.Handlers;

public sealed class DeleteImporterCommandHandler
    : ICommandHandler<DeleteImporterCommand, Result<VoidResult, Error>>
{
    private readonly IImporterRepository _repository;

    public DeleteImporterCommandHandler(IImporterRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<VoidResult, Error>> HandleAsync(
        DeleteImporterCommand command, CancellationToken ct = default)
    {
        await _repository.DeleteAsync(command.Id, ct);
        return Result<VoidResult, Error>.Success(VoidResult.Value);
    }
}
