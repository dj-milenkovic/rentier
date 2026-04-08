# Rentier Feature Roadmap

**Created**: 2026-04-06  
**Baseline**: `001-initial-setup` (complete — shell, domain stubs, CI)  
**Reference**: [dobkapapp](https://github.com/SergeiPatiakin/dobkapapp) (behavioral reference only)

---

## How to Use This Document

Each feature below is sized for a single spec-kit cycle:
**`speckit.specify` → `speckit.plan` → `speckit.tasks` → `speckit.implement`**

Features are ordered by dependency (top-down). Features in the same tier can be
developed in parallel. Each entry includes:

- **What it delivers** — scope and acceptance boundary
- **Depends on** — prerequisite features
- **Spec-kit prompt** — the exact prompt to feed `speckit.specify`

> **Sizing rule**: A well-sized feature has 2–5 user stories, touches ≤ 3 layers,
> and can be completed in one spec-kit cycle. If a feature grows beyond that,
> split it.

---

## Tier 0 — Data Foundation

These features wire up persistence and the settings screens. They have no
business logic but unlock every subsequent feature.

---

### 002 · Taxpayer Profile Management

**What it delivers**: The user can create, view, edit, and persist a single
taxpayer profile (JMBG, full name, street address, opština code, phone, email)
in SQLite. The Settings pane shows a form bound to the profile. Data survives
app restart.

**Layers touched**: Domain (enrich `TaxpayerProfile`), Application (commands/queries),
Infrastructure (EF Core mapping, repository impl), Desktop (Settings sub-view).

**Depends on**: `001-initial-setup`

**Spec-kit prompt**:
```
Implement taxpayer profile management for Rentier. The user opens Settings and
sees a form for their Serbian tax identity: JMBG (13-digit string), full name,
street address, opština code, phone number, and email. Only one profile exists
per app instance (singleton pattern). The form validates JMBG on save
(exactly 13 digits). Data is stored in local SQLite via EF Core and persists
across restarts. Use ITaxpayerProfileRepository (already defined in Application).
Add DbSet<TaxpayerProfile> to AppDbContext with a new EF migration. Create
SaveTaxpayerProfileCommand/Handler and GetTaxpayerProfileQuery/Handler in
Application. Build a SettingsView sub-view with ReactiveUI bindings. All
monetary/date rules from the constitution apply. No network calls.
```

---

### 003 · Holiday Configuration

**What it delivers**: The user can view and edit the list of Serbian public
holidays used for filing deadline calculations. Stored in SQLite. Seeded with
current-year defaults.

**Layers touched**: Domain (enrich `HolidayConf`), Application (commands/queries),
Infrastructure (EF mapping), Desktop (Settings sub-view).

**Depends on**: `001-initial-setup`

**Spec-kit prompt**:
```
Implement holiday configuration for Rentier. The user opens Settings → Holidays
and sees a list of Serbian public holidays (DateOnly values) with a configurable
year range (holiday_range_start, holiday_range_end). They can add, remove, and
edit holiday dates. Data is stored in local SQLite via EF Core. Seed the
database with Serbian public holidays for the current year (New Year 1–2 Jan,
Sretenje 15–16 Feb, Labour Day 1–2 May, Vidovdan 28 Jun, Armistice Day 11 Nov,
Christmas 7 Jan). Use HolidayConf value object from Domain. Create
SaveHolidayConfCommand/Handler and GetHolidayConfQuery/Handler. Build a
Settings sub-view with a DataGrid for dates. DateOnly only — no DateTime.
No network calls.
```

---

### 004 · Mailbox Configuration

**What it delivers**: The user can configure one or more IMAP mailboxes
(host, port, username) and securely store the password via OS credential store.
Mailbox cursor (date-based initial position) is user-settable.

**Layers touched**: Domain (enrich `Mailbox`, `MailboxCursor`), Application
(commands/queries, `ICredentialStore` usage), Infrastructure (EF mapping,
repository impl, `OsCredentialStore` real implementation for Windows),
Desktop (Settings sub-view).

**Depends on**: `001-initial-setup`

**Spec-kit prompt**:
```
Implement IMAP mailbox configuration for Rentier. The user opens Settings →
Mailboxes and can add/edit/delete mailbox connections. Each mailbox has: IMAP
host (default imap.gmail.com), port (default 993), email/username, and
password. Password MUST be stored via ICredentialStore (OS credential store) —
never in SQLite. Implement OsCredentialStore for Windows using
Windows.Security.Credentials.PasswordVault (constitution Principle II). The
user sets an initial cursor date (DateOnly) to control how far back to sync.
The cursor transitions from date-based to UID-based after first sync (handled
by the sync feature later). Use IMailboxRepository. Create
AddMailboxCommand/Handler, UpdateMailboxCommand/Handler,
DeleteMailboxCommand/Handler, GetMailboxesQuery/Handler. Add
DbSet<Mailbox> + migration. Build a Settings sub-view with a form and
password field (masked). All async. No actual IMAP connections yet.
```

---

### 005 · Importer Configuration

**What it delivers**: The user can configure importers that define how to
find and parse brokerage statement emails. Each importer links to a mailbox
and taxpayer profile, with email filters (from, subject, attachment regex)
and a report type (IBKR CSV).

**Layers touched**: Domain (enrich `Importer` with filter fields), Application
(commands/queries), Infrastructure (EF mapping), Desktop (Settings sub-view).

**Depends on**: `004-mailbox-configuration` (importer references mailbox),
`002-taxpayer-profile` (importer references taxpayer profile)

**Spec-kit prompt**:
```
Implement importer configuration for Rentier. The user opens Settings →
Importers and can add/edit/delete importers. Each importer has: display name,
report type (enum: IbkrCsv — only option for now), linked taxpayer profile
(dropdown), linked mailbox (dropdown), from-filter (email sender pattern),
subject-filter (email subject pattern), attachment-regex (pattern to match
filenames, e.g. .*\.csv$), and payment notes (free text included in XML
filings). Enrich the Importer domain entity with these fields (currently only
has Id, DisplayName, FilterExpression). Use IImporterRepository. Create CQRS
commands/queries. Add DbSet<Importer> + migration with FK to TaxpayerProfile
and Mailbox. Build Settings sub-view with a form. Validate regex patterns
on save. All async. No actual email processing yet.
```

---

## Tier 1 — Core Engine

These features implement the tax calculation pipeline. They are the heart of
the application and must be rock-solid on correctness.

---

### 006 · NBS Exchange Rate Fetcher

**What it delivers**: The application can fetch daily middle exchange rates
from the National Bank of Serbia (NBS) for a given date and currency. Rates
are cached in SQLite to avoid repeated network calls.

**Layers touched**: Domain (use `ExchangeRate` VO), Application (new
`IExchangeRateFetcher` interface, query handler), Infrastructure (HTTP
scraper impl, EF cache repository impl).

**Depends on**: `001-initial-setup`

**Spec-kit prompt**:
```
Implement NBS exchange rate fetching for Rentier. Create an
IExchangeRateFetcher interface in Application with method
FetchRateAsync(DateOnly date, string currency, CancellationToken) that
returns ExchangeRate. Implement NbsExchangeRateFetcher in Infrastructure
that: (1) checks IExchangeRateCacheRepository first — return cached rate if
found, (2) if not cached, fetches from NBS public website by scraping the
daily exchange rate page for the given date, (3) parses the XML/HTML response
to extract the middle rate for the currency, (4) stores the rate in the cache
via SaveAsync, (5) returns the ExchangeRate value object. Supported NBS
currencies: EUR, USD, GBP, CHF, AUD, CAD, CZK, DKK, HUF, JPY, NOK, PLN,
SEK, TRY, AED. The rate is Middle_Rate / Unit (scaled). Use
System.Net.Http.HttpClient. All amounts decimal, all dates DateOnly. Add
DbSet<ExchangeRate> + migration for the cache table. Include integration
tests with a real NBS call (tagged [Trait("Category","Integration")]) and
unit tests with mocked HTTP responses. Handle rate-not-found with
Result<ExchangeRate, Error> pattern.
```

---

### 007 · IBKR CSV Statement Parser

**What it delivers**: The application can parse an Interactive Brokers CSV
activity statement and extract dividend income, interest income, withholding
tax, and embedded exchange rates.

**Layers touched**: Domain (new `IncomeRecord` VO, `StatementParseResult`),
Application (new `IStatementParser` interface), Infrastructure (CsvHelper
implementation).

**Depends on**: `001-initial-setup`

**Spec-kit prompt**:
```
Implement the IBKR CSV activity statement parser for Rentier. Create an
IStatementParser interface in Application with method
ParseAsync(Stream csvStream, CancellationToken) returning
Result<StatementParseResult, Error>. StatementParseResult is a new domain
type containing: IReadOnlyList<DividendRecord> dividends,
IReadOnlyList<InterestRecord> interest, IReadOnlyList<WithholdingTaxRecord>
withholdings, IReadOnlyList<ExchangeRate> embeddedRates.

Implement IbkrCsvParser in Infrastructure using CsvHelper that:
(1) Parses the "Dividends" section — extracts currency, date (DateOnly),
    paying entity name (strip ISIN), amount (decimal). Aggregates multiple
    dividends from the same entity on the same date.
(2) Parses the "Withholding Tax" section — matches WHT to dividends by
    date + entity. Amounts are negative in CSV — convert to positive.
(3) Parses the "Interest" section — only "Credit Interest" and "Debit
    Interest" rows. Aggregates by currency and date. Paying entity =
    "Interactive Brokers".
(4) Parses "Base Currency Exchange Rate" section — stores currency→USD rates
    for cross-rate calculations.

Edge cases: multiple dividends same entity different dates = separate records;
WHT currency mismatch with dividend = error; interest debit/credit netting.
Use decimal for all amounts. Include comprehensive unit tests with sample CSV
fixtures covering happy path and edge cases.
```

---

### 008 · Tax Calculation Engine

**What it delivers**: Given an income record and its exchange rate, compute
the PP-OPO tax liability in RSD. Handles withholding tax credits, cross-rate
conversion for non-NBS currencies, and the 15% passive income tax rate.

**Layers touched**: Domain (new `TaxCalculation` service, `FilingInfo` VO),
Application (command handler to orchestrate calculation).

**Depends on**: `006-nbs-exchange-rate-fetcher`, `007-ibkr-csv-parser`

**Spec-kit prompt**:
```
Implement the tax calculation engine for Rentier. This is pure domain logic.

Create a TaxCalculationService in Domain that computes PP-OPO tax for a
single income event:

Input: income amount (decimal), income currency (string), income date
(DateOnly), withholding tax amount (decimal), WHT currency (string),
exchange rate provider (Func<DateOnly, string, Task<ExchangeRate>>).

Calculation:
1. Convert gross income to RSD: gross_income_rsd = amount × rate_to_rsd
2. Convert WHT to RSD: wht_rsd = wht_amount × wht_rate_to_rsd
3. Gross tax = gross_income_rsd × 0.15 (Serbian passive income rate)
4. Tax payable = max(gross_tax - wht_rsd, 0)  — WHT credit cannot exceed
   computed Serbian tax

For non-NBS currencies, use cross-rate:
  currency_to_rsd = (currency_to_usd from IBKR statement) × (usd_to_rsd from NBS)

Output: FilingInfo record with: incomeType (dividend/interest), payingEntity,
incomeDate (DateOnly), grossIncomeRsd (decimal), whtPaidRsd (decimal),
grossTaxPayableRsd (decimal), taxPayableRsd (decimal).

All values decimal. All dates DateOnly. 100% test coverage on calculation
logic. Test edge cases: WHT exceeds gross tax (clamp to 0), zero income,
cross-rate calculation, rounding behavior.
```

---

### 009 · Filing Deadline Calculator

**What it delivers**: Given an income date and a holiday configuration,
compute the PP-OPO filing deadline: income date + 30 days, skipping weekends
and Serbian public holidays.

**Layers touched**: Domain only (pure function on `HolidayConf`).

**Depends on**: `003-holiday-configuration`

**Spec-kit prompt**:
```
Implement the filing deadline calculator for Rentier. This is pure domain
logic — no infrastructure dependencies.

Add a method to HolidayConf or create a new DeadlineCalculator domain service:
CalculateDeadline(DateOnly incomeDate, HolidayConf holidays) → DateOnly.

Rules:
1. Start from incomeDate + 30 calendar days
2. If the result falls on a Saturday, advance to Monday
3. If the result falls on a Sunday, advance to Monday
4. If the result falls on a configured public holiday, advance to next day
5. Repeat steps 2–4 until landing on a working day

Edge cases: deadline lands on Friday before a holiday Monday = OK (Friday is
working day); consecutive holidays (e.g., 1–2 Jan + weekend); holiday on
Saturday (no double-skip); 30 days from Feb 28/29 in leap years.

100% test coverage with parametrized xUnit Theory tests. DateOnly only.
No network calls, no I/O. Pure function.
```

---

## Tier 2 — Email Integration & Filing Pipeline

These features connect the dots: fetch emails, parse statements, calculate
taxes, and produce filings.

---

### 010 · IMAP Email Sync

**What it delivers**: The application connects to configured IMAP mailboxes,
searches for statement emails matching importer filters, downloads attachments,
creates Report records, and updates the mailbox cursor.

**Layers touched**: Application (SyncMailboxCommand/Handler), Infrastructure
(MailKit IMAP client), Domain (enrich `Report` with status, attachment info).

**Depends on**: `004-mailbox-configuration`, `005-importer-configuration`

**Spec-kit prompt**:
```
Implement IMAP email sync for Rentier. Create a SyncMailboxCommand and
SyncMailboxCommandHandler in Application that:

1. Loads all configured importers and their linked mailboxes
2. Connects to each mailbox via IMAP/TLS using MailKit (ImapClient)
3. Retrieves credentials from ICredentialStore
4. Searches for emails matching importer filters:
   - FROM filter (sender address)
   - SUBJECT filter (subject line)
   - Date range or UID range from mailbox cursor
5. Downloads attachments matching the importer's attachment regex
6. Creates Report records in the database (IReportRepository) with status
   "init" and stores attachment content
7. Updates the mailbox cursor:
   - First sync: date-based → UID-based transition
   - Subsequent syncs: advance UID cursor
8. Handles errors gracefully: connection failure, auth failure, no matching
   emails — all return Result<T, Error>, cursor not advanced on failure
9. Reports progress via an IProgress<SyncProgress> callback for UI

Unique constraint: (importer_id, report_name) prevents duplicate processing.
All async. Use CancellationToken throughout. Enrich Report domain entity
with: Status (enum: Init, Processed), ReportName, AttachmentContent (byte[]),
MailboxMessageId. Add EF migration. Add unit tests with mocked MailKit and
integration test structure.
```

---

### 011 · Filing Generation Pipeline

**What it delivers**: After sync, unprocessed reports are parsed, tax is
calculated for each income event, and Filing records with computed tax data
are created in the database. This is the orchestration feature that ties
parser + calculator + persistence together.

**Layers touched**: Application (ProcessReportsCommand/Handler), Domain
(enrich `Filing` with tax fields).

**Depends on**: `007-ibkr-csv-parser`, `008-tax-calculation-engine`,
`009-filing-deadline-calculator`, `010-imap-email-sync`

**Spec-kit prompt**:
```
Implement the filing generation pipeline for Rentier. Create a
ProcessReportsCommand and ProcessReportsCommandHandler that:

1. Loads all reports with status "Init" from IReportRepository
2. For each report:
   a. Determines the parser based on importer report type (IbkrCsv)
   b. Parses the attachment via IStatementParser → StatementParseResult
   c. For each income record (dividend or interest):
      - Fetches exchange rate via IExchangeRateFetcher (NBS or cross-rate)
      - Calculates tax via TaxCalculationService
      - Calculates filing deadline via DeadlineCalculator
      - Creates a Filing record with: type (dividend/interest), payingEntity,
        incomeDate, grossIncomeRsd, whtPaidRsd, grossTaxPayableRsd,
        taxPayableRsd, filingDeadline, reportId, taxpayerProfileId
   d. Marks report status as "Processed"
3. Returns summary: count of filings created, any errors

Enrich Filing domain entity with monetary fields (all decimal):
grossIncomeRsd, whtPaidRsd, grossTaxPayableRsd, taxPayableRsd,
filingDeadline (DateOnly), payingEntity (string), incomeType
(dividend/interest enum), reportId (Guid). Add EF migration.

Handle edge cases: parse failure marks report as "Error" not "Processed";
exchange rate not found = filing skipped with error logged; duplicate
income detection (same entity + date + amount).

All decimal. All DateOnly. Unit tests with mocked dependencies. Test the
full orchestration flow.
```

---

## Tier 3 — User-Facing Filing Workflow

These features give the user visibility and control over their filings.

---

### 012 · Filings List & Management

**What it delivers**: The Filings pane shows a paginated DataGrid of all
filings with columns for status, type, paying entity, deadline, tax payable,
and payment reference. The user can filter (unpaid/all), edit status, and
delete filings.

**Layers touched**: Application (queries), Desktop (FilingsViewModel/View).

**Depends on**: `011-filing-generation-pipeline`

**Spec-kit prompt**:
```
Implement the Filings list and management UI for Rentier. Replace the
FilingsView placeholder with a full DataGrid showing all filings.

Columns: Status (Init/Filed/Paid), Income Type (Dividend/Interest), Paying
Entity, Filing Deadline (DateOnly), Tax Payable (decimal, formatted as RSD),
Payment Reference (string, user-entered).

Features:
- Filter toggle: "Unpaid" (Init + Filed) vs "All"
- Pagination (20 items per page)
- Inline status editing via dropdown (enforces valid transitions only —
  Init→Filed→Paid, uses Filing.AdvanceStatus domain logic)
- Payment reference text field (editable when status = Filed)
- Delete action with confirmation dialog (async ContentDialog)
- Sort by deadline (ascending by default)

Create GetFilingsQuery/Handler, UpdateFilingStatusCommand/Handler,
DeleteFilingCommand/Handler in Application. Bind FilingsViewModel with
ReactiveCommand.CreateFromTask for all async operations. Show loading
indicator during queries. Show error messages on failure. All strings
in Strings.resx. No blocking UI operations.
```

---

### 013 · PP-OPO XML Export

**What it delivers**: The user can export a filing as a PP-OPO XML file
compatible with the Serbian ePorezi tax portal. The XML contains all required
taxpayer data, income details, and tax calculations.

**Layers touched**: Domain (XML schema mapping), Application
(ExportFilingCommand/Handler), Infrastructure (XML serializer).

**Depends on**: `012-filings-list`, `002-taxpayer-profile`

**Spec-kit prompt**:
```
Implement PP-OPO XML export for Rentier. The user clicks "Export" on a filing
row and saves a .xml file via a native Save dialog.

Create an IXmlFilingSerializer interface in Application and implement
PpOpoXmlSerializer in Infrastructure using System.Xml.Linq (XDocument).

XML structure (ePorezi PP-OPO format):
- PodaciOPrijavi: VrstaPrijave=1, ObracunskiPeriod (YYYY-MM),
  DatumOstvarivanjaPrihoda, DatumDospelostiObaveze (filing deadline)
- PodaciOPoreskomObvezniku: JMBG, name (CDATA), address (CDATA),
  opština code, phone, email
- PodaciONacinuOstvarivanjaPrihoda: NacinIsplate=3, Ostalo (payment notes)
- DeklarisaniPodaciOVrstamaPrihoda: SifraVrstePrihoda = 111401000 (interest)
  or 111402000 (dividend); BrutoPrihod, OsnovicaZaPorez, ObracunatiPorez,
  PorezPlacenDrugojDrzavi, PorezZaUplatu
- Amounts formatted as "XXXX.YY" (decimal string, 2 decimal places)

Create ExportFilingCommand/Handler that loads Filing + TaxpayerProfile,
serializes to XML, and returns the byte[]. Desktop shows SaveFileDialog.
All async. Unit tests validate XML structure against known-good samples.
```

---

### 014 · Reports List & Manual Import

**What it delivers**: The Reports pane shows a list of all downloaded/imported
reports with their status and linked filings. The user can manually import
a JSON file for income entry without email sync.

**Layers touched**: Application (queries, import command), Desktop
(ReportsViewModel/View).

**Depends on**: `011-filing-generation-pipeline`

**Spec-kit prompt**:
```
Implement the Reports list and manual import UI for Rentier. Replace the
ReportsView placeholder with a DataGrid showing all reports.

Columns: Report Name, Import Date (DateOnly), Importer Name, Status
(Init/Processed/Error), linked Filing count.

Features:
- Manual import via "Import" button: user selects a JSON file from disk
  (native OpenFileDialog). JSON follows NativeIncomeJson format with
  income records and exchange rates. After import, run the filing generation
  pipeline for that report.
- View linked filings (navigate to Filings pane with filter)
- Delete report with confirmation (cascades to filings)

Create GetReportsQuery/Handler, ImportReportCommand/Handler. Bind
ReportsViewModel with ReactiveCommand. Async file dialog. Proper error
handling for invalid JSON. All strings in Strings.resx.
```

---

## Tier 4 — Sync Orchestration & UX Polish

---

### 015 · One-Click Sync Workflow

**What it delivers**: A "Sync" button on the main toolbar triggers the full
pipeline: connect to IMAP → download attachments → parse statements →
calculate taxes → create filings. Progress is shown in real time.

**Layers touched**: Application (orchestration command), Desktop (Sync UI).

**Depends on**: `010-imap-email-sync`, `011-filing-generation-pipeline`

**Spec-kit prompt**:
```
Implement the one-click sync workflow for Rentier. Add a "Sync" button to
the main window toolbar (or a dedicated Sync pane accessible from sidebar).

When clicked:
1. Runs SyncMailboxCommand for all configured importers
2. Then runs ProcessReportsCommand for all new reports
3. Shows real-time progress: "Connecting to mailbox...", "Found N emails",
   "Parsing report X...", "Created N filings", errors

UI:
- Sync button with loading spinner during sync
- Progress log (scrolling TextBlock or ItemsControl)
- Cancel button (CancellationTokenSource)
- Error summary at end
- Auto-navigate to Filings pane on completion

Create a SyncAllCommand/Handler that orchestrates both steps. Use
IProgress<SyncProgress> for UI updates. ReactiveCommand.CreateFromTask
with IsExecuting for busy state. All async. No UI blocking.
```

---

### 016 · Dashboard & Filing Deadline Alerts

**What it delivers**: A dashboard / home pane showing summary stats: upcoming
deadlines, unpaid filings count, total tax liability. Visual indicators for
overdue filings.

**Layers touched**: Application (dashboard query), Desktop (new DashboardView).

**Depends on**: `012-filings-list`

**Spec-kit prompt**:
```
Implement a dashboard for Rentier. Add a new "Dashboard" navigation entry
(first position in sidebar, before Filings).

Shows:
- Upcoming deadlines: filings with status Init or Filed, deadline within
  30 days, sorted by deadline ascending
- Overdue filings: deadline < today and status != Paid, highlighted in red
- Summary cards: total unpaid tax (decimal, RSD), count of filings by
  status (Init/Filed/Paid)
- Last sync timestamp

Create GetDashboardQuery/Handler returning a DashboardDto. Bind to
DashboardViewModel. Use DataGrid for upcoming deadlines. Use styled
TextBlocks or Cards for summary stats. DateOnly for all dates. Decimal
for all monetary display. All strings in Strings.resx. Responsive layout.
```

---

## Feature Dependency Graph

```
001-initial-setup (DONE)
 ├── 002-taxpayer-profile ──────────────────┐
 ├── 003-holiday-configuration              │
 │    └── 009-filing-deadline-calculator     │
 ├── 004-mailbox-configuration              │
 │    ├── 005-importer-configuration ◄──────┘
 │    └── 010-imap-email-sync ◄── 005
 ├── 006-nbs-exchange-rate-fetcher          │
 │    └── 008-tax-calculation ◄── 007       │
 ├── 007-ibkr-csv-parser                    │
 │    └── 008-tax-calculation               │
 │         └── 011-filing-pipeline ◄── 009, 010
 │              ├── 012-filings-list
 │              │    ├── 013-xml-export ◄── 002
 │              │    └── 016-dashboard
 │              └── 014-reports-list
 └── 015-one-click-sync ◄── 010, 011
```

---

## Parallel Development Lanes

| Lane | Features (in order) | Theme |
|------|---------------------|-------|
| **A — Settings** | 002 → 003 → 004 → 005 | Configuration & persistence |
| **B — Engine** | 006 → 007 → 008 → 009 | Calculation & parsing (pure logic) |
| **C — Integration** | 010 → 011 (needs A+B) | Email sync + filing pipeline |
| **D — UI** | 012 → 013 → 014 → 015 → 016 (needs C) | User-facing screens |

Lanes A and B can run in parallel from day one. Lane C starts when both
converge. Lane D follows C.

---

## Assumptions & Open Questions

### Assumptions
- A-001: Only IBKR CSV format is supported initially. Other brokers are future work.
- A-002: Single user, single machine. No multi-user or cloud sync.
- A-003: Windows is the primary platform. macOS support is build-only for now.
- A-004: The ePorezi XML schema is stable. Schema version changes would need a new feature.
- A-005: NBS website structure for exchange rate scraping is stable.
- A-006: Filing status transitions are user-initiated (manual mark as Filed/Paid).

### Open Questions
- OQ-001: Should we support multiple taxpayer profiles (e.g., for tax advisors filing on behalf of clients)?
- OQ-002: What happens when NBS doesn't publish rates for a date (weekends, holidays)? Use previous business day?
- OQ-003: Should filing export support batch XML (multiple filings in one file) or one-file-per-filing?
- OQ-004: Do we need a "dry run" mode for sync that shows what would be imported without creating records?
- OQ-005: Should the dashboard show historical trends (tax paid per month/quarter)?
- OQ-006: Is there a need for report deduplication beyond (importer_id, report_name) uniqueness?
