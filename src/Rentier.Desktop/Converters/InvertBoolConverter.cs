using Avalonia.Data.Converters;

namespace Rentier.Desktop.Converters;

public static class InvertBoolConverter
{
    public static readonly IValueConverter Instance =
        new FuncValueConverter<bool, bool>(b => !b, b => !b);
}
