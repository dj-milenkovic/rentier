using Rentier.Domain.Enums;

namespace Rentier.Application.Commands;

public sealed record CreateManualFilingCommand(
    Guid TaxpayerProfileId,
    IncomeType IncomeType,
    string Ticker,
    DateOnly IncomeDate,
    string Currency,
    decimal GrossAmount,
    decimal? NetReceived);
