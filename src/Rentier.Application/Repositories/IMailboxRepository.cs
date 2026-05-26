using Rentier.Domain.Entities;

namespace Rentier.Application.Repositories;

public interface IMailboxRepository
{
    Task<Mailbox?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Mailbox>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(Mailbox mailbox, CancellationToken ct = default);
    Task UpdateAsync(Mailbox mailbox, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
