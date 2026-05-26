using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Styling;

namespace Rentier.Desktop.Converters;

/// <summary>
/// Converts a <see cref="bool"/> (flyout IsActive) to a <see cref="IBrush"/>.
/// Returns <c>RentierAccentBrush</c> when active, <c>RentierTextSecondaryBrush</c> when inactive.
/// Used to colour the funnel filter icon in DataGrid column headers.
/// </summary>
public sealed class FilterActiveConverter : IValueConverter
{
    public static readonly FilterActiveConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var active = value is bool b && b;
        var key = active ? "RentierAccentBrush" : "RentierTextSecondaryBrush";

        if (Avalonia.Application.Current?.TryGetResource(key, ThemeVariant.Default, out var res) == true
            && res is IBrush brush)
        {
            return brush;
        }

        // Fallback colours (should never be reached in production)
        return active ? Brushes.DodgerBlue : Brushes.Gray;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException($"{nameof(FilterActiveConverter)} does not support ConvertBack.");
}
