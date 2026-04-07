using Avalonia.Data.Converters;
using Rentier.Desktop.Extensions;
using Rentier.Domain.Enums;

namespace Rentier.Desktop.Converters;

public static class ReportTypeDisplayConverter
{
    public static readonly IValueConverter Instance =
        new FuncValueConverter<ReportType, string>(rt => rt.ToDisplayString());
}
