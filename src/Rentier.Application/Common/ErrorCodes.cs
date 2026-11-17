namespace Rentier.Application.Common;

/// <summary>
/// Centralized registry of all error code constants used by CQRS handlers.
/// All values follow SCREAMING_SNAKE_CASE and the ENTITY_ACTION_REASON naming convention.
/// Uniqueness is enforced by <c>ErrorCodesTests</c>.
/// </summary>
public static class ErrorCodes
{
    // ── Generic ────────────────────────────────────────────────────────────
    public const string DOMAIN_ERROR = "DOMAIN_ERROR";
    public const string NOT_FOUND = "NOT_FOUND";
    public const string INFRASTRUCTURE_ERROR = "INFRASTRUCTURE_ERROR";

    // ── Credential ─────────────────────────────────────────────────────────
    public const string CREDENTIAL_NOT_FOUND = "CREDENTIAL_NOT_FOUND";
    public const string CREDENTIAL_WRITE_FAILED = "CREDENTIAL_WRITE_FAILED";
    public const string CREDENTIAL_READ_FAILED = "CREDENTIAL_READ_FAILED";
    public const string CREDENTIAL_DELETE_FAILED = "CREDENTIAL_DELETE_FAILED";
    public const string PROVIDER_UNAVAILABLE = "PROVIDER_UNAVAILABLE";
    public const string UNSUPPORTED_PLATFORM = "UNSUPPORTED_PLATFORM";

    // ── Filing ─────────────────────────────────────────────────────────────
    public const string FILING_BULK_DELETE_FAILED = "FILING_BULK_DELETE_FAILED";
    public const string FILING_BULK_DELETE_INVALID = "FILING_BULK_DELETE_INVALID";
    public const string FILING_CREATE_DUPLICATE = "FILING_CREATE_DUPLICATE";
    public const string FILING_CORRECTION_DETECTED = "FILING_CORRECTION_DETECTED";

    // ── Report ─────────────────────────────────────────────────────────────
    public const string REPORT_BULK_DELETE_FAILED = "REPORT_BULK_DELETE_FAILED";
    public const string REPORT_BULK_DELETE_INVALID = "REPORT_BULK_DELETE_INVALID";
    public const string REPORT_DELETE_FAILED = "REPORT_DELETE_FAILED";
    public const string REPORT_QUERY_FAILED = "REPORT_QUERY_FAILED";
    public const string REPORT_IMPORT_FAILED = "REPORT_IMPORT_FAILED";
    public const string REPORT_IMPORT_DUPLICATE = "REPORT_IMPORT_DUPLICATE";
    public const string REPORT_IMPORT_INVALID_CSV = "REPORT_IMPORT_INVALID_CSV";
    public const string REPORT_PROCESS_NO_ATTACHMENT = "REPORT_PROCESS_NO_ATTACHMENT";
    public const string REPORT_PROCESS_NO_TAXPAYER = "REPORT_PROCESS_NO_TAXPAYER";
    public const string REPORT_PROCESS_PARSE_FAILED = "REPORT_PROCESS_PARSE_FAILED";

    // ── Dashboard ──────────────────────────────────────────────────────────
    public const string DASHBOARD_QUERY_FAILED = "DASHBOARD_QUERY_FAILED";

    // ── Importer ───────────────────────────────────────────────────────────
    public const string IMPORTER_NOT_FOUND = "IMPORTER_NOT_FOUND";
    public const string IMPORTER_VALIDATION_INVALID_REGEX = "IMPORTER_VALIDATION_INVALID_REGEX";

    // ── Exchange Rate ───────────────────────────────────────────────────────
    public const string INVALID_EXCHANGE_RATE = "INVALID_EXCHANGE_RATE";

    // ── Mailbox ────────────────────────────────────────────────────────────
    public const string MAILBOX_VALIDATION_FAILED = "MAILBOX_VALIDATION_FAILED";

    // ── Holiday ──────────────────────────────────────────────────────────
    public const string HOLIDAY_SAVE_DUPLICATE_DATES = "HOLIDAY_SAVE_DUPLICATE_DATES";
    public const string HOLIDAY_SAVE_INVALID_YEAR_RANGE = "HOLIDAY_SAVE_INVALID_YEAR_RANGE";
    public const string HOLIDAY_FETCH_ALL_FAILED = "HOLIDAY_FETCH_ALL_FAILED";

    // ── Pagination ─────────────────────────────────────────────────────────
    public const string PAGINATION_VALIDATION_FAILED = "PAGINATION_VALIDATION_FAILED";

    // ── Calculator ─────────────────────────────────────────────────────────
    public const string DATE_REQUIRED = "DATE_REQUIRED";
    public const string GROSS_REQUIRED = "GROSS_REQUIRED";
    public const string TICKER_REQUIRED = "TICKER_REQUIRED";
    public const string NET_EXCEEDS_GROSS = "NET_EXCEEDS_GROSS";
    public const string NET_NEGATIVE = "NET_NEGATIVE";
    public const string NETWORK_FAILURE = "NETWORK_FAILURE";
    public const string RATE_NOT_FOUND = "RATE_NOT_FOUND";

    /// <summary>
    /// The exchange-rate source could not be reached or read (temporary). Unlike
    /// <see cref="RATE_NOT_FOUND"/>, the rate may exist; report processing defers and retries.
    /// </summary>
    public const string RATE_SOURCE_UNAVAILABLE = "RATE_SOURCE_UNAVAILABLE";

    // ── Exchange-rate sources (IExchangeRateFetcher adapters) ───────────────
    // The resolver's retry/fallback decisions key off these, so adapters must use the
    // constants, never literals.

    /// <summary>NBS does not publish this currency; the only case where a broker cross-rate may apply.</summary>
    public const string UNSUPPORTED_CURRENCY = "UNSUPPORTED_CURRENCY";

    /// <summary>The NBS XML service could not be reached (network/HTTP failure).</summary>
    public const string NBS_HTTP_ERROR = "NBS_HTTP_ERROR";

    /// <summary>The NBS XML service answered with a document that could not be parsed.</summary>
    public const string NBS_PARSE_ERROR = "NBS_PARSE_ERROR";

    /// <summary>The NBS web-app fallback returned a page that could not be read.</summary>
    public const string NBS_SCRAPE_ERROR = "NBS_SCRAPE_ERROR";

    // ── Report processing retry ────────────────────────────────────────────
    public const string REPORT_PROCESS_DEFERRED = "REPORT_PROCESS_DEFERRED";
    public const string REPORT_PROCESS_ATTEMPTS_EXHAUSTED = "REPORT_PROCESS_ATTEMPTS_EXHAUSTED";
}
