namespace Rentier.Application.Parsing;
// NOT the same as domain ExchangeRate (which has RateToRsd for NBS rates)
// IBKR rates are FromCurrency→ToCurrency(USD), used for cross-rate calc
public sealed record IbkrExchangeRate(DateOnly Date, string FromCurrency, string ToCurrency, decimal Rate);
