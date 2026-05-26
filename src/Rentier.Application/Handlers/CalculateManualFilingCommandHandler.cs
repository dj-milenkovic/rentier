using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Application.Interfaces;
using Rentier.Application.Services;

namespace Rentier.Application.Handlers;

/// <summary>
/// Validates inputs, resolves NBS exchange rate, computes PP-OPO tax, and returns a preview DTO.
/// No data is persisted — this is a pure calculation step.
/// </summary>
public sealed class CalculateManualFilingCommandHandler
    : ICommandHandler<CalculateManualFilingCommand, Result<ManualFilingPreviewDto, Error>>
{
    private readonly ManualFilingCalculator _calculator;

    public CalculateManualFilingCommandHandler(ManualFilingCalculator calculator)
    {
        _calculator = calculator;
    }

    public async Task<Result<ManualFilingPreviewDto, Error>> HandleAsync(
        CalculateManualFilingCommand command, CancellationToken ct = default)
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
            return Result<ManualFilingPreviewDto, Error>.Failure(calcResult.Error);

        var r = calcResult.Value;
        return Result<ManualFilingPreviewDto, Error>.Success(new ManualFilingPreviewDto(
            r.TaxInfo.GrossIncomeRsd,
            r.TaxInfo.WhtPaidRsd,
            r.TaxInfo.GrossTaxPayableRsd,
            r.TaxInfo.TaxPayableRsd,
            r.Deadline,
            r.Rate.Rate.RateToRsd,
            r.Rate.SourceDate,
            r.Rate.SourceType));
    }
}

