# Feature Specification: Manual Filing Creation

**Feature Branch**: `034-manual-filing-creation`  
**Created**: 2025-07-22  
**Status**: Draft  

---

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Create a Manual Filing with Full Inputs (Priority: P1)

A user who received a dividend or interest payment outside of an imported statement opens the
manual filing form, enters the asset ticker, income type, date, currency, gross amount, and net
received amount. The system fetches the NBS exchange rate, calculates the tax, computes the filing
deadline, and shows a preview. The user reviews the preview and saves the filing.

**Why this priority**: This is the core purpose of the feature — creating a filing without a
statement import. Without this flow, the feature has no value.

**Independent Test**: Can be fully tested by opening the form, filling all fields (e.g., ticker
"AAPL", Dividend, 2025-06-15, USD, gross 100.00, net 85.00), clicking Calculate, verifying the
preview shows correct RSD-converted values and deadline, then clicking Save and confirming the
new filing appears in the Filings list.

**Acceptance Scenarios**:

1. **Given** the user is on the Filings screen, **When** the user clicks the "New Filing" button
   (plus icon) on the toolbar, **Then** the manual filing form appears with all input fields
   empty and ready for input.
2. **Given** the form is displayed with all required fields filled (Income Type = Dividend,
   Ticker = "AAPL", Income Date = 2025-06-15, Currency = USD, Gross Amount = 100.00,
   Net Received = 85.00), **When** the user triggers calculation, **Then** the system fetches
   the NBS exchange rate for 2025-06-15/USD, computes the tax breakdown, computes the filing
   deadline, and displays a preview showing Gross Income (RSD), WHT Paid (RSD), Gross Tax
   Payable (RSD), Tax Payable (RSD), Filing Deadline, and the exchange rate used.
3. **Given** a valid preview is displayed, **When** the user clicks "Save Filing", **Then** the
   filing is persisted with status Init, the ticker is stored as uppercase trimmed, ReportId is
   null, and the user is navigated back to the Filings list with filter set to All so the new
   row is visible.

---

### User Story 2 — Create a Manual Filing Without Withholding (Priority: P1)

A user received income that had no withholding tax deducted (e.g., interest from certain
jurisdictions). The user leaves the Net Received field blank, meaning WHT is treated as zero.
The system calculates accordingly and produces a filing where the full gross tax is payable.

**Why this priority**: Many real-world income events have no withholding. This path must work
from day one since omitting the Net Received field is a first-class input option, not an edge
case.

**Independent Test**: Can be fully tested by entering all required fields except Net Received,
triggering calculation, and confirming WHT Paid = 0 RSD and Tax Payable = Gross Tax Payable
in the preview.

**Acceptance Scenarios**:

1. **Given** the form is displayed with required fields filled and Net Received left blank,
   **When** the user triggers calculation, **Then** WHT is treated as zero and the preview
   shows WHT Paid = 0.00 RSD with Tax Payable equal to Gross Tax Payable.
2. **Given** the preview shows zero WHT, **When** the user clicks "Save Filing", **Then** the
   filing is persisted with WhtPaidRsd = 0.

---

### User Story 3 — Validation Prevents Incomplete or Invalid Submissions (Priority: P2)

A user attempts to calculate or save a filing without providing all required inputs, or with
invalid values. The form displays inline error messages guiding the user to correct the
issues — no exception pop-ups or modal dialogs appear.

**Why this priority**: Validation protects data integrity and provides a smooth user experience.
It prevents persisting corrupt filings and avoids confusing error dialogs.

**Independent Test**: Can be fully tested by submitting the form with various invalid states
(blank ticker, zero gross, future dates, etc.) and confirming that appropriate inline error
messages appear beside each invalid field without any pop-ups.

**Acceptance Scenarios**:

1. **Given** the ticker field is blank, **When** the user triggers calculation, **Then** an
   inline error message "Ticker is required" appears beside the ticker field.
2. **Given** the gross amount is zero or negative, **When** the user triggers calculation,
   **Then** an inline error message "Gross amount must be greater than zero" appears beside
   the gross amount field.
3. **Given** the income date is not selected, **When** the user triggers calculation, **Then**
   an inline error message "Income date is required" appears beside the date field.
