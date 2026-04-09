using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Application.Interfaces;
using Rentier.Application.Repositories;
using Rentier.Domain.Entities;

namespace Rentier.Application.Handlers;

/// <summary>
/// Validates and imports a CSV brokerage statement, persists the report, and triggers processing.
/// CSV is validated before the duplicate check so no record is persisted on an invalid file.
/// </summary>
public sealed class ImportReportCommandHandler
    : ICommandHandler<ImportReportCommand, Result<Guid, Error>>
{
    private readonly IReportRepository _reportRepository;
    private readonly IStatementParser _statementParser;
    private readonly ICommandHandler<ProcessReportsCommand, Result<ProcessReportsResult, Error>> _processReports;

    public ImportReportCommandHandler(
        IReportRepository reportRepository,
        IStatementParser statementParser,
        ICommandHandler<ProcessReportsCommand, Result<ProcessReportsResult, Error>> processReports)
    {
        _reportRepository = reportRepository;
        _statementParser = statementParser;
        _processReports = processReports;
    }

    public async Task<Result<Guid, Error>> HandleAsync(
        ImportReportCommand command, CancellationToken ct = default)
    {
        try
        {
            // Step 1: Validate CSV format before anything is persisted
            using var stream = new MemoryStream(command.CsvContent);
            var parseResult = await _statementParser.ParseAsync(stream, ct);
            if (!parseResult.IsSuccess)
                return Result<Guid, Error>.Failure(
                    new Error("INVALID_CSV", parseResult.Error.Message));

            // Step 2: Duplicate check
            var exists = await _reportRepository.ExistsByImporterAndNameAsync(
                command.ImporterId, command.FileName, ct);
            if (exists)
                return Result<Guid, Error>.Failure(
                    new Error("DUPLICATE_REPORT",
                        $"A report named '{command.FileName}' already exists for this importer."));

            // Step 3: Persist
            var report = Report.Create(command.ImporterId, command.FileName, command.CsvContent, null);
            await _reportRepository.AddAsync(report, ct);

            // Step 4: Run pipeline for all Init reports
            var processResult = await _processReports.HandleAsync(new ProcessReportsCommand(), ct);
            if (!processResult.IsSuccess)
                return Result<Guid, Error>.Failure(processResult.Error);

            return Result<Guid, Error>.Success(report.Id);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Result<Guid, Error>.Failure(new Error("IMPORT_FAILED", ex.Message));
        }
    }
}
