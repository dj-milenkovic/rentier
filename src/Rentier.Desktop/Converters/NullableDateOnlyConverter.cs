using System.Globalization;
using Avalonia.Data.Converters;

namespace Rentier.Desktop.Converters;

/// <summary>Converts DateOnly? to/from string (yyyy-MM-dd format). Returns null for unparseable input.</summary>
public sealed class NullableDateOnlyConverter : IValueConverter
{
    public static readonly NullableDateOnlyConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is DateOnly d)
            return d.ToString("yyyy-MM-dd");
        return string.Empty;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string s && DateOnly.TryParse(s, out var d))
            return (DateOnly?)d;
        return null;
    }
}
