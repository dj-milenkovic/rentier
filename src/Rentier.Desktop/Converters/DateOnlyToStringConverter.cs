using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace Rentier.Desktop.Converters;

/// <summary>Converts <see cref="DateOnly"/> to/from yyyy-MM-dd string for DataGrid two-way bindings.</summary>
public sealed class DateOnlyToStringConverter : IValueConverter
{
    public static readonly DateOnlyToStringConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is DateOnly d) return d.ToString("yyyy-MM-dd");
        return string.Empty;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var s = value as string;
        if (string.IsNullOrWhiteSpace(s))
            return new BindingNotification(new FormatException("Invalid date. Use yyyy-MM-dd."), BindingErrorType.DataValidationError);
        if (DateOnly.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
            return d;
        return new BindingNotification(new FormatException("Invalid date. Use yyyy-MM-dd."), BindingErrorType.DataValidationError);
    }
}
