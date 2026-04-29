using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Application.Interfaces;
using Rentier.Application.Queries;
using Rentier.Application.Repositories;

namespace Rentier.Application.Handlers;

public sealed class GetReportsQueryHandler
    : IQueryHandler<GetReportsQuery, Result<ReportsPageResult, Error>>
{
    private readonly IReportRepository _reports;
    private readonly IImporterRepository _importers;
    private readonly IFilingRepository _filings;

    public GetReportsQueryHandler(
        IReportRepository reports,
        IImporterRepository importers,
        IFilingRepository filings)
    {
        _reports = reports;
        _importers = importers;
        _filings = filings;
    }

    public Task<Result<ReportsPageResult, Error>> HandleAsync(
        GetReportsQuery query, CancellationToken ct = default) =>
        HandlerHelper.ExecuteWithValidationAsync<ReportsPageResult>(
            () => PaginationValidator.Validate<ReportsPageResult>(query),
            async () =>
            {
                // Always load importers (needed for name resolution and optionally for filtering)
                var importerList = await _importers.GetAllAsync(ct);
                var importerNames = importerList.ToDictionary(i => i.Id, i => i.DisplayName);

                // Pre-resolve importer IDs if ImporterContains is set
                IReadOnlyList<Guid>? importerIds = null;
                if (!string.IsNullOrWhiteSpace(query.Filter?.ImporterContains))
                {
                    var term = query.Filter.ImporterContains.Trim();
                    importerIds = importerList
                        .Where(i => i.DisplayName.Contains(term, StringComparison.OrdinalIgnoreCase))
                        .Select(i => i.Id)
                        .ToList()
                        .AsReadOnly();
                }

                // Build effective filter with resolved importer IDs
                var effectiveFilter = query.Filter is null && importerIds is null
                    ? null
                    : (query.Filter ?? new ReportColumnFilter()) with { ImporterIds = importerIds };

                var skip = (query.Page - 1) * query.PageSize;
                var (pageReports, totalCount) = await _reports.GetPagedAsync(
                    effectiveFilter, skip, query.PageSize, query.SortDescending, ct);

                var dtos = new List<ReportRowDto>(pageReports.Count);
                foreach (var r in pageReports)
                {
                    ct.ThrowIfCancellationRequested();
                    var count        = await _filings.GetFilingCountByReportIdAsync(r.Id, ct);
                    var earliest     = await _filings.GetEarliestIncomeDateByReportIdAsync(r.Id, ct);
                    var importerName = importerNames.GetValueOrDefault(r.ImporterId, "Unknown");
                    var datePart     = (r.EmailDate ?? earliest ?? r.ImportDate).ToString("yyyy-MM-dd");
                    var displayName  = $"{importerName} \u2013 {datePart}";
                    dtos.Add(new ReportRowDto(r.Id, r.ReportName, r.ImportDate, r.EmailDate, importerName, r.Status, count, displayName, earliest));
                }

                // Post-filter by filing count (computed, not in DB)
                if (query.Filter?.FilingCountValue.HasValue == true)
                {
                    var fcVal = query.Filter.FilingCountValue.Value;
                    dtos = dtos.Where(d => d.FilingCount == fcVal).ToList();
                }

                var totalPages = Math.Max(1, (int)Math.Ceiling((double)totalCount / query.PageSize));
                return Result<ReportsPageResult, Error>.Success(
                    new ReportsPageResult(dtos.AsReadOnly(), totalCount, totalPages));
            },
            ErrorCodes.REPORT_QUERY_FAILED);
}
