using System.Globalization;
using Avalonia.Data.Converters;

namespace Rentier.Desktop.Converters;

/// <summary>Converts int? to/from string. Returns null for non-numeric input.</summary>
public sealed class NullableIntConverter : IValueConverter
{
    public static readonly NullableIntConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int i)
            return i.ToString();
        return string.Empty;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string s && int.TryParse(s, out var i))
            return (int?)i;
        return null;
    }
}
