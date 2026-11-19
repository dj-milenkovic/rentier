using System.Collections.Concurrent;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting;

namespace Rentier.UnitTests.Desktop.Logging;

/// <summary>In-memory Serilog sink: keeps the events and, optionally, each event rendered through a formatter.</summary>
internal sealed class CapturingSink(ITextFormatter? formatter = null) : ILogEventSink
{
    private readonly ConcurrentQueue<LogEvent> _events = new();
    private readonly ConcurrentQueue<string> _lines = new();

    public IReadOnlyCollection<LogEvent> Events => [.. _events];

    public IReadOnlyCollection<string> Lines => [.. _lines];

    public void Emit(LogEvent logEvent)
    {
        _events.Enqueue(logEvent);

        if (formatter is null)
            return;

        using var writer = new StringWriter();
        formatter.Format(logEvent, writer);
        _lines.Enqueue(writer.ToString().TrimEnd());
    }
}
