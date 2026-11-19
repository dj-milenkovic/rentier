using System.Text;

namespace Rentier.Desktop.Logging;

/// <summary>
/// Renders an exception for the log without echoing messages that are known to embed user data.
/// CsvHelper exceptions carry the raw CSV record, and <see cref="FormatException"/> messages on
/// modern .NET include the offending input (often an amount). For those the type and stack trace
/// are kept and the message is dropped.
/// </summary>
internal static class SafeExceptionRenderer
{
    private const int MAX_DEPTH = 8;
    private const string CSV_HELPER_NAMESPACE = "CsvHelper.";
    private const string SUPPRESSED = "[message suppressed]";

    public static string Render(Exception exception)
    {
        var sb = new StringBuilder();
        Append(sb, exception, depth: 0);
        return sb.ToString();
    }

    private static void Append(StringBuilder sb, Exception exception, int depth)
    {
        var indent = new string(' ', depth * 2);
        var typeName = exception.GetType().FullName ?? exception.GetType().Name;

        sb.Append(indent).Append(typeName).Append(": ")
          .AppendLine(IsMessageSensitive(exception) ? SUPPRESSED : exception.Message);

        if (!string.IsNullOrEmpty(exception.StackTrace))
            sb.AppendLine(exception.StackTrace);

        if (depth >= MAX_DEPTH)
            return;

        if (exception is AggregateException aggregate)
        {
            foreach (var inner in aggregate.InnerExceptions)
                Append(sb, inner, depth + 1);
        }
        else if (exception.InnerException is { } innerException)
        {
            Append(sb, innerException, depth + 1);
        }
    }

    private static bool IsMessageSensitive(Exception exception)
    {
        if (exception is FormatException)
            return true;

        // Match by name so Desktop needs no CsvHelper reference; walk the hierarchy so derived
        // exception types are covered too.
        for (var type = exception.GetType(); type is not null; type = type.BaseType)
        {
            if (type.FullName?.StartsWith(CSV_HELPER_NAMESPACE, StringComparison.Ordinal) == true)
                return true;
        }

        return false;
    }
}
