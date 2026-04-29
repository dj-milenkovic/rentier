using System.Globalization;
using Avalonia.Data.Converters;
using Rentier.Application.Enums;

namespace Rentier.Desktop.Converters;

/// <summary>Converts ComparisonOperator enum to/from int index (0=Equals, 1=GreaterThan, 2=LessThan).</summary>
public sealed class ComparisonOperatorIndexConverter : IValueConverter
{
    public static readonly ComparisonOperatorIndexConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ComparisonOperator op)
            return (int)op;
        return 0;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int index)
            return (ComparisonOperator)index;
        return ComparisonOperator.Equals;
    }
}
