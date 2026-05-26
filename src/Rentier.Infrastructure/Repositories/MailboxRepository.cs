using Microsoft.EntityFrameworkCore;
using Rentier.Application.Repositories;
using Rentier.Domain.Entities;
using Rentier.Infrastructure.Persistence;

namespace Rentier.Infrastructure.Repositories;

public sealed class MailboxRepository : IMailboxRepository
{
    private readonly AppDbContext _db;

    public MailboxRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<Mailbox?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.Mailboxes.FindAsync([id], ct);
    }

    public async Task<IReadOnlyList<Mailbox>> GetAllAsync(CancellationToken ct = default)
    {
        var list = await _db.Mailboxes.AsNoTracking().ToListAsync(ct);
        return list.AsReadOnly();
    }

    public async Task AddAsync(Mailbox mailbox, CancellationToken ct = default)
    {
        _db.Mailboxes.Add(mailbox);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Mailbox mailbox, CancellationToken ct = default)
    {
        var stale = _db.ChangeTracker.Entries<Mailbox>()
            .FirstOrDefault(e => e.Entity.Id == mailbox.Id);
        if (stale != null)
            stale.State = EntityState.Detached;

        _db.Mailboxes.Update(mailbox);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _db.Mailboxes.FindAsync([id], ct);
        if (entity is not null)
        {
            _db.Mailboxes.Remove(entity);
            await _db.SaveChangesAsync(ct);
        }
    }
}
