using Microsoft.Extensions.Logging;
using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.Interfaces;
using Rentier.Application.Repositories;

namespace Rentier.Application.Handlers;

/// <summary>Bulk-deletes multiple filings by ID. Empty list is rejected as a domain error.</summary>
public sealed class BulkDeleteFilingsCommandHandler
    : ICommandHandler<BulkDeleteFilingsCommand, Result<VoidResult, Error>>
{
    private readonly IFilingRepository _filingRepository;
    private readonly ILogger<BulkDeleteFilingsCommandHandler> _logger;

    public BulkDeleteFilingsCommandHandler(IFilingRepository filingRepository, ILogger<BulkDeleteFilingsCommandHandler> logger)
    {
        _filingRepository = filingRepository;
        _logger = logger;
    }

    public Task<Result<VoidResult, Error>> HandleAsync(
        BulkDeleteFilingsCommand command, CancellationToken ct = default)
    {
        if (command.FilingIds is null || command.FilingIds.Count == 0)
            return Task.FromResult(Result<VoidResult, Error>.Failure(
                new Error(ErrorCodes.FILING_BULK_DELETE_INVALID, "FilingIds must be non-null and non-empty.")));

        return HandlerHelper.ExecuteAsync<VoidResult>(
            async () =>
            {
                await _filingRepository.DeleteManyAsync(command.FilingIds, ct);
                return Result<VoidResult, Error>.Success(VoidResult.Value);
            },
            ErrorCodes.FILING_BULK_DELETE_FAILED,
            _logger);
    }
}
