using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.Filings;
using Rentier.Application.Interfaces;
using Rentier.Application.Repositories;

namespace Rentier.Application.Handlers;

/// <summary>
/// Validates a manual entry and files it through <see cref="IncomeEventFiler"/> — the same rate,
/// tax, deadline and duplicate rules as statement imports — and returns the new filing's ID.
/// </summary>
public sealed class CreateManualFilingCommandHandler
    : ICommandHandler<CreateManualFilingCommand, Result<Guid, Error>>
{
    private readonly IncomeEventFiler _filer;
    private readonly IHolidayRepository _holidayRepository;
    private readonly IFilingRepository _filingRepository;

    public CreateManualFilingCommandHandler(
        IExchangeRateResolver exchangeRateResolver,
        IHolidayRepository holidayRepository,
        IFilingRepository filingRepository)
    {
        _filer = new IncomeEventFiler(exchangeRateResolver);
        _holidayRepository = holidayRepository;
        _filingRepository = filingRepository;
    }

    public async Task<Result<Guid, Error>> HandleAsync(
        CreateManualFilingCommand command, CancellationToken ct = default)
    {
        var incomeEvent = IncomeEvent.FromManualEntry(
            command.IncomeType, command.Ticker, command.IncomeDate,
            command.Currency, command.GrossAmount, command.NetReceived);
        if (!incomeEvent.IsSuccess)
            return Result<Guid, Error>.Failure(incomeEvent.Error);

        var holidays = await _holidayRepository.GetHolidayCalendarAsync(ct);
        var context = new FilingContext(command.TaxpayerProfileId, holidays);

        var outcome = await _filer.FileAsync(
            incomeEvent.Value, context, _filingRepository, command.PaymentNotes, ct);

        return outcome switch
        {
            FilingOutcome.Created created => Result<Guid, Error>.Success(created.Filing.Id),

            FilingOutcome.AlreadyFiled => Result<Guid, Error>.Failure(
                new Error(ErrorCodes.FILING_CREATE_DUPLICATE, "A filing with the same details already exists")),

            FilingOutcome.CorrectionDetected correction => Result<Guid, Error>.Failure(
                new Error(ErrorCodes.FILING_CORRECTION_DETECTED,
                    $"A filing for '{incomeEvent.Value.PayingEntity}' on {incomeEvent.Value.Date:yyyy-MM-dd} already " +
                    $"exists with gross {correction.Existing.GrossIncomeRsd} RSD (this entry: " +
                    $"{correction.NewGrossIncomeRsd} RSD). Edit or delete that filing instead of adding a second one.")),

            FilingOutcome.Failed failed => Result<Guid, Error>.Failure(failed.Error),

            _ => throw new System.Diagnostics.UnreachableException(
                $"Unhandled filing outcome {outcome.GetType().Name}."),
        };
    }
}
