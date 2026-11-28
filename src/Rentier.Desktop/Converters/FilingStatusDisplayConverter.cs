using Avalonia.Data.Converters;
using Rentier.Desktop.Resources;
using Rentier.Domain.Enums;

namespace Rentier.Desktop.Converters;

/// <summary>Maps <see cref="FilingStatus"/> to its display text in the current UI language.</summary>
public static class FilingStatusDisplayConverter
{
    public static readonly IValueConverter Instance =
        new FuncValueConverter<FilingStatus, string>(s => s switch
        {
            FilingStatus.Init => LocalizedText.Get(nameof(Strings.FilingStatus_Init)),
            FilingStatus.Filed => LocalizedText.Get(nameof(Strings.FilingStatus_Filed)),
            FilingStatus.Paid => LocalizedText.Get(nameof(Strings.FilingStatus_Paid)),
            _ => s.ToString()
        });
}
