using Microsoft.Extensions.Logging;
using Rentier.Domain.Enums;

namespace Rentier.Application.Logging;

/// <summary>
/// Source-generated log messages for Application handlers and services. Generated methods check
/// <see cref="ILogger.IsEnabled"/> before doing any work, so disabled levels cost nothing. Keeping
/// every template here also makes the content rule easy to review: ids, codes, counts and dates
/// only — never amounts, tickers, report names, payer names or exception messages.
/// </summary>
internal static partial class ApplicationLog
{
    // ── Handler infrastructure (1xxx) ────────────────────────────────────────

    [LoggerMessage(1001, LogLevel.Error, "[{Caller}] Unexpected failure")]
    public static partial void UnexpectedHandlerFailure(this ILogger logger, Exception exception, string? caller);

    // ── Mailboxes (2xxx) ─────────────────────────────────────────────────────

    [LoggerMessage(2001, LogLevel.Warning, "Failed to delete credential for mailbox {MailboxId}: [{Code}]")]
    public static partial void MailboxCredentialDeleteFailed(this ILogger logger, Guid mailboxId, string code);

    // ── Report processing (3xxx) ─────────────────────────────────────────────

    [LoggerMessage(3001, LogLevel.Error, "Report {ReportId} failed unexpectedly")]
    public static partial void ReportFailedUnexpectedly(this ILogger logger, Exception exception, Guid reportId);

    [LoggerMessage(3002, LogLevel.Warning, "Report {ReportId} rejected: [{Code}]")]
    public static partial void ReportRejected(this ILogger logger, Guid reportId, string code);

    [LoggerMessage(3003, LogLevel.Information, "Report {ReportId}: {Total} events, {Created} filings, {Failed} failed -> {Status}")]
    public static partial void ReportCommitted(this ILogger logger, Guid reportId, int total, int created, int failed, ReportStatus status);

    [LoggerMessage(3004, LogLevel.Error, "Report {ReportId}: rejection [{Code}] could not be saved")]
    public static partial void ReportRejectionNotSaved(this ILogger logger, Exception exception, Guid reportId, string code);

    [LoggerMessage(3005, LogLevel.Warning, "Report {ReportId} failed but is no longer pending (status {Status}); nothing to retry")]
    public static partial void ReportNoLongerPending(this ILogger logger, Guid reportId, string status);

    [LoggerMessage(3006, LogLevel.Error, "Report {ReportId} failed {Attempts} times and was marked Error")]
    public static partial void ReportMarkedError(this ILogger logger, Guid reportId, int attempts);

    [LoggerMessage(3007, LogLevel.Error, "Report {ReportId}: the failed attempt could not be recorded; it stays pending")]
    public static partial void ReportAttemptNotRecorded(this ILogger logger, Exception exception, Guid reportId);

    [LoggerMessage(3008, LogLevel.Warning, "Report {ReportId} row {Row}: [{Code}]")]
    public static partial void ReportRowDiagnostic(this ILogger logger, Guid reportId, int? row, string code);

    // ── Exchange-rate resolution (4xxx) ──────────────────────────────────────

    [LoggerMessage(4001, LogLevel.Warning, "Rate resolved (cross-rate): {Currency} on {Date} = {BrokerRate} {Pivot} x NBS {PivotRate} ({PivotDate})")]
    public static partial void RateResolvedByCrossRate(
        this ILogger logger, string currency, DateOnly date, decimal brokerRate, string pivot, decimal pivotRate, DateOnly pivotDate);

    [LoggerMessage(4002, LogLevel.Warning, "Rate resolved (fallback): {Currency} on {OriginalDate} used {FallbackDate}")]
    public static partial void RateResolvedByFallback(this ILogger logger, string currency, DateOnly originalDate, DateOnly fallbackDate);

    [LoggerMessage(4003, LogLevel.Warning, "Rate lookup for {Currency} on {Date} failed with {Code}; not walking back further")]
    public static partial void RateLookupStopped(this ILogger logger, string currency, DateOnly date, string code);

    [LoggerMessage(4004, LogLevel.Error, "Rate resolution exhausted for {Currency} on {Date}. Tried: {TriedDates}")]
    public static partial void RateResolutionExhausted(this ILogger logger, string currency, DateOnly date, string triedDates);
}
