using System.Diagnostics.CodeAnalysis;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace Rentier.Desktop.Logging;

/// <summary>
/// Owns the process-wide logger and its level switch. Started once from <c>Program.Main</c> —
/// never from <c>App</c>, which headless tests run too and would otherwise write into the
/// developer's real logs folder. When a session was never started (tests, the smoke test) the
/// composition root uses <see cref="Disabled"/>, which writes nowhere.
/// </summary>
internal sealed class LoggingSession
{
    private LoggingSession(string logDirectory, LoggingLevelSwitch levelSwitch, Serilog.ILogger logger)
    {
        LogDirectory = logDirectory;
        LevelSwitch = levelSwitch;
        Logger = logger;
    }

    /// <summary>The session started by <see cref="Start"/>, or <c>null</c> if none was.</summary>
    public static LoggingSession? Current { get; private set; }

    public string LogDirectory { get; }

    public LoggingLevelSwitch LevelSwitch { get; }

    public Serilog.ILogger Logger { get; }

    /// <summary>A session around an existing logger and switch (for example one with an in-memory sink).</summary>
    public static LoggingSession Create(string logDirectory, LoggingLevelSwitch levelSwitch, Serilog.ILogger logger) =>
        new(logDirectory, levelSwitch, logger);

    /// <summary>A session that writes nowhere. Safe default for tests and tooling.</summary>
    public static LoggingSession CreateDisabled(string logDirectory) =>
        new(logDirectory, new LoggingLevelSwitch(LogEventLevel.Information), Serilog.Core.Logger.None);

    /// <summary>
    /// Starts file logging at Information. Never throws: if the log folder cannot be created or
    /// opened, logging is disabled rather than preventing the app from starting.
    /// </summary>
    [ExcludeFromCodeCoverage(Justification = "Creates real log files; exercised manually and by the release smoke test.")]
    public static LoggingSession Start(string logDirectory)
    {
        LoggingSession session;
        try
        {
            Directory.CreateDirectory(logDirectory);
            var levelSwitch = new LoggingLevelSwitch(LogEventLevel.Information);
            var sessionId = Guid.NewGuid().ToString("N")[..6];
            var logger = LoggingConfiguration.CreateFileLogger(logDirectory, levelSwitch, sessionId);
            session = new LoggingSession(logDirectory, levelSwitch, logger);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Logging is diagnostic only; a read-only profile must not stop the app.
            session = CreateDisabled(logDirectory);
        }

        Log.Logger = session.Logger;
        Current = session;
        return session;
    }
}
