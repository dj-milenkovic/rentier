using Avalonia.Data.Converters;
using Rentier.Desktop.Extensions;
using Rentier.Domain.Enums;

namespace Rentier.Desktop.Converters;

public static class FilingStatusDisplayConverter
{
    public static readonly IValueConverter Instance =
        new FuncValueConverter<FilingStatus, string>(s => s.ToDisplayString());
}
