using System.Globalization;
using Avalonia.Data.Converters;
using Rentier.Desktop.ViewModels;
using Rentier.Domain.Enums;

namespace Rentier.Desktop.Converters;

/// <summary>
/// Renders <see cref="SyncViewModel.BuildImpactSummary"/> in the user's language from
/// (SelectedSyncMode, ReplayFromDateOffset).
/// </summary>
public sealed class SyncImpactSummaryConverter : IMultiValueConverter
{
    public static readonly SyncImpactSummaryConverter Instance = new();

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 1 || values[0] is not SyncMode mode)
            return string.Empty;

        var replayFrom = values.Count > 1 ? values[1] as DateTimeOffset? : null;
        return SyncViewModel.BuildImpactSummary(mode, replayFrom, LocalizedText.Get);
    }
}
