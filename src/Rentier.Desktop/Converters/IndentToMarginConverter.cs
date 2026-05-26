using Avalonia;
using Avalonia.Data.Converters;

namespace Rentier.Desktop.Converters;

/// <summary>
/// Converts an <see cref="int"/> indent level to an <see cref="Thickness"/> left margin.
/// Each indent level adds 16px of left padding, which shifts child navigation icons
/// to visually indicate their nesting under the Settings group header.
/// </summary>
public sealed class IndentToMarginConverter : IValueConverter
{
    public static readonly IndentToMarginConverter Instance = new();

    private const double IndentWidth = 16.0;

    public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        var level = value is int i ? i : 0;
        return new Thickness(level * IndentWidth, 0, 0, 0);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        => throw new NotImplementedException();
}
