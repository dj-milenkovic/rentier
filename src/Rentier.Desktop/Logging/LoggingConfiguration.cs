using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace Rentier.Desktop.Logging;

/// <summary>
/// Builds the Serilog pipeline. <see cref="Create"/> returns the sink-less configuration (level
/// switch, namespace overrides, enrichers) so tests can attach an in-memory sink;
/// <see cref="CreateFileLogger"/> adds the daily file sink used by the app: one file per day
/// (<c>rentier-YYYYMMDD.log</c>), appended to by every session and instance that day.
/// </summary>
internal static class LoggingConfiguration
{
    public const string LOG_FILE_NAME = "rentier-.log";
    public const int RETENTION_DAYS = 30;

    // Per-day disk-space backstop. Exactly one file per day is the contract, so the file never
    // rolls on size: once a day's file reaches this size, further lines that day are dropped.
    private const long MAX_FILE_BYTES = 50L * 1024 * 1024;
    // One file per day: today's plus RETENTION_DAYS earlier days. Age (retainedFileTimeLimit)
    // is the real limit; the count is a backstop.
    private const int MAX_RETAINED_FILES = RETENTION_DAYS + 1;

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
                // No _NNN files: never roll on size, and share the file so a second instance
                // (or one still exiting, or stopped in the debugger) appends to the same day
                // file instead of opening rentier-YYYYMMDD_001.log next to the locked one.
                rollOnFileSizeLimit: false,
                shared: true,
                fileSizeLimitBytes: MAX_FILE_BYTES,
                retainedFileTimeLimit: TimeSpan.FromDays(RETENTION_DAYS),
                retainedFileCountLimit: MAX_RETAINED_FILES,
                buffered: false)
            .CreateLogger();
}
