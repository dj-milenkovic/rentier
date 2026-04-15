using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.Interfaces;
using Rentier.Application.Repositories;

namespace Rentier.Application.Handlers;

/// <summary>
/// Bulk-deletes multiple reports and all their linked filings.
/// Filings are deleted first per report (cascade), then reports are deleted.
/// Empty list is rejected as a domain error.
/// </summary>
public sealed class BulkDeleteReportsCommandHandler
    : ICommandHandler<BulkDeleteReportsCommand, Result<VoidResult, Error>>
{
    private readonly IReportRepository _reportRepository;
    private readonly IFilingRepository _filingRepository;

    public BulkDeleteReportsCommandHandler(
        IReportRepository reportRepository,
        IFilingRepository filingRepository)
    {
        _reportRepository = reportRepository;
        _filingRepository = filingRepository;
    }

    public async Task<Result<VoidResult, Error>> HandleAsync(
        BulkDeleteReportsCommand command, CancellationToken ct = default)
    {
        if (command.ReportIds is null || command.ReportIds.Count == 0)
            return Result<VoidResult, Error>.Failure(
                new Error("BULK_DELETE_REPORTS_INVALID", "ReportIds must be non-null and non-empty."));

        try
        {
            // Delete linked filings first (cascade) to prevent FK violations
            foreach (var reportId in command.ReportIds)
                await _filingRepository.DeleteByReportIdAsync(reportId, ct);

            // Delete the reports
            await _reportRepository.DeleteManyAsync(command.ReportIds, ct);

            return Result<VoidResult, Error>.Success(VoidResult.Value);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Result<VoidResult, Error>.Failure(
                new Error("BULK_DELETE_REPORTS_FAILED", ex.Message));
        }
    }
}
