using Rentier.Domain.Entities;

namespace Rentier.Application.Repositories;

public interface IFilingRepository
{
    Task<Filing?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Filing>> GetAllAsync(CancellationToken ct = default);
    Task<Filing?> GetByTaxPeriodAsync(Guid taxpayerProfileId, DateOnly taxPeriod, CancellationToken ct = default);
    Task AddAsync(Filing filing, CancellationToken ct = default);
    Task UpdateAsync(Filing filing, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
