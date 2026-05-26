namespace Rentier.Application.Enums;

/// <summary>
/// Identifies the column by which filings should be sorted in GetFilingsQuery.
/// Values are stable integers — do not reorder.
/// </summary>
public enum FilingSortColumn
{
    FilingDeadline = 0,
    Status = 1,
    IncomeType = 2,
    PayingEntity = 3,
    TaxPayable = 4,
    PaymentReference = 5
}