4. **Given** all fields are valid but the NBS exchange rate cannot be fetched (network error or
   rate not found for the selected date/currency), **When** the calculation runs, **Then** an
   inline error message describing the rate-fetch failure appears (e.g., "Exchange rate not
   available for USD on 2025-06-15") and no preview is shown.
5. **Given** Net Received is provided and is greater than Gross Amount, **When** the user
   triggers calculation, **Then** an inline error message "Net received cannot exceed gross
   amount" appears.

---

### User Story 4 — Preview Before Committing (Priority: P2)

A user wants to review the calculated tax amounts and filing deadline before persisting the
filing. The preview panel displays all computed values in RSD so the user can verify correctness
before committing.

**Why this priority**: The preview step prevents accidental creation of filings with wrong inputs.
Since manual filings bypass the imported-statement verification path, this explicit review is
the user's primary safety net.

**Independent Test**: Can be fully tested by calculating a filing and confirming the preview shows
all six computed fields (Gross Income RSD, WHT Paid RSD, Gross Tax Payable RSD, Tax Payable RSD,
Filing Deadline, Exchange Rate used) with correctly formatted values before Save is enabled.

**Acceptance Scenarios**:

1. **Given** calculation succeeds, **When** the preview is displayed, **Then** it shows:
   Gross Income (formatted as N,NNN.NN RSD), WHT Paid (N,NNN.NN RSD), Gross Tax Payable
   (N,NNN.NN RSD), Tax Payable (N,NNN.NN RSD), Filing Deadline (yyyy-MM-dd), and the
   exchange rate used (N.NNNN currency/RSD).
2. **Given** no calculation has been performed yet, **When** the form loads, **Then** the
   preview panel is hidden or empty and the "Save Filing" button is disabled.
3. **Given** a preview is displayed and the user changes any input field, **When** the input
   changes, **Then** the preview is cleared and the "Save Filing" button is disabled until
   a new calculation is performed.

---

### User Story 5 — Navigate Back Without Saving (Priority: P3)

A user opens the manual filing form but decides not to create a filing. They navigate away
(back to the Filings list) without saving, and no data is persisted.

**Why this priority**: Standard navigation hygiene. Users must be able to abandon the form
without side effects.

**Independent Test**: Can be fully tested by opening the form, partially filling fields,
navigating back, and confirming no new filing row exists in the list.

**Acceptance Scenarios**:

1. **Given** the form is open with partially filled fields, **When** the user navigates back
   to the Filings list (e.g., clicks a back/cancel button), **Then** no filing is persisted
   and the Filings list is unchanged.

---

### Edge Cases

- What happens when the NBS web service is unreachable? An inline error message is shown
  describing the network failure; the preview is not displayed and Save remains disabled.
- What happens when the exchange rate exists for a prior business day but not the exact income
  date? The system uses the exchange rate resolver's fallback behaviour (previous business day
  within a 10-day window) and the preview shows the actual source date used.
- What happens when the user enters a ticker with mixed case and extra whitespace (e.g.,
  " AaPl  ")? The system trims and uppercases the value before persisting (stored as "AAPL").
- What happens when the user enters a gross amount with more than two decimal places? The
  system accepts the input but rounds to two decimal places during calculation per standard
  rounding rules.
- What happens when the filing deadline falls on a holiday cluster (e.g., New Year period)?
  The deadline calculator advances past all consecutive holidays and weekends to the next
  working day.
- What happens when the same manual filing (same ticker, date, amount) already exists? The
  system checks for duplicate filings using the existing duplicate-check logic (taxpayer +
  paying entity + income date + gross RSD). If a duplicate is found, an error message is
  displayed and the filing is not saved.

---

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST provide a "New Filing" action (plus icon) on the Filings toolbar
  that opens the manual filing creation form.
- **FR-002**: The form MUST include the following input fields: Income Type (Dividend / Interest
  selection), Asset/Ticker (free text), Income Date (date picker, DateOnly), Currency (dropdown
  of NBS-supported currencies: USD, EUR, GBP, CHF, AUD, CAD, CZK, DKK, HUF, JPY, NOK, PLN,
  SEK, TRY, AED), Gross Amount (decimal), and Net Received (decimal, optional).
- **FR-003**: The Income Type field MUST default to Dividend. The Currency field MUST default to
  USD.
- **FR-004**: When Net Received is provided, the system MUST compute WHT as Gross Amount minus
  Net Received (in the original currency, before conversion to RSD). When Net Received is blank,
  WHT MUST be treated as zero.
- **FR-005**: Net Received MUST NOT exceed Gross Amount. The system MUST reject the input with
  an inline error if this constraint is violated.
- **FR-006**: The system MUST provide a "Calculate" action that, when triggered, performs the
  following steps in order: (a) validates all required fields, (b) fetches the NBS exchange rate
  for the selected income date and currency using the exchange rate resolver with fallback logic,
  (c) runs the tax calculation producing gross income RSD, WHT paid RSD, gross tax payable RSD,
  and tax payable RSD, (d) computes the filing deadline from the income date using the holiday
  configuration.
