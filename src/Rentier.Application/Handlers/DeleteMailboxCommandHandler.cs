using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.Interfaces;
using Rentier.Application.Repositories;

namespace Rentier.Application.Handlers;

public sealed class DeleteMailboxCommandHandler
    : ICommandHandler<DeleteMailboxCommand, Result<VoidResult, Error>>
{
    private readonly IMailboxRepository _repository;
    private readonly ICredentialStore _credentials;

    public DeleteMailboxCommandHandler(IMailboxRepository repository, ICredentialStore credentials)
    {
        _repository = repository;
        _credentials = credentials;
    }

    public async Task<Result<VoidResult, Error>> HandleAsync(
        DeleteMailboxCommand command, CancellationToken ct = default)
    {
        await _credentials.DeleteCredentialAsync(CredentialKeys.MailboxPassword(command.Id), ct);

        await _repository.DeleteAsync(command.Id, ct);
        return Result<VoidResult, Error>.Success(VoidResult.Value);
    }
}
