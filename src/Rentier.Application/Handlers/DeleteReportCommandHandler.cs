using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.Interfaces;
using Rentier.Application.Repositories;

namespace Rentier.Application.Handlers;

/// <summary>
/// Deletes a Report and all linked Filings.
/// Filings are deleted first to avoid FK violations, then the report is deleted.
/// Both operations are idempotent — safe to call when records do not exist.
/// </summary>
public sealed class DeleteReportCommandHandler
    : ICommandHandler<DeleteReportCommand, Result<VoidResult, Error>>
{
    private readonly IReportRepository _reportRepository;
    private readonly IFilingRepository _filingRepository;

    public DeleteReportCommandHandler(
        IReportRepository reportRepository,
        IFilingRepository filingRepository)
    {
        _reportRepository = reportRepository;
        _filingRepository = filingRepository;
    }

    public async Task<Result<VoidResult, Error>> HandleAsync(
        DeleteReportCommand command, CancellationToken ct = default)
    {
        try
        {
            // Step 1: delete linked filings first (prevents FK violations)
            await _filingRepository.DeleteByReportIdAsync(command.ReportId, ct);

            // Step 2: delete the report
            await _reportRepository.DeleteAsync(command.ReportId, ct);

            return Result<VoidResult, Error>.Success(VoidResult.Value);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Result<VoidResult, Error>.Failure(
                new Error("DELETE_REPORT_FAILED", ex.Message));
        }
    }
}