- **FR-007**: Upon successful calculation, the system MUST display a preview panel showing:
  Gross Income (RSD), WHT Paid (RSD), Gross Tax Payable (RSD), Tax Payable (RSD), Filing
  Deadline (date), and the exchange rate used (rate value and source date).
- **FR-008**: The "Save Filing" button MUST be disabled until a successful calculation has been
  performed. If any input field changes after calculation, Save MUST be re-disabled until a
  new calculation is triggered.
- **FR-009**: When the user clicks "Save Filing", the system MUST create a new Filing entity
  with: ReportId = null, PayingEntity = ticker input (uppercase, trimmed), IncomeDate = selected
  date, and all computed RSD values from the calculation step.
- **FR-010**: On successful save, the system MUST navigate the user back to the Filings list
  with the filter set to "All" so the newly created filing is visible.
- **FR-011**: All validation errors (blank ticker, zero/negative gross, missing date, NBS rate
  not found, NBS network error, net exceeds gross, duplicate filing) MUST be surfaced as inline
  error messages adjacent to the relevant field or in a form-level error area — no exception
  pop-ups or modal error dialogs.
- **FR-012**: The ticker input MUST be trimmed of whitespace and converted to uppercase before
  being used in calculations or persistence.
- **FR-013**: The system MUST check for duplicate filings before saving, using the existing
  duplicate-detection logic (taxpayer profile + paying entity + income date + gross income RSD).
  If a duplicate is detected, the save MUST be rejected with an inline error message.
- **FR-014**: The form MUST include a Cancel or Back action that returns to the Filings list
  without persisting any data.
- **FR-015**: All monetary amounts in the preview MUST be formatted as `N,NNN.NN RSD` and all
  dates MUST be formatted as `yyyy-MM-dd`.
- **FR-016**: All user-visible strings (field labels, button labels, error messages, preview
  labels) MUST be stored in resource files, not hardcoded.
- **FR-017**: The form MUST show a loading indicator during the exchange rate fetch and
  calculation, and all form inputs MUST be disabled while the calculation is in progress to
  prevent concurrent modifications.
- **FR-018**: The filing MUST record exchange rate provenance metadata (source date and source
  type: Exact or Fallback) so the user can see whether the rate was from the exact income date
  or a prior business day.

---

### Constitution Alignment *(mandatory)*

- **CA-001 (Architecture)**: This feature spans all four layers.
  - **Domain**: Reuses existing `Filing.CreateFromIncome()` factory, `TaxCalculationService`,
    `FilingDeadlineCalculator`, and `ExchangeRate` value object. No new domain entities needed.
  - **Application**: Adds `CreateManualFilingCommand` record and
    `CreateManualFilingCommandHandler` implementing `ICommandHandler`. The handler orchestrates
    exchange rate resolution, tax calculation, deadline computation, duplicate checking, and
    filing persistence. References only Domain and Application-layer interfaces.
  - **Infrastructure**: No new infrastructure code required — the existing `NbsExchangeRateFetcher`,
    `ExchangeRateResolver`, `FilingRepository`, and `HolidayRepository` implementations are
    reused.
  - **Desktop**: Adds `ManualFilingViewModel` (ReactiveObject + IActivatableViewModel) and
    `ManualFilingView` (ReactiveUserControl). The ViewModel calls the Application-layer command
    handler only.
  Clean Architecture dependency rules remain intact — Desktop → Application → Domain; no upward
  references.
- **CA-002 (Money and Dates)**: All monetary fields (Gross Amount, Net Received, WHT, all RSD
  computed values, exchange rate) use `decimal`. Income Date and Filing Deadline use `DateOnly`.
  Preview formatting uses explicit format strings (`N,NNN.NN RSD` and `yyyy-MM-dd`). No `float`,
  `double`, or `DateTime` usage permitted.
- **CA-003 (Privacy and Security)**: All data is stored locally in SQLite. No credentials or
  sensitive identifiers are transmitted. The only outbound call is the NBS exchange rate fetch,
  which transmits only a date parameter — no user data.
- **CA-004 (Network Scope)**: This feature makes one outbound call: the NBS exchange rate XML
  web service (`https://webservices.nbs.rs/`). This endpoint is already in the approved list of
  allowed network targets. No new endpoints are introduced.
- **CA-005 (Async and UI)**: The exchange rate fetch, tax calculation, deadline computation, and
  filing persistence are all performed via `async/await`. The ViewModel uses
  `ReactiveCommand.CreateFromTask` for the Calculate and Save commands. The UI thread is never
  blocked. A loading indicator is shown during async operations.
