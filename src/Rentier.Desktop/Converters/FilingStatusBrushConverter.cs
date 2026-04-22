using Avalonia.Data.Converters;
using Avalonia.Media;
using Rentier.Domain.Enums;

namespace Rentier.Desktop.Converters;

public static class FilingStatusBrushConverter
{
    // Cached to avoid allocating a new SolidColorBrush instance on every cell render
    private static readonly SolidColorBrush InitBrush  = new(Color.Parse("#D4A017"));
    private static readonly SolidColorBrush FiledBrush = new(Color.Parse("#0063B1"));
    private static readonly SolidColorBrush PaidBrush  = new(Color.Parse("#107C10"));

    public static readonly IValueConverter Instance =
        new FuncValueConverter<FilingStatus, IBrush>(s => s switch
        {
            FilingStatus.Init   => InitBrush,
            FilingStatus.Filed  => FiledBrush,
            FilingStatus.Paid   => PaidBrush,
            _                   => Brushes.Transparent
        });
}
