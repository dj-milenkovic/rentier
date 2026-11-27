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
            ReportStatus.Init => LocalizedText.Get(nameof(Strings.ReportStatus_Init)),
            ReportStatus.Processed => LocalizedText.Get(nameof(Strings.ReportStatus_Processed)),
            ReportStatus.PartialError => LocalizedText.Get(nameof(Strings.ReportStatus_PartialError)),
            ReportStatus.Error => LocalizedText.Get(nameof(Strings.ReportStatus_Error)),
            _ => s.ToString()
        });
}
