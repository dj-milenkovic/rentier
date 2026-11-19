using Rentier.Desktop.Services;
using Serilog.Events;

namespace Rentier.Desktop.Logging;

/// <summary>Adapts <see cref="LoggingSession"/> to the UI-facing <see cref="IDiagnosticsService"/>.</summary>
internal sealed class DiagnosticsService(LoggingSession session) : IDiagnosticsService
{
    public string LogDirectory => session.LogDirectory;

    public bool IsDebugLoggingEnabled => session.LevelSwitch.MinimumLevel <= LogEventLevel.Debug;

    public void SetDebugLogging(bool enabled) =>
        session.LevelSwitch.MinimumLevel = enabled ? LogEventLevel.Debug : LogEventLevel.Information;
}
