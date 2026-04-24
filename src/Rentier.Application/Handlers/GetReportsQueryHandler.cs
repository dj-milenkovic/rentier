using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Application.Interfaces;
using Rentier.Application.Queries;
using Rentier.Application.Repositories;

namespace Rentier.Application.Handlers;

/// <summary>Returns a paged list of reports as display rows with resolved importer name and filing count.</summary>
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
                var reports = await _reports.GetAllAsync(query.SortDescending, ct);
                var importerList = await _importers.GetAllAsync(ct);
                var importerNames = importerList.ToDictionary(i => i.Id, i => i.DisplayName);

                var dtos = new List<ReportRowDto>(reports.Count);
                foreach (var r in reports)
                {
                    ct.ThrowIfCancellationRequested();
                    var count        = await _filings.GetFilingCountByReportIdAsync(r.Id, ct);
                    var earliest     = await _filings.GetEarliestIncomeDateByReportIdAsync(r.Id, ct);
                    var importerName = importerNames.GetValueOrDefault(r.ImporterId, "Unknown");
                    var datePart     = (r.EmailDate ?? earliest ?? r.ImportDate).ToString("yyyy-MM-dd");
                    var displayName  = $"{importerName} \u2013 {datePart}";
                    dtos.Add(new ReportRowDto(r.Id, r.ReportName, r.ImportDate, r.EmailDate, importerName, r.Status, count, displayName, earliest));
                }

                var totalCount = dtos.Count;
                var totalPages = Math.Max(1, (int)Math.Ceiling((double)totalCount / query.PageSize));
                var slicedRows = (IReadOnlyList<ReportRowDto>)dtos
                    .Skip((query.Page - 1) * query.PageSize)
                    .Take(query.PageSize)
                    .ToList()
                    .AsReadOnly();

                return Result<ReportsPageResult, Error>.Success(
                    new ReportsPageResult(slicedRows, totalCount, totalPages));
            },
            ErrorCodes.REPORT_QUERY_FAILED);
}
