using Avalonia.Data.Converters;
using Avalonia.Media;
using Rentier.Domain.Entities;

namespace Rentier.Desktop.Converters;

public static class FilingStatusBrushConverter
{
    public static readonly IValueConverter Instance =
        new FuncValueConverter<FilingStatus, IBrush>(s => s switch
        {
            FilingStatus.Init   => new SolidColorBrush(Color.Parse("#D4A017")),
            FilingStatus.Filed  => new SolidColorBrush(Color.Parse("#0063B1")),
            FilingStatus.Paid   => new SolidColorBrush(Color.Parse("#107C10")),
            _                   => Brushes.Transparent
        });
}
