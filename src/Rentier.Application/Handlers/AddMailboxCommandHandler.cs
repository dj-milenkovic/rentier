using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.Interfaces;
using Rentier.Application.Repositories;
using Rentier.Domain.Exceptions;

namespace Rentier.Application.Handlers;

public sealed class AddMailboxCommandHandler
    : ICommandHandler<AddMailboxCommand, Result<Guid, Error>>
{
    private readonly IMailboxRepository _repository;
    private readonly ICredentialStore _credentials;

    public AddMailboxCommandHandler(IMailboxRepository repository, ICredentialStore credentials)
    {
        _repository = repository;
        _credentials = credentials;
    }

    public async Task<Result<Guid, Error>> HandleAsync(
        AddMailboxCommand command, CancellationToken ct = default)
    {
        Domain.Entities.Mailbox mailbox;
        try
        {
            mailbox = Domain.Entities.Mailbox.Create(
                command.Host, command.Port, command.Username, command.InitialSyncDate);
        }
        catch (DomainException ex)
        {
            return Result<Guid, Error>.Failure(new Error("DOMAIN_VALIDATION", ex.Message));
        }

        if (!string.IsNullOrEmpty(command.Password))
        {
            var credResult = await _credentials.SaveCredentialAsync(
                CredentialKeys.MailboxPassword(mailbox.Id), command.Password, ct);
            if (!credResult.IsSuccess)
                return Result<Guid, Error>.Failure(credResult.Error);
        }

        await _repository.AddAsync(mailbox, ct);
        return Result<Guid, Error>.Success(mailbox.Id);
    }
}
