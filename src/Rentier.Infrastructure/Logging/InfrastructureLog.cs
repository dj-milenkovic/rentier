using Microsoft.Extensions.Logging;

namespace Rentier.Infrastructure.Logging;

/// <summary>
/// Source-generated log messages for Infrastructure adapters. Generated methods check
/// <see cref="ILogger.IsEnabled"/> before doing any work, so disabled levels (Debug, most of the
/// time) cost nothing. Keeping every template here also makes the content rule easy to review:
/// ids, codes, counts and dates only — never amounts, tickers, names, subjects or raw text.
/// </summary>
internal static partial class InfrastructureLog
{
    // ── NBS exchange rates (1xxx) ────────────────────────────────────────────

    [LoggerMessage(1001, LogLevel.Debug, "NBS rate cache hit: {Currency} on {Date}")]
    public static partial void NbsCacheHit(this ILogger logger, string currency, DateOnly date);

    [LoggerMessage(1002, LogLevel.Debug, "NBS rate cache miss: {Currency} on {Date}; fetching from the NBS web app")]
    public static partial void NbsCacheMiss(this ILogger logger, string currency, DateOnly date);

    [LoggerMessage(1003, LogLevel.Warning, "NBS web app returned HTTP {StatusCode} for {Date}")]
    public static partial void NbsHttpError(this ILogger logger, int statusCode, DateOnly date);

    [LoggerMessage(1004, LogLevel.Warning, "NBS web app request failed for {Date}")]
    public static partial void NbsRequestFailed(this ILogger logger, Exception exception, DateOnly date);

    [LoggerMessage(1005, LogLevel.Debug, "Parsed {Count} NBS rates for {Date}")]
    public static partial void NbsRatesParsed(this ILogger logger, int count, DateOnly date);

    [LoggerMessage(1006, LogLevel.Warning, "Could not cache {Count} NBS rates for {Date}")]
    public static partial void NbsCacheWriteFailed(this ILogger logger, Exception exception, int count, DateOnly date);

    [LoggerMessage(1007, LogLevel.Warning, "Failed to parse the NBS web app page for {Date}")]
    public static partial void NbsParseFailed(this ILogger logger, Exception exception, DateOnly date);

    [LoggerMessage(1008, LogLevel.Warning, "Failed to write NBS rates to cache for {Date}. Rates will be re-fetched next time.")]
    public static partial void NbsApiCacheWriteFailed(this ILogger logger, Exception exception, DateOnly date);

    // ── IBKR statement parsing (2xxx) ────────────────────────────────────────

    [LoggerMessage(2001, LogLevel.Warning, "Could not read the statement as CSV")]
    public static partial void StatementUnreadable(this ILogger logger, Exception exception);

    [LoggerMessage(2002, LogLevel.Warning, "Statement has no recognised IBKR sections ({RowCount} rows)")]
    public static partial void StatementHasNoSections(this ILogger logger, int rowCount);

    [LoggerMessage(2003, LogLevel.Debug, "Parsed statement: {Dividends} dividends, {Interest} interest, {FxRates} FX rates, {Diagnostics} diagnostics")]
    public static partial void StatementParsed(this ILogger logger, int dividends, int interest, int fxRates, int diagnostics);

    [LoggerMessage(2004, LogLevel.Warning, "Unexpected failure while parsing the statement")]
    public static partial void StatementParseFailed(this ILogger logger, Exception exception);

    // ── Holiday import (3xxx) ────────────────────────────────────────────────

    [LoggerMessage(3001, LogLevel.Warning, "Holiday import request failed for {Year}")]
    public static partial void HolidayRequestFailed(this ILogger logger, Exception exception, int year);

    [LoggerMessage(3002, LogLevel.Warning, "Holiday page for {Year} has no #holidays-table; the site layout may have changed")]
    public static partial void HolidayTableMissing(this ILogger logger, int year);

    [LoggerMessage(3003, LogLevel.Warning, "No national holidays found for {Year}")]
    public static partial void NoHolidaysFound(this ILogger logger, int year);

    [LoggerMessage(3004, LogLevel.Debug, "Imported {Count} national holidays for {Year}")]
    public static partial void HolidaysImported(this ILogger logger, int count, int year);

    [LoggerMessage(3005, LogLevel.Warning, "Failed to parse the holiday page for {Year}")]
    public static partial void HolidayParseFailed(this ILogger logger, Exception exception, int year);

    // ── Mailbox sync (4xxx) ──────────────────────────────────────────────────

    [LoggerMessage(4001, LogLevel.Information, "Syncing mailbox {MailboxId} with {ImporterCount} importers")]
    public static partial void MailboxSyncStarted(this ILogger logger, Guid mailboxId, int importerCount);

    [LoggerMessage(4002, LogLevel.Warning, "Mailbox {MailboxId}: password unavailable ({Code}); sync skipped")]
    public static partial void MailboxPasswordUnavailable(this ILogger logger, Guid mailboxId, string code);

    [LoggerMessage(4003, LogLevel.Information, "Mailbox {MailboxId} sync finished: {ReportsCreated} reports created, {ErrorCount} errors")]
    public static partial void MailboxSyncFinished(this ILogger logger, Guid mailboxId, int reportsCreated, int errorCount);

    [LoggerMessage(4004, LogLevel.Warning, "IMAP sync failed for mailbox {MailboxId}")]
    public static partial void MailboxSyncFailed(this ILogger logger, Exception exception, Guid mailboxId);

    [LoggerMessage(4005, LogLevel.Debug, "Importer {ImporterId}: {MessageCount} matching messages")]
    public static partial void ImporterMessagesMatched(this ILogger logger, Guid importerId, int messageCount);

    [LoggerMessage(4006, LogLevel.Debug, "Importer {ImporterId}: processing UID {Uid}")]
    public static partial void ImporterProcessingMessage(this ILogger logger, Guid importerId, uint uid);

    [LoggerMessage(4007, LogLevel.Warning, "Importer {ImporterId}: message UID {Uid} failed")]
    public static partial void ImporterMessageFailed(this ILogger logger, Exception exception, Guid importerId, uint uid);

    [LoggerMessage(4008, LogLevel.Warning, "Importer {ImporterId} failed")]
    public static partial void ImporterFailed(this ILogger logger, Exception exception, Guid importerId);

    // ── Startup (5xxx) ───────────────────────────────────────────────────────

    [LoggerMessage(5001, LogLevel.Information, "Credential store provider: {Provider}")]
    public static partial void CredentialStoreSelected(this ILogger logger, string provider);

    [LoggerMessage(5002, LogLevel.Warning, "Credential store unavailable ({Code}); mailbox credentials cannot be saved or read")]
    public static partial void CredentialStoreUnavailable(this ILogger logger, string code);

    [LoggerMessage(5003, LogLevel.Debug, "Update check failed silently; the app will continue without update information.")]
    public static partial void UpdateCheckFailed(this ILogger logger, Exception exception);

    // ── Persistence (6xxx) ───────────────────────────────────────────────────

    [LoggerMessage(6001, LogLevel.Error, "Transaction rollback failed after a holiday save error.")]
    public static partial void HolidayRollbackFailed(this ILogger logger, Exception exception);
}
