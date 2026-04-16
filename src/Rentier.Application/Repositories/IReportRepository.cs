using Rentier.Domain.Entities;
using Rentier.Domain.Enums;

namespace Rentier.Application.Repositories;

public interface IReportRepository
{
    Task<Report?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Report>> GetAllAsync(bool sortDescending = true, CancellationToken ct = default);
    Task<IReadOnlyList<Report>> GetByImporterAsync(Guid importerId, CancellationToken ct = default);
    Task<IReadOnlyList<Report>> GetByStatusAsync(ReportStatus status, CancellationToken ct = default);
    Task<bool> ExistsByImporterAndNameAsync(Guid importerId, string reportName, CancellationToken ct = default);
    Task AddAsync(Report report, CancellationToken ct = default);
    Task UpdateAsync(Report report, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Deletes all Report records whose IDs are in the provided list.
    /// An empty list is a no-op.
    /// </summary>
    Task DeleteManyAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default);
}
