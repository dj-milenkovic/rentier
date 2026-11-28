using Microsoft.EntityFrameworkCore;
using Rentier.Application.Repositories;
using Rentier.Domain.Entities;
using Rentier.Infrastructure.Persistence;

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
        // A tracking query returns an instance cached by an earlier call without refreshing its
        // values, and the context outlives one operation, so start from the current state (#119).
        _db.ChangeTracker.Clear();
        return await _db.TaxpayerProfiles.FirstOrDefaultAsync(ct);
    }

    public async Task SaveAsync(TaxpayerProfile profile, CancellationToken ct = default)
    {
        // Forget any copy cached by an earlier call so Update on the incoming instance
        // doesn't trigger a tracking conflict.
        _db.ChangeTracker.Clear();

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
