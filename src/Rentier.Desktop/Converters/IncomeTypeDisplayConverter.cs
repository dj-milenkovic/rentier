using Avalonia.Data.Converters;
using Rentier.Desktop.Extensions;
using Rentier.Domain.Enums;

namespace Rentier.Desktop.Converters;

public static class IncomeTypeDisplayConverter
{
    public static readonly IValueConverter Instance =
        new FuncValueConverter<IncomeType, string>(t => t.ToDisplayString());
}
