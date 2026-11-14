using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.Interfaces;
using Rentier.Application.Repositories;

namespace Rentier.Application.Handlers;

/// <summary>
/// Bulk-deletes multiple reports and all their linked filings as one atomic operation.
/// Filings are staged for deletion first per report (cascade), then the reports; a single
/// commit removes everything, so a failure leaves all reports and filings untouched.
/// Empty list is rejected as a domain error.
/// </summary>
public sealed class BulkDeleteReportsCommandHandler
    : ICommandHandler<BulkDeleteReportsCommand, Result<VoidResult, Error>>
{
    private readonly IUnitOfWorkFactory _unitOfWorkFactory;

    public BulkDeleteReportsCommandHandler(IUnitOfWorkFactory unitOfWorkFactory)
    {
        _unitOfWorkFactory = unitOfWorkFactory;
    }

    public Task<Result<VoidResult, Error>> HandleAsync(
        BulkDeleteReportsCommand command, CancellationToken ct = default)
    {
        if (command.ReportIds is null || command.ReportIds.Count == 0)
            return Task.FromResult(Result<VoidResult, Error>.Failure(
                new Error(ErrorCodes.REPORT_BULK_DELETE_INVALID, "ReportIds must be non-null and non-empty.")));

        return HandlerHelper.ExecuteAsync<VoidResult>(
            async () =>
            {
                await using var unitOfWork = _unitOfWorkFactory.Create();

                // Delete linked filings first (cascade) to prevent FK violations
                foreach (var reportId in command.ReportIds)
                    await unitOfWork.Filings.DeleteByReportIdAsync(reportId, ct);

                // Delete the reports
                await unitOfWork.Reports.DeleteManyAsync(command.ReportIds, ct);

                await unitOfWork.SaveChangesAsync(ct);

                return Result<VoidResult, Error>.Success(VoidResult.Value);
            },
            ErrorCodes.REPORT_BULK_DELETE_FAILED);
    }
}
