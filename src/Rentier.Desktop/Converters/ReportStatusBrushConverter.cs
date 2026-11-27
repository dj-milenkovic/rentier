using Avalonia.Data.Converters;
using Avalonia.Media;
using Rentier.Domain.Enums;

namespace Rentier.Desktop.Converters;

/// <summary>
/// Tinted badge colors for a report status: <see cref="Background"/> fills the pill and
/// <see cref="Border"/> outlines it. Badge text uses the primary text brush, so contrast holds
/// in both themes while the color carries the meaning (neutral, success, warning, danger).
/// Brushes are read at call time so the active theme variant is used.
/// </summary>
public static class ReportStatusBrushConverter
{
    public static readonly IValueConverter Background =
        new FuncValueConverter<ReportStatus, IBrush>(s => GetBrush(s switch
        {
            ReportStatus.Processed => "RentierSuccessBackgroundBrush",
            ReportStatus.PartialError => "RentierWarningBackgroundBrush",
            ReportStatus.Error => "RentierDangerBackgroundBrush",
            _ => "RentierCardElevatedBrush"
        }));

    public static readonly IValueConverter Border =
        new FuncValueConverter<ReportStatus, IBrush>(s => GetBrush(s switch
        {
            ReportStatus.Processed => "RentierSuccessForegroundBrush",
            ReportStatus.PartialError => "RentierWarningForegroundBrush",
            ReportStatus.Error => "RentierDangerForegroundBrush",
            _ => "RentierBorderBrush"
        }));

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
