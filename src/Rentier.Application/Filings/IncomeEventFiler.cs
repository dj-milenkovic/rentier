using Rentier.Application.Common;
using Rentier.Application.Interfaces;
using Rentier.Application.Repositories;
using Rentier.Domain.Entities;
using Rentier.Domain.Exceptions;
using Rentier.Domain.Services;
using Rentier.Domain.ValueObjects;

namespace Rentier.Application.Filings;

/// <summary>
/// Turns an income event into a PP-OPO filing — the one place that resolves the exchange rate,
/// computes the tax and deadline, and applies the duplicate/correction rule, for statement
/// imports and manual entries alike.
/// </summary>
public sealed class IncomeEventFiler
{
    private readonly IExchangeRateResolver _exchangeRateResolver;

    public IncomeEventFiler(IExchangeRateResolver exchangeRateResolver)
    {
        _exchangeRateResolver = exchangeRateResolver;
    }

    /// <summary>
    /// Computes what a filing for <paramref name="incomeEvent"/> would contain, without looking at
    /// or changing existing filings (used for previews).
    /// </summary>
    public async Task<Result<IncomeEventAssessment, Error>> AssessAsync(
        IncomeEvent incomeEvent, FilingContext context, CancellationToken ct = default)
    {
        var rate = await _exchangeRateResolver.ResolveAsync(
            incomeEvent.Date, incomeEvent.Currency, context.Holidays,
            brokerRates: context.BrokerRates, ct: ct);
        if (!rate.IsSuccess)
            return Result<IncomeEventAssessment, Error>.Failure(rate.Error);

        try
        {
            var info = TaxCalculationService.Calculate(
                new IncomeTaxInput(incomeEvent.IncomeType, incomeEvent.PayingEntity, incomeEvent.Date,
                    incomeEvent.Amount, incomeEvent.Currency, incomeEvent.Withholding, incomeEvent.Currency),
                rate.Value.Rate);
            var deadline = FilingDeadlineCalculator.CalculateDeadline(incomeEvent.Date, context.Holidays);

            return Result<IncomeEventAssessment, Error>.Success(new IncomeEventAssessment(info, rate.Value, deadline));
        }
        catch (DomainException ex)
        {
            return Result<IncomeEventAssessment, Error>.Failure(new Error(ErrorCodes.DOMAIN_ERROR, ex.Message));
        }
    }

    /// <summary>
    /// Assesses <paramref name="incomeEvent"/> and adds its filing to <paramref name="filings"/>
    /// unless a filing for the same taxpayer, paying entity (case-insensitive) and income date
    /// already exists. Inside a unit of work, filings staged earlier in the same operation count.
    /// Storage failures are not caught: they are not a property of the event.
    /// </summary>
    public async Task<FilingOutcome> FileAsync(
        IncomeEvent incomeEvent,
        FilingContext context,
        IFilingRepository filings,
        string? paymentNotes = null,
        CancellationToken ct = default)
    {
        var assessment = await AssessAsync(incomeEvent, context, ct);
        if (!assessment.IsSuccess)
            return new FilingOutcome.Failed(assessment.Error);

        var (info, rate, deadline) = assessment.Value;

        var existing = await filings.GetByIncomeEventAsync(
            context.TaxpayerProfileId, incomeEvent.PayingEntity, incomeEvent.Date, ct);

        if (existing.Count > 0)
        {
            var sameGross = existing.FirstOrDefault(f => f.GrossIncomeRsd == info.GrossIncomeRsd);
            if (sameGross is not null)
                return new FilingOutcome.AlreadyFiled(sameGross);

            return new FilingOutcome.CorrectionDetected(existing[0], info.GrossIncomeRsd,
                new Error(ErrorCodes.FILING_CORRECTION_DETECTED,
                    $"A filing for '{incomeEvent.PayingEntity}' on {incomeEvent.Date:yyyy-MM-dd} already exists " +
                    $"with gross {existing[0].GrossIncomeRsd} RSD; this event yields {info.GrossIncomeRsd} RSD " +
                    "(e.g. a broker correction). Review and adjust the filing manually."));
        }

        // A business-rule violation (DomainException) fails this one event; anything else — e.g. a
        // database error — propagates, so the caller can retry rather than lose the event.
        try
        {
            var filing = Filing.CreateFromIncome(
                info, context.TaxpayerProfileId, deadline,
                new FilingProvenance(
                    ReportId: context.ReportId,
                    ExchangeRateSourceDate: rate.SourceDate,
                    ExchangeRateSourceType: rate.SourceType,
                    Ticker: incomeEvent.PayingEntity,
                    PaymentNotes: paymentNotes));

            await filings.AddAsync(filing, ct);
            return new FilingOutcome.Created(filing);
        }
        catch (DomainException ex)
        {
            return new FilingOutcome.Failed(new Error(ErrorCodes.DOMAIN_ERROR, ex.Message));
        }
    }
}
