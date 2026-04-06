using Rentier.Domain.Entities;

namespace Rentier.Application.Repositories;

public interface IReportRepository
{
    Task<Report?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Report>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Report>> GetByImporterAsync(Guid importerId, CancellationToken ct = default);
    Task AddAsync(Report report, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
