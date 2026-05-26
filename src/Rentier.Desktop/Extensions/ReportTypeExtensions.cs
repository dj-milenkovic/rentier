using Rentier.Domain.Enums;

namespace Rentier.Desktop.Extensions;

public static class ReportTypeExtensions
{
    public static string ToDisplayString(this ReportType reportType) => reportType switch
    {
        ReportType.IbkrCsv => "IBKR CSV",
        _ => reportType.ToString()
    };
}
