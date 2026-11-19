using Microsoft.Extensions.Logging;
using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.Interfaces;
using Rentier.Application.Repositories;

namespace Rentier.Application.Handlers;

/// <summary>
/// Deletes a Report and all linked Filings as one atomic operation.
/// Filings are staged for deletion first, then the report; a single commit removes both,
/// so a failure leaves the report and its filings untouched.
/// Both operations are idempotent — safe to call when records do not exist.
/// </summary>
public sealed class DeleteReportCommandHandler
    : ICommandHandler<DeleteReportCommand, Result<VoidResult, Error>>
{
    private readonly IUnitOfWorkFactory _unitOfWorkFactory;
    private readonly ILogger<DeleteReportCommandHandler> _logger;

    public DeleteReportCommandHandler(IUnitOfWorkFactory unitOfWorkFactory, ILogger<DeleteReportCommandHandler> logger)
    {
        _logger = logger;
        _unitOfWorkFactory = unitOfWorkFactory;
    }

    public Task<Result<VoidResult, Error>> HandleAsync(
        DeleteReportCommand command, CancellationToken ct = default) =>
        HandlerHelper.ExecuteAsync<VoidResult>(
            async () =>
            {
                await using var unitOfWork = _unitOfWorkFactory.Create();

                // Step 1: delete linked filings first (prevents FK violations)
                await unitOfWork.Filings.DeleteByReportIdAsync(command.ReportId, ct);

                // Step 2: delete the report
                await unitOfWork.Reports.DeleteAsync(command.ReportId, ct);

                await unitOfWork.SaveChangesAsync(ct);

                return Result<VoidResult, Error>.Success(VoidResult.Value);
            },
            ErrorCodes.REPORT_DELETE_FAILED,
            _logger);
}
