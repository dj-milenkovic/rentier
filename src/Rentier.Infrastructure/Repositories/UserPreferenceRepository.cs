using Rentier.Application.Repositories;
using Rentier.Domain.Entities;
using Rentier.Infrastructure.Persistence;

namespace Rentier.Infrastructure.Repositories;

public sealed class UserPreferenceRepository : IUserPreferenceRepository
{
    private readonly AppDbContext _db;

    public UserPreferenceRepository(AppDbContext db) => _db = db;

    public async Task<UserPreference?> GetAsync(string key, CancellationToken ct = default)
        => await _db.UserPreferences.FindAsync([key], ct);

    public async Task SaveAsync(UserPreference preference, CancellationToken ct = default)
    {
        var existing = await _db.UserPreferences.FindAsync([preference.Key], ct);
        if (existing is null)
            _db.UserPreferences.Add(preference);
        else
            _db.UserPreferences.Update(preference);
        await _db.SaveChangesAsync(ct);
    }
}
