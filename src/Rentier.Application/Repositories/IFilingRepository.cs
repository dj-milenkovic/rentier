using Rentier.Application.Enums;
using Rentier.Application.Queries;
using Rentier.Domain.Entities;

namespace Rentier.Application.Repositories;

public interface IFilingRepository
{
    Task<Filing?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Filing>> GetAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Returns all filings for the same income event identified by (taxpayer, paying entity, income date),
    /// regardless of amount; the paying entity is compared case-insensitively (ASCII). Used by
    /// <see cref="Rentier.Application.Filings.IncomeEventFiler"/> to distinguish an exact duplicate
    /// (same gross — skip) from a correction (different gross — flag for manual review).
    /// When obtained from an <see cref="Rentier.Application.Interfaces.IUnitOfWork"/>, the result also reflects changes
    /// staged in that unit of work (staged inserts included, staged deletes excluded), so
    /// duplicates within one operation are detected before it commits.
    /// </summary>
    Task<IReadOnlyList<Filing>> GetByIncomeEventAsync(Guid taxpayerProfileId, string payingEntity, DateOnly incomeDate, CancellationToken ct = default);

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
        FilingColumnFilter? columnFilter = null,
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
    /// Deletes all Filing records whose ReportId is in <paramref name="reportIds"/>, loading
    /// them with a single query. Used by BulkDeleteReportsCommandHandler BEFORE deleting the
    /// parent Reports. An empty list is a no-op. Same load-then-remove rule as
    /// <see cref="DeleteByReportIdAsync"/> — ExecuteDeleteAsync is prohibited.
    /// </summary>
    Task DeleteByReportIdsAsync(
        IReadOnlyList<Guid> reportIds,
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
    /// Returns filing count and earliest IncomeDate per Report ID in a single GROUP BY query.
    /// Used by GetReportsQueryHandler to avoid N+1 queries when building the reports page.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, (int Count, DateOnly? EarliestDate)>> GetAggregatesByReportIdsAsync(
        IReadOnlyList<Guid> reportIds, CancellationToken ct = default);
}
