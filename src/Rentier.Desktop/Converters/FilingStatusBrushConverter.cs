using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Rentier.Domain.Enums;

namespace Rentier.Desktop.Converters;

public static class FilingStatusBrushConverter
{
    public static readonly IValueConverter Instance =
        new FuncValueConverter<FilingStatus, IBrush>(s => s switch
        {
            FilingStatus.Init => GetBrush("RentierStatusInitBrush"),
            FilingStatus.Filed => GetBrush("RentierStatusFiledBrush"),
            FilingStatus.Paid => GetBrush("RentierStatusPaidBrush"),
            _ => Brushes.Transparent
        });

    private static IBrush GetBrush(string key)
    {
        var app = global::Avalonia.Application.Current;
        if (app is not null
            && app.TryGetResource(key, app.ActualThemeVariant, out var res)
            && res is IBrush b)
            return b;
        return Brushes.Transparent;
    }
}
