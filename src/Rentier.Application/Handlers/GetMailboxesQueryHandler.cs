using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Application.Interfaces;
using Rentier.Application.Queries;
using Rentier.Application.Repositories;

namespace Rentier.Application.Handlers;

public sealed class GetMailboxesQueryHandler
    : IQueryHandler<GetMailboxesQuery, Result<IReadOnlyList<MailboxDto>, Error>>
{
    private readonly IMailboxRepository _repository;

    public GetMailboxesQueryHandler(IMailboxRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<IReadOnlyList<MailboxDto>, Error>> HandleAsync(
        GetMailboxesQuery query, CancellationToken ct = default)
    {
        var mailboxes = await _repository.GetAllAsync(ct);
        var list = mailboxes.Select(m => new MailboxDto(
            m.Id,
            m.Host,
            m.Port,
            m.Username,
            m.Cursor.LastSyncDate,
            m.Cursor.LastUid)).ToList();
        return Result<IReadOnlyList<MailboxDto>, Error>.Success(list.AsReadOnly());
    }
}
