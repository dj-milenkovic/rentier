using Microsoft.Extensions.Logging;

namespace Rentier.Desktop.Logging;

/// <summary>
/// Source-generated log messages for Desktop services that log through
/// <see cref="ILogger"/>. Generated methods check <see cref="ILogger.IsEnabled"/> first.
/// </summary>
internal static partial class DesktopLog
{
    [LoggerMessage(1001, LogLevel.Warning, "Could not open the log folder")]
    public static partial void LogFolderOpenFailed(this ILogger logger, Exception exception);
}
