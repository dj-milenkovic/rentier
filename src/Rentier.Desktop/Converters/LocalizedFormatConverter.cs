using System.Globalization;
using Avalonia.Data.Converters;

namespace Rentier.Desktop.Converters;

/// <summary>
/// Formats the bound values with a localized composite format string whose resource key is the
/// <c>ConverterParameter</c> — e.g. <c>Filings_Page_Indicator</c> ("Strana {0} od {1}") with
/// CurrentPage and TotalPages.
/// </summary>
public sealed class LocalizedFormatConverter : IMultiValueConverter
{
    public static readonly LocalizedFormatConverter Instance = new();

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (parameter is not string key)
            return null;

        var args = values.Select(v => v is Avalonia.UnsetValueType ? null : v).ToArray();
        return string.Format(CultureInfo.CurrentCulture, LocalizedText.Get(key), args);
    }
}
