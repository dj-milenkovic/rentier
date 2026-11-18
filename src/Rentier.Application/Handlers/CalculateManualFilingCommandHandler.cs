using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Application.Filings;
using Rentier.Application.Interfaces;
using Rentier.Application.Repositories;

namespace Rentier.Application.Handlers;

/// <summary>
/// Previews a manual entry: the tax, deadline and exchange rate <see cref="IncomeEventFiler"/>
/// would file. Nothing is persisted.
/// </summary>
public sealed class CalculateManualFilingCommandHandler
    : ICommandHandler<CalculateManualFilingCommand, Result<ManualFilingPreviewDto, Error>>
{
    private readonly IncomeEventFiler _filer;
    private readonly IHolidayRepository _holidayRepository;

    public CalculateManualFilingCommandHandler(
        IExchangeRateResolver exchangeRateResolver,
        IHolidayRepository holidayRepository)
    {
        _filer = new IncomeEventFiler(exchangeRateResolver);
        _holidayRepository = holidayRepository;
    }

    public async Task<Result<ManualFilingPreviewDto, Error>> HandleAsync(
        CalculateManualFilingCommand command, CancellationToken ct = default)
    {
        var incomeEvent = IncomeEvent.FromManualEntry(
            command.IncomeType, command.Ticker, command.IncomeDate,
            command.Currency, command.GrossAmount, command.NetReceived);
        if (!incomeEvent.IsSuccess)
            return Result<ManualFilingPreviewDto, Error>.Failure(incomeEvent.Error);

        var holidays = await _holidayRepository.GetHolidayCalendarAsync(ct);
        var assessment = await _filer.AssessAsync(
            incomeEvent.Value, new FilingContext(command.TaxpayerProfileId, holidays), ct);
        if (!assessment.IsSuccess)
            return Result<ManualFilingPreviewDto, Error>.Failure(assessment.Error);

        var (tax, rate, deadline) = assessment.Value;
        return Result<ManualFilingPreviewDto, Error>.Success(new ManualFilingPreviewDto(
            tax.GrossIncomeRsd,
            tax.WhtPaidRsd,
            tax.GrossTaxPayableRsd,
            tax.TaxPayableRsd,
            deadline,
            rate.Rate.RateToRsd,
            rate.SourceDate,
            rate.SourceType));
    }
}
