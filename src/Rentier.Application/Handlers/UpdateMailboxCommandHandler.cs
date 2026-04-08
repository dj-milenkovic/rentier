using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.Interfaces;
using Rentier.Application.Repositories;
using Rentier.Domain.Exceptions;

namespace Rentier.Application.Handlers;

public sealed class UpdateMailboxCommandHandler
    : ICommandHandler<UpdateMailboxCommand, Result<VoidResult, Error>>
{
    private readonly IMailboxRepository _repository;
    private readonly ICredentialStore _credentials;

    public UpdateMailboxCommandHandler(IMailboxRepository repository, ICredentialStore credentials)
    {
        _repository = repository;
        _credentials = credentials;
    }

    public async Task<Result<VoidResult, Error>> HandleAsync(
        UpdateMailboxCommand command, CancellationToken ct = default)
    {
        var mailbox = await _repository.GetByIdAsync(command.Id, ct);
        if (mailbox is null)
            return Result<VoidResult, Error>.Failure(
                new Error("NOT_FOUND", $"Mailbox {command.Id} not found"));

        try
        {
            mailbox.UpdateDetails(command.Host, command.Port, command.Username);
        }
        catch (DomainException ex)
        {
            return Result<VoidResult, Error>.Failure(new Error("DOMAIN_VALIDATION", ex.Message));
        }

        await _repository.UpdateAsync(mailbox, ct);

        if (!string.IsNullOrEmpty(command.Password))
        {
            var credResult = await _credentials.SaveCredentialAsync(
                CredentialKeys.MailboxPassword(mailbox.Id), command.Password, ct);
            if (!credResult.IsSuccess)
                return Result<VoidResult, Error>.Failure(credResult.Error);
        }

        return Result<VoidResult, Error>.Success(VoidResult.Value);
    }
}
