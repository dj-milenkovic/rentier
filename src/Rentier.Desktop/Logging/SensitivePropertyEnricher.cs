using Serilog.Core;
using Serilog.Events;

namespace Rentier.Desktop.Logging;

/// <summary>
/// Backstop for mistakes: any structured property whose name suggests personal, credential or
/// financial data is replaced with a placeholder before it reaches a sink. Free-text names
/// (<c>Message</c>, <c>Reason</c>) are included because existing error strings embed raw
/// exception text and parser output.
/// </summary>
internal sealed class SensitivePropertyEnricher : ILogEventEnricher
{
    private const string PLACEHOLDER = "[redacted]";

    private static readonly HashSet<string> SENSITIVE_NAMES = new(StringComparer.OrdinalIgnoreCase)
    {
        "Password", "Secret", "Token", "Username", "User", "Email", "Host", "Subject",
        "FileName", "ReportName", "Amount", "Jmbg", "Account", "Iban",
        "EntityName", "PayingEntity", "Ticker", "Isin",
        "Message", "Reason",
    };

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        List<string>? hits = null;
        foreach (var name in logEvent.Properties.Keys)
        {
            if (SENSITIVE_NAMES.Contains(name))
                (hits ??= []).Add(name);
        }

        if (hits is null)
            return;

        foreach (var name in hits)
            logEvent.AddOrUpdateProperty(new LogEventProperty(name, new ScalarValue(PLACEHOLDER)));
    }
}
