using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.Interfaces;
using Rentier.Application.Repositories;
using Rentier.Application.Services;
using Rentier.Domain.Entities;
using Rentier.Domain.ValueObjects;

namespace Rentier.Application.Handlers;

/// <summary>
/// Validates inputs, resolves exchange rate, computes tax, checks for duplicate filings,
/// persists the new Filing, and returns its ID.
/// </summary>
public sealed class CreateManualFilingCommandHandler
    : ICommandHandler<CreateManualFilingCommand, Result<Guid, Error>>
{
    private readonly ManualFilingCalculator _calculator;
    private readonly IFilingRepository _filingRepository;

    public CreateManualFilingCommandHandler(
        ManualFilingCalculator calculator,
        IFilingRepository filingRepository)
    {
        _calculator = calculator;
        _filingRepository = filingRepository;
    }

    public async Task<Result<Guid, Error>> HandleAsync(
        CreateManualFilingCommand command, CancellationToken ct = default)
    {
        var calcResult = await _calculator.CalculateAsync(
            command.IncomeType,
            command.Ticker,
            command.IncomeDate,
            command.Currency,
            command.GrossAmount,
            command.NetReceived,
            ct);

        if (!calcResult.IsSuccess)
            return Result<Guid, Error>.Failure(calcResult.Error);

        var r = calcResult.Value;

        // ── Duplicate check ──────────────────────────────────────────────
        var exists = await _filingRepository.ExistsByIncomeAsync(
            command.TaxpayerProfileId, r.TickerUpper, command.IncomeDate, r.TaxInfo.GrossIncomeRsd, ct);

        if (exists)
            return Result<Guid, Error>.Failure(
                new Error(ErrorCodes.FILING_CREATE_DUPLICATE, "A filing with the same details already exists"));

        // ── Persist filing ───────────────────────────────────────────────
        var filing = Filing.CreateFromIncome(
            r.TaxInfo,
            command.TaxpayerProfileId,
            r.Deadline,
            new FilingProvenance(
                ReportId: null,
                ExchangeRateSourceDate: r.Rate.SourceDate,
                ExchangeRateSourceType: r.Rate.SourceType,
                Ticker: r.TickerUpper,
                PaymentNotes: command.PaymentNotes));

        await _filingRepository.AddAsync(filing, ct);

        return Result<Guid, Error>.Success(filing.Id);
    }
}


