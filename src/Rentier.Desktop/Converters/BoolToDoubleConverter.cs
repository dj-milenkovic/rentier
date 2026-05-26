using System.Globalization;
using Avalonia.Data.Converters;

namespace Rentier.Desktop.Converters;

/// <summary>
/// Converts a <see cref="bool"/> to a <see cref="double"/> value.
/// Used to drive chevron rotation angles: <c>false</c> → <see cref="FalseValue"/> (default 0°),
/// <c>true</c> → <see cref="TrueValue"/> (default 90°).
/// </summary>
public sealed class BoolToDoubleConverter : IValueConverter
{
    /// <summary>Value returned when the binding is <c>true</c>. Default: 90.0.</summary>
    public double TrueValue { get; set; } = 90.0;

    /// <summary>Value returned when the binding is <c>false</c>. Default: 0.0.</summary>
    public double FalseValue { get; set; } = 0.0;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? TrueValue : FalseValue;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
