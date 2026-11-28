using Avalonia.Data.Converters;
using Rentier.Desktop.Resources;
using Rentier.Domain.Enums;

namespace Rentier.Desktop.Converters;

/// <summary>Maps <see cref="IncomeType"/> to its display text in the current UI language.</summary>
public static class IncomeTypeDisplayConverter
{
    public static readonly IValueConverter Instance =
        new FuncValueConverter<IncomeType, string>(t => t switch
        {
            IncomeType.Dividend => LocalizedText.Get(nameof(Strings.IncomeType_Dividend)),
            IncomeType.Interest => LocalizedText.Get(nameof(Strings.IncomeType_Interest)),
            _ => t.ToString()
        });
}
