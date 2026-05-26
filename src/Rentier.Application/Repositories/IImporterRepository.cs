using Rentier.Domain.Entities;

namespace Rentier.Application.Repositories;

public interface IImporterRepository
{
    Task<Importer?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Importer>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(Importer importer, CancellationToken ct = default);
    Task UpdateAsync(Importer importer, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
