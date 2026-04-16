using Rentier.Application.Enums;
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

    /// <summary>Returns a paged, optionally-filtered list of filings ordered by the specified column.</summary>
    Task<(IReadOnlyList<Filing> Items, int TotalCount)> GetPagedAsync(
        FilingFilterMode filter,
        int skip,
        int take,
        FilingSortColumn sortColumn = FilingSortColumn.FilingDeadline,
        bool sortDescending = true,
        CancellationToken ct = default);

    /// <summary>
    /// Returns the count of Filing records linked to the given Report.
    /// Used by GetReportsQueryHandler to populate ReportRowDto.FilingCount without
    /// loading full Filing entities (count-only EF query).
    /// </summary>
    Task<int> GetFilingCountByReportIdAsync(
        Guid reportId,
        CancellationToken ct = default);

    /// <summary>
    /// Deletes all Filing records whose ReportId matches reportId.
    /// Used by DeleteReportCommandHandler BEFORE deleting the parent Report.
    /// Implementation MUST use load-then-remove pattern — ExecuteDeleteAsync is prohibited.
    /// </summary>
    Task DeleteByReportIdAsync(
        Guid reportId,
        CancellationToken ct = default);

    /// <summary>
    /// Deletes all Filing records whose IDs are in the provided list.
    /// An empty list is a no-op.
    /// </summary>
    Task DeleteManyAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default);

    /// <summary>
    /// Returns filings with Status ∈ {Init, Filed} whose FilingDeadline falls within [today, today+days], ordered by FilingDeadline ascending.
    /// </summary>
    Task<IReadOnlyList<Filing>> GetUpcomingAsync(DateOnly today, int days, CancellationToken ct = default);

    /// <summary>
    /// Returns filings with Status ∈ {Init, Filed} whose FilingDeadline is before today, ordered by FilingDeadline ascending.
    /// </summary>
    Task<IReadOnlyList<Filing>> GetOverdueAsync(DateOnly today, CancellationToken ct = default);

    /// <summary>
    /// Returns aggregate counts by status and the total unpaid tax amount.
    /// </summary>
    Task<(int InitCount, int FiledCount, int PaidCount, decimal TotalUnpaidRsd)> GetFilingStatsAsync(CancellationToken ct = default);

    /// <summary>
    /// Returns the earliest IncomeDate among filings linked to the given Report, or null if no filings exist.
    /// Used by GetReportsQueryHandler to derive a human-readable display name.
    /// </summary>
    Task<DateOnly?> GetEarliestIncomeDateByReportIdAsync(Guid reportId, CancellationToken ct = default);
}
