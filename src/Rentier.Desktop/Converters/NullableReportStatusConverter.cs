using System.Globalization;
using Avalonia.Data.Converters;
using Rentier.Desktop.Resources;
using Rentier.Domain.Enums;

namespace Rentier.Desktop.Converters;

/// <summary>Converts ReportStatus? to/from display string. Null maps to the "All" label.</summary>
public sealed class NullableReportStatusConverter : IValueConverter
{
    public static readonly NullableReportStatusConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            null => Strings.Reports_Filter_Status_All,
            ReportStatus.Init => Strings.ReportStatus_Init,
            ReportStatus.Processed => Strings.ReportStatus_Processed,
            ReportStatus.PartialError => Strings.ReportStatus_PartialError,
            ReportStatus.Error => Strings.ReportStatus_Error,
            _ => value.ToString()
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return null;
    }
}
