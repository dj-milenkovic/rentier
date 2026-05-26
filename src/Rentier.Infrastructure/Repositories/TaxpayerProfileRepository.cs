using Microsoft.EntityFrameworkCore;
using Rentier.Application.Repositories;
using Rentier.Domain.Entities;
using Rentier.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Rentier.Infrastructure.Repositories;

public sealed class TaxpayerProfileRepository : ITaxpayerProfileRepository
{
    private readonly AppDbContext _db;

    public TaxpayerProfileRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<TaxpayerProfile?> GetAsync(CancellationToken ct = default)
    {
        return await _db.TaxpayerProfiles.FirstOrDefaultAsync(ct);
    }

    public async Task SaveAsync(TaxpayerProfile profile, CancellationToken ct = default)
    {
        // Detach any stale tracked instance with the same primary key so that
        // Update on the incoming (new) instance doesn't trigger a tracking conflict.
        EntityEntry<TaxpayerProfile>? tracked = _db.ChangeTracker
            .Entries<TaxpayerProfile>()
            .FirstOrDefault(e => e.Entity.Id == profile.Id);
        if (tracked is not null)
            tracked.State = EntityState.Detached;

        bool exists = await _db.TaxpayerProfiles.AnyAsync(ct);
        if (!exists)
        {
            _db.TaxpayerProfiles.Add(profile);
        }
        else
        {
            _db.TaxpayerProfiles.Update(profile);
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(CancellationToken ct = default)
    {
        await _db.TaxpayerProfiles.ExecuteDeleteAsync(ct);
    }
}
