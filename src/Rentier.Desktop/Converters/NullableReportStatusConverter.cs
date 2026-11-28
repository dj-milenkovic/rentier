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
            null => LocalizedText.Get(nameof(Strings.Reports_Filter_Status_All)),
            ReportStatus.Init => LocalizedText.Get(nameof(Strings.ReportStatus_Init)),
            ReportStatus.Processed => LocalizedText.Get(nameof(Strings.ReportStatus_Processed)),
            ReportStatus.PartialError => LocalizedText.Get(nameof(Strings.ReportStatus_PartialError)),
            ReportStatus.Error => LocalizedText.Get(nameof(Strings.ReportStatus_Error)),
            _ => value.ToString()
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return null;
    }
}
