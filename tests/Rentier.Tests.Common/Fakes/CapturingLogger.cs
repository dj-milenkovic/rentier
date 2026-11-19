using Microsoft.Extensions.Logging;

namespace Rentier.Tests.Common.Fakes;

/// <summary>One captured log call: level, the formatted message, and the exception passed to the logger.</summary>
public sealed record CapturedLogEntry(LogLevel Level, string Message, Exception? Exception);

/// <summary>
/// An <see cref="ILogger{TCategoryName}"/> that records every call so tests can assert what was
/// logged (and, just as important for privacy, what was not). Simpler than substituting
/// <c>ILogger.Log&lt;TState&gt;</c>.
/// </summary>
public sealed class CapturingLogger<T> : ILogger<T>
{
    private readonly List<CapturedLogEntry> _entries = [];
    private readonly Lock _gate = new();

    public IReadOnlyList<CapturedLogEntry> Entries
    {
        get
        {
            lock (_gate)
                return [.. _entries];
        }
    }

    /// <summary>Every captured message joined into one string, for "must not contain" assertions.</summary>
    public string AllMessages => string.Join('\n', Entries.Select(e => e.Message));

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        var entry = new CapturedLogEntry(logLevel, formatter(state, exception), exception);
        lock (_gate)
            _entries.Add(entry);
    }
}
