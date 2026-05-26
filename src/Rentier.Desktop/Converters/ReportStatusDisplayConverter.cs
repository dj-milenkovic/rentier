using Avalonia.Data.Converters;
using Rentier.Desktop.Resources;
using Rentier.Domain.Enums;

namespace Rentier.Desktop.Converters;

/// <summary>Maps ReportStatus enum values to localized display strings.</summary>
public static class ReportStatusDisplayConverter
{
    public static readonly IValueConverter Instance =
        new FuncValueConverter<ReportStatus, string>(s => s switch
        {
            ReportStatus.Init => Strings.ReportStatus_Init,
            ReportStatus.Processed => Strings.ReportStatus_Processed,
            ReportStatus.PartialError => Strings.ReportStatus_PartialError,
            ReportStatus.Error => Strings.ReportStatus_Error,
            _ => s.ToString()
        });
}
