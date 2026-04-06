using Microsoft.EntityFrameworkCore;
using Rentier.Application.Repositories;
using Rentier.Domain.Entities;
using Rentier.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Rentier.Infrastructure.Repositories;

public sealed class TaxpayerProfileRepository : ITaxpayerProfileRepository
{
    private readonly AppDbContext _context;

    public TaxpayerProfileRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<TaxpayerProfile?> GetAsync(CancellationToken ct = default)
    {
        return await _context.TaxpayerProfiles.FirstOrDefaultAsync(ct);
    }

    public async Task SaveAsync(TaxpayerProfile profile, CancellationToken ct = default)
    {
        // Detach any stale tracked instance with the same primary key so that
        // Update on the incoming (new) instance doesn't trigger a tracking conflict.
        EntityEntry<TaxpayerProfile>? tracked = _context.ChangeTracker
            .Entries<TaxpayerProfile>()
            .FirstOrDefault(e => e.Entity.Id == profile.Id);
        if (tracked is not null)
            tracked.State = EntityState.Detached;

        bool exists = await _context.TaxpayerProfiles.AnyAsync(ct);
        if (!exists)
        {
            _context.TaxpayerProfiles.Add(profile);
        }
        else
        {
            _context.TaxpayerProfiles.Update(profile);
        }
        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(CancellationToken ct = default)
    {
        await _context.TaxpayerProfiles.ExecuteDeleteAsync(ct);
    }
}
