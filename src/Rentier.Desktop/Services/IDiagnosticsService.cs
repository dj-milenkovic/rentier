namespace Rentier.Desktop.Services;

/// <summary>
/// Runtime controls for the diagnostic log: where it lives and whether Debug-level detail is
/// written. Kept free of any logging-library types so ViewModels depend only on this abstraction.
/// </summary>
public interface IDiagnosticsService
{
    /// <summary>Absolute path of the folder holding the daily log files.</summary>
    string LogDirectory { get; }

    /// <summary>True while Debug-level detail is being written to the log.</summary>
    bool IsDebugLoggingEnabled { get; }

    /// <summary>Switches Debug-level logging on or off immediately, without a restart.</summary>
    void SetDebugLogging(bool enabled);
}
