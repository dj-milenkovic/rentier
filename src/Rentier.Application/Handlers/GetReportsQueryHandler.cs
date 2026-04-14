using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Application.Interfaces;
using Rentier.Application.Queries;
using Rentier.Application.Repositories;

namespace Rentier.Application.Handlers;

/// <summary>Returns all reports as display rows with resolved importer name and filing count.</summary>
public sealed class GetReportsQueryHandler
    : IQueryHandler<GetReportsQuery, Result<IReadOnlyList<ReportRowDto>, Error>>
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

    public async Task<Result<IReadOnlyList<ReportRowDto>, Error>> HandleAsync(
        GetReportsQuery query, CancellationToken ct = default)
    {
        try
        {
            var reports = await _reports.GetAllAsync(ct);
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
                dtos.Add(new ReportRowDto(r.Id, r.ReportName, r.ImportDate, importerName, r.Status, count, displayName, earliest));
            }

            return Result<IReadOnlyList<ReportRowDto>, Error>.Success(dtos.AsReadOnly());
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<ReportRowDto>, Error>.Failure(
                new Error("GET_REPORTS_FAILED", ex.Message));
        }
    }
}
