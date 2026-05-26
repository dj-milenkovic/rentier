using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Styling;
using Rentier.Application.Enums;

namespace Rentier.Desktop.Converters;

/// <summary>
/// <see cref="IMultiValueConverter"/> that returns a <see cref="StreamGeometry"/> sort-arrow
/// for use in DataGrid column header <see cref="Avalonia.Controls.PathIcon"/> bindings.
///
/// <para>Inputs (values list):
/// <list type="bullet">
///   <item><description>[0] <see cref="FilingSortColumn"/>? — the currently active sort column (null = unsorted)</description></item>
///   <item><description>[1] <see cref="bool"/> — true = descending, false = ascending</description></item>
/// </list>
/// </para>
///
/// <para>Parameter: <see cref="string"/> column tag matching a <see cref="FilingSortColumn"/> name.</para>
///
/// <para>Returns <c>SortAscIcon</c> geometry when the column matches and is ascending,
/// <c>SortDescIcon</c> when descending, or <c>null</c> when the column is not active.</para>
/// </summary>
public sealed class SortArrowConverter : IMultiValueConverter
{
    public static readonly SortArrowConverter Instance = new();

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values is null || values.Count < 2) return null;
        if (parameter is not string columnTag) return null;

        // Guard against Avalonia sending UnsetValue during binding initialisation.
        if (ReferenceEquals(values[0], AvaloniaProperty.UnsetValue) || ReferenceEquals(values[1], AvaloniaProperty.UnsetValue))
            return null;

        // values[0]: FilingSortColumn? (boxed as FilingSortColumn when non-null, or null when unsorted)
        FilingSortColumn? sortColumn = values[0] is FilingSortColumn col ? col : (FilingSortColumn?)null;
        if (sortColumn is null) return null;

        bool sortDescending = values[1] is bool b && b;

        if (!Enum.TryParse<FilingSortColumn>(columnTag, out var target)) return null;
        if (sortColumn.Value != target) return null;

        // Column is the active sort column — return the appropriate chevron geometry.
        var key = sortDescending ? "SortDescIcon" : "SortAscIcon";
        if (Avalonia.Application.Current?.TryGetResource(key, ThemeVariant.Default, out var res) == true
            && res is StreamGeometry sg)
        {
            return sg;
        }

        return null;
    }
}
