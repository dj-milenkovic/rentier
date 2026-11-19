using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace Rentier.Desktop.Logging;

/// <summary>
/// Builds the Serilog pipeline. <see cref="Create"/> returns the sink-less configuration (level
/// switch, namespace overrides, enrichers) so tests can attach an in-memory sink;
/// <see cref="CreateFileLogger"/> adds the rolling file sink used by the app.
/// </summary>
internal static class LoggingConfiguration
{
    public const string LOG_FILE_NAME = "rentier-.log";
    public const int RETENTION_DAYS = 30;

    private const long MAX_FILE_BYTES = 10L * 1024 * 1024;
    // Disk-space backstop only. Retention is by age (RETENTION_DAYS); the count is high enough
    // that size-rolled files within 30 days are not deleted early.
    private const int MAX_RETAINED_FILES = 120;

    public static LoggerConfiguration Create(LoggingLevelSwitch levelSwitch, string sessionId) =>
        new LoggerConfiguration()
            .MinimumLevel.ControlledBy(levelSwitch)
            // These namespaces are chatty and can include SQL text, URLs and connection details.
            // Overrides apply regardless of the switch, so the Debug toggle never loosens them.
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
            .MinimumLevel.Override("System.Net.Http", LogEventLevel.Warning)
            .Enrich.With<SensitivePropertyEnricher>()
            .Enrich.WithProperty("SessionId", sessionId);

    public static Serilog.Core.Logger CreateFileLogger(
        string logDirectory, LoggingLevelSwitch levelSwitch, string sessionId) =>
        Create(levelSwitch, sessionId)
            .WriteTo.File(
                new RedactingTextFormatter(),
                Path.Combine(logDirectory, LOG_FILE_NAME),
                rollingInterval: RollingInterval.Day,
                rollOnFileSizeLimit: true,
                fileSizeLimitBytes: MAX_FILE_BYTES,
                retainedFileTimeLimit: TimeSpan.FromDays(RETENTION_DAYS),
                retainedFileCountLimit: MAX_RETAINED_FILES,
                buffered: false)
            .CreateLogger();
}
