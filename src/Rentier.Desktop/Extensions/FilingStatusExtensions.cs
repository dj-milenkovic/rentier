using Rentier.Desktop.Resources;
using Rentier.Domain.Enums;

namespace Rentier.Desktop.Extensions;

public static class FilingStatusExtensions
{
    public static string ToDisplayString(this FilingStatus status) => status switch
    {
        FilingStatus.Init => Strings.FilingStatus_Init,
        FilingStatus.Filed => Strings.FilingStatus_Filed,
        FilingStatus.Paid => Strings.FilingStatus_Paid,
        _ => status.ToString()
    };
}
