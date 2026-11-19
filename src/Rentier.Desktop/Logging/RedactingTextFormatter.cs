using System.Globalization;
using System.Text;
using Serilog.Events;
using Serilog.Formatting;

namespace Rentier.Desktop.Logging;

/// <summary>
/// Renders one log line (<c>timestamp [LVL] session Source message</c> plus an optional safe
/// exception block) and runs the whole rendered text through <see cref="PiiRedactor"/> before it
/// is written, so scrubbing covers the message and the exception alike.
/// </summary>
internal sealed class RedactingTextFormatter(string? homeFolder = null) : ITextFormatter
{
    private const string SESSION_ID_PROPERTY = "SessionId";
    private const string SOURCE_CONTEXT_PROPERTY = "SourceContext";
    private const string TIMESTAMP_FORMAT = "yyyy-MM-dd HH:mm:ss.fff zzz";

    public void Format(LogEvent logEvent, TextWriter output)
    {
        var sb = new StringBuilder();

        sb.Append(logEvent.Timestamp.ToString(TIMESTAMP_FORMAT, CultureInfo.InvariantCulture))
          .Append(" [").Append(LevelCode(logEvent.Level)).Append("] ")
          .Append(ScalarText(logEvent, SESSION_ID_PROPERTY) ?? "-").Append(' ');

        var source = ScalarText(logEvent, SOURCE_CONTEXT_PROPERTY);
        if (!string.IsNullOrEmpty(source))
            sb.Append(ShortSource(source)).Append(' ');

        sb.Append(logEvent.RenderMessage(CultureInfo.InvariantCulture));

        if (logEvent.Exception is not null)
        {
            sb.AppendLine().Append(SafeExceptionRenderer.Render(logEvent.Exception).TrimEnd());
        }

        output.WriteLine(PiiRedactor.Redact(sb.ToString(), homeFolder));
    }

    private static string LevelCode(LogEventLevel level) => level switch
    {
        LogEventLevel.Verbose => "VRB",
        LogEventLevel.Debug => "DBG",
        LogEventLevel.Information => "INF",
        LogEventLevel.Warning => "WRN",
        LogEventLevel.Error => "ERR",
        LogEventLevel.Fatal => "FTL",
        _ => "???",
    };

    private static string? ScalarText(LogEvent logEvent, string name) =>
        logEvent.Properties.TryGetValue(name, out var value) && value is ScalarValue scalar
            ? scalar.Value?.ToString()
            : null;

    private static string ShortSource(string source)
    {
        var lastDot = source.LastIndexOf('.');
        return lastDot >= 0 ? source[(lastDot + 1)..] : source;
    }
}
