using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Rentier.Application.DTOs;

namespace Rentier.Desktop.Converters;

public static class SyncSeverityBrushConverter
{
    public static readonly IValueConverter Instance =
        new FuncValueConverter<SyncProgressSeverity, IBrush>(s => s switch
        {
            SyncProgressSeverity.Error => GetBrush("RentierDangerForegroundBrush"),
            SyncProgressSeverity.Warning => GetBrush("RentierWarningForegroundBrush"),
            _ => GetBrush("RentierTextSecondaryBrush")
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
