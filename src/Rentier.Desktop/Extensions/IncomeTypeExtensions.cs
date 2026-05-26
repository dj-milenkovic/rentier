using Rentier.Desktop.Resources;
using Rentier.Domain.Enums;

namespace Rentier.Desktop.Extensions;

public static class IncomeTypeExtensions
{
    public static string ToDisplayString(this IncomeType incomeType) => incomeType switch
    {
        IncomeType.Dividend => Strings.IncomeType_Dividend,
        IncomeType.Interest => Strings.IncomeType_Interest,
        _ => incomeType.ToString()
    };
}
