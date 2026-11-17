using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Application.Interfaces;
using Rentier.Application.Repositories;
using Rentier.Domain.Entities;

namespace Rentier.Application.Handlers;

public sealed class ExportFilingCommandHandler
    : ICommandHandler<ExportFilingCommand, Result<ExportFilingResult, Error>>
{
    private readonly IFilingRepository _filings;
    private readonly ITaxpayerProfileRepository _profiles;
    private readonly IReportRepository _reports;
    private readonly IImporterRepository _importers;
    private readonly IXmlFilingSerializer _serializer;

    public ExportFilingCommandHandler(
        IFilingRepository filings,
        ITaxpayerProfileRepository profiles,
        IReportRepository reports,
        IImporterRepository importers,
        IXmlFilingSerializer serializer)
    {
        _filings = filings;
        _profiles = profiles;
        _reports = reports;
        _importers = importers;
        _serializer = serializer;
    }

    public async Task<Result<ExportFilingResult, Error>> HandleAsync(
        ExportFilingCommand command, CancellationToken ct = default)
    {
        var filing = await _filings.GetByIdAsync(command.FilingId, ct);
        if (filing is null)
            return Result<ExportFilingResult, Error>.Failure(
                Error.NotFound($"Filing {command.FilingId} not found."));

        var profile = await _profiles.GetAsync(ct);
        if (profile is null)
            return Result<ExportFilingResult, Error>.Failure(
                Error.Domain("Taxpayer profile is required before exporting."));

        var paymentNotes = await ResolvePaymentNotesAsync(filing, ct);

        var serializeResult = _serializer.Serialize(filing, profile, paymentNotes);
        if (!serializeResult.IsSuccess)
            return Result<ExportFilingResult, Error>.Failure(serializeResult.Error);

        var suggestedFileName = BuildFileName(filing);

        return Result<ExportFilingResult, Error>.Success(
            new ExportFilingResult(serializeResult.Value, suggestedFileName));
    }

    /// <summary>
    /// Resolves the text for PP-OPO field 3.3 ("Ostalo"): the importer's notes for
    /// report-backed filings, the filing's own notes for manual ones. NacinIsplate is
    /// always 3 ("other"), so 3.3 must never be empty — blank notes fall back to
    /// <see cref="PpOpoDefaults.DEFAULT_PAYMENT_NOTES"/>.
    /// </summary>
    private async Task<string> ResolvePaymentNotesAsync(Filing filing, CancellationToken ct)
    {
        string? notes;
        if (filing.ReportId.HasValue)
        {
            var report = await _reports.GetByIdAsync(filing.ReportId.Value, ct);
            var importer = report is null ? null : await _importers.GetByIdAsync(report.ImporterId, ct);
            notes = importer?.PaymentNotes;
        }
        else
        {
            notes = filing.PaymentNotes;
        }

        return string.IsNullOrWhiteSpace(notes) ? PpOpoDefaults.DEFAULT_PAYMENT_NOTES : notes;
    }

    /// <summary>
    /// Builds the suggested export filename.
    /// Priority: Ticker → PayingEntity → "filing"
    /// Pattern: {yyyy}-{MM}-{identifier}.xml
    /// </summary>
    private static string BuildFileName(Filing filing)
    {
        var raw = FirstNonBlank(filing.Ticker, filing.PayingEntity);
        var identifier = Sanitize(raw);
        return $"{filing.IncomeDate:yyyy-MM}-{identifier}.xml";
    }

    /// <summary>Returns the first value that is not null/whitespace, or null if none qualify.</summary>
    private static string? FirstNonBlank(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static readonly char[] UnsafeChars = ['\\', '/', ':', '*', '?', '"', '<', '>', '|', ' '];

    private static string Sanitize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "filing";

        var result = raw.Trim();
        foreach (var ch in UnsafeChars)
            result = result.Replace(ch, '_');

        return string.IsNullOrEmpty(result) ? "filing" : result;
    }
}
