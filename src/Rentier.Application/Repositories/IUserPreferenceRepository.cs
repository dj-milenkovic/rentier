using Rentier.Domain.Entities;

namespace Rentier.Application.Repositories;

public interface IUserPreferenceRepository
{
    Task<UserPreference?> GetAsync(string key, CancellationToken ct = default);
    Task SaveAsync(UserPreference preference, CancellationToken ct = default);
}
