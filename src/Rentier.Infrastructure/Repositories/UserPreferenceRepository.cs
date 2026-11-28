using Microsoft.EntityFrameworkCore;
using Rentier.Application.Repositories;
using Rentier.Domain.Entities;
using Rentier.Infrastructure.Persistence;

namespace Rentier.Infrastructure.Repositories;

public sealed class UserPreferenceRepository : IUserPreferenceRepository
{
    private readonly AppDbContext _db;

    public UserPreferenceRepository(AppDbContext db) => _db = db;

    public async Task<UserPreference?> GetAsync(string key, CancellationToken ct = default)
    {
        // The context outlives one operation: never serve a copy cached by an earlier call (#119).
        _db.StartFromCurrentState();
        return await _db.UserPreferences.FindAsync([key], ct);
    }

    public async Task SaveAsync(UserPreference preference, CancellationToken ct = default)
    {
        // Decide insert vs update from the database, not from a copy cached by an earlier call,
        // which may be stale or a different instance than the one being saved.
        _db.StartFromCurrentState();
        var exists = await _db.UserPreferences.AnyAsync(p => p.Key == preference.Key, ct);
        if (!exists)
            _db.UserPreferences.Add(preference);
        else
            _db.UserPreferences.Update(preference);
        await _db.SaveChangesAsync(ct);
    }
}
