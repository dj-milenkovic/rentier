using Microsoft.Extensions.Logging;
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
    private readonly ILogger<DeleteMailboxCommandHandler> _logger;

    public DeleteMailboxCommandHandler(
        IMailboxRepository repository,
        ICredentialStore credentials,
        ILogger<DeleteMailboxCommandHandler> logger)
    {
        _repository = repository;
        _credentials = credentials;
        _logger = logger;
    }

    public async Task<Result<VoidResult, Error>> HandleAsync(
        DeleteMailboxCommand command, CancellationToken ct = default)
    {
        var credResult = await _credentials.DeleteCredentialAsync(
            CredentialKeys.MailboxPassword(command.Id), ct);

        // CREDENTIAL_NOT_FOUND is idempotent — password was never saved, nothing to clean up.
        // Any other failure is unexpected and warrants a warning, but must not block DB deletion.
        if (!credResult.IsSuccess && credResult.Error.Code != "CREDENTIAL_NOT_FOUND")
        {
            _logger.LogWarning(
                "Failed to delete credential for mailbox {MailboxId}: [{Code}] {Message}",
                command.Id, credResult.Error.Code, credResult.Error.Message);
        }

        await _repository.DeleteAsync(command.Id, ct);
        return Result<VoidResult, Error>.Success(VoidResult.Value);
    }
}
