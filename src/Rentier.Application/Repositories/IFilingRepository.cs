using Rentier.Domain.Entities;

namespace Rentier.Application.Repositories;

public interface IFilingRepository
{
    Task<Filing?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Filing>> GetAllAsync(CancellationToken ct = default);
    Task<Filing?> GetByTaxPeriodAsync(Guid taxpayerProfileId, DateOnly taxPeriod, CancellationToken ct = default);
    Task<bool> ExistsByIncomeAsync(Guid taxpayerProfileId, string payingEntity, DateOnly incomeDate, decimal grossIncomeRsd, CancellationToken ct = default);
    Task<IReadOnlyList<Filing>> GetByReportIdAsync(Guid reportId, CancellationToken ct = default);
    Task AddAsync(Filing filing, CancellationToken ct = default);
    Task UpdateAsync(Filing filing, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