- **CA-006 (Testing Impact)**:
  - **Domain**: No new domain logic; existing `TaxCalculationService` and
    `FilingDeadlineCalculator` tests cover the calculation paths.
  - **Application**: Unit tests for `CreateManualFilingCommandHandler` covering: happy path
    (with and without WHT), validation failures (blank ticker, zero gross, net > gross),
    duplicate detection, exchange rate fetch failure, and deadline computation integration.
  - **Infrastructure**: No new infrastructure code to test.
  - **Desktop**: ViewModel tests for `ManualFilingViewModel` covering: command enablement
    (Calculate enabled only when required fields filled, Save enabled only after successful
    calculation), input clearing resets preview, error display on validation failure, and
    navigation on successful save.

---

### Key Entities

- **Filing** (existing): The aggregate root representing a single PP-OPO tax filing. This
  feature creates filings using the existing `Filing.CreateFromIncome()` factory method with
  `ReportId = null` to distinguish manual filings from statement-imported ones. Key attributes:
  Id (Guid), TaxpayerProfileId (Guid), IncomeType (Dividend/Interest), PayingEntity (string,
  ticker), IncomeDate (DateOnly), GrossIncomeRsd (decimal), WhtPaidRsd (decimal),
  GrossTaxPayableRsd (decimal), TaxPayableRsd (decimal), FilingDeadline (DateOnly),
  ReportId (Guid?, null for manual), ExchangeRateSourceDate (DateOnly?),
  ExchangeRateSourceType (Exact/Fallback).
- **ExchangeRate** (existing): A value object representing an NBS middle exchange rate for a
  specific date and currency. Used to convert foreign-currency amounts to RSD.
- **FilingInfo** (existing): A computed value object returned by the tax calculation service
  containing the RSD-converted tax breakdown for a single income event.
- **HolidayConf** (existing): A value object containing Serbian public holiday dates, used by
  the filing deadline calculator to advance deadlines past non-working days.

---

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users can create a complete manual filing (fill form → calculate → review preview
  → save) in under 60 seconds when the NBS service is available.
- **SC-002**: 100% of validation errors (blank ticker, zero gross, rate not found, net exceeds
  gross, duplicate filing) produce a visible inline error message — no silent failures and no
  exception pop-ups.
- **SC-003**: The calculated tax preview matches the results that would be produced by the
  existing statement-import pipeline for the same income parameters (same tax calculation service,
  same exchange rate resolution, same deadline computation).
- **SC-004**: Users can abandon the form at any point without any data being persisted — zero
  accidental filings created from incomplete form sessions.
- **SC-005**: The filing preview displays all six computed fields (Gross Income RSD, WHT Paid
  RSD, Gross Tax Payable RSD, Tax Payable RSD, Filing Deadline, Exchange Rate) before the user
  can commit, ensuring 100% of saved filings have been explicitly reviewed.
- **SC-006**: The UI remains responsive (no freeze) during the exchange rate fetch and
  calculation; a loading indicator is visible for any operation exceeding 200 ms.
- **SC-007**: Manual filings are indistinguishable from statement-imported filings in the Filings
  list — they appear in the same table, support the same status transitions, and can be exported
  to PP-OPO XML identically.

---

## Assumptions

- Features 006 (NBS Exchange Rate Fetcher), 008 (Tax Calculation Engine), 009 (Filing Deadline
  Calculator), and 012 (Filings List Management) have been completed and their services are
  operational before this feature is implemented.
- The existing `Filing.CreateFromIncome()` factory method accepts a null `ReportId`, which is
  how manual filings are distinguished from statement-imported ones.
- The `IFilingRepository.ExistsByIncomeAsync()` method is available for duplicate detection,
  matching on taxpayer profile, paying entity, income date, and gross income RSD.
- The `ExchangeRateResolver` service is available and handles fallback to previous business days
  when no rate exists for the exact income date.
- A `IHolidayRepository` (or equivalent) provides the `HolidayConf` for the Serbian holiday
  calendar needed by the filing deadline calculator.
- The existing taxpayer profile is loaded at application startup and is available in the
  ViewModel's DI context — the user does not need to select a taxpayer profile when creating a
  manual filing.
- The manual filing form is presented as a new view/panel within the existing Filings screen
  navigation — no new top-level navigation entry is needed.
- Supported NBS currencies for the dropdown are: USD, EUR, GBP, CHF, AUD, CAD, CZK, DKK, HUF,
  JPY, NOK, PLN, SEK, TRY, AED — matching the existing `NbsExchangeRateFetcher` supported
  currency list.
- The exchange rate fetch may take 1–5 seconds when not cached; the loading indicator is
  essential for user feedback during this wait.
- No multi-user or concurrent-access scenarios apply; this is a single-user desktop application.
