# Research — 011 Filing Generation Pipeline

All design questions were resolved during the clarification phase (`clarify.md`).
No NEEDS CLARIFICATION items remained at implementation time.

---

## Decision Log

### 1. Cross-Rate Fallback Strategy

**Question**: What happens when NBS does not publish an exchange rate for the income currency (e.g., GBP, CHF)?

**Decision**: Try the target currency directly on NBS. On failure, look for an IBKR embedded rate in `StatementParseResult.EmbeddedRates` where `FromCurrency == currency`. If found, fetch the USD/RSD rate from NBS, then compute: `rateToRsd = ibkrEmbeddedRate.Rate × usdRateToRsd`.

**Rationale**: IBKR activity statements embed a daily FX rate table (base currency USD). This gives us a two-hop path for any currency that is quoted against USD by IBKR but not directly by NBS. The handler already owns the `StatementParseResult` so the embedded rates are naturally available.

**Alternatives considered**:
- Fail hard on missing rate — too aggressive; users with non-USD/EUR holdings would be completely blocked.
- Fall back to a 3rd-party FX API — violates constitution §II (network access restricted to IMAP + NBS).
- Cache and interpolate from nearby dates — introduces complexity and potential inaccuracy.

---

### 2. WHT Matching Strategy

**Question**: How do we match a `WithholdingTaxRecord` to a `DividendRecord`?

**Decision**: Match by `(Date == dividend.Date, EntityName == dividend.EntityName, Currency == dividend.Currency)`. Use `FirstOrDefault`; if no match, `whtAmount = 0`.

**Rationale**: IBKR CSV aligns withholding tax rows with the same date, entity, and currency as the dividend they apply to. A 3-field composite match is sufficient for standard IBKR statements.

**Alternatives considered**:
- Match by date + entity only — risks picking the wrong WHT row when the same entity paid multiple currencies on the same day.
- Match by a dedicated IBKR transaction ID — not reliably present in all IBKR report formats.

---

### 3. Duplicate Detection Key

**Question**: How do we prevent re-creating Filings on re-runs?

**Decision**: `IFilingRepository.ExistsByIncomeAsync(taxpayerProfileId, payingEntity, incomeDate, grossIncomeRsd)`. All four fields must match.

**Rationale**: This 4-field composite is functionally unique for a real-world income event. Two dividends from the same entity on the same day with the same gross RSD amount would be extremely unusual and would indicate a data anomaly worth investigating manually.

**Alternatives considered**:
- IBKR transaction ID as dedup key — not persisted in the Filing entity; would require schema change.
- Hash of all income fields — over-engineering; the 4-field composite is sufficient.

---

### 4. Per-Record vs Per-Report Error Handling

**Question**: If one dividend's exchange rate lookup fails, should the entire report fail?

**Decision**: Isolate errors per record. A failed record is logged into `errors` and `reportHadError` is set to true. The remaining records in the same report continue processing. If `reportHadError`, the report is set to `Error`; otherwise `Processed`.

**Rationale**: Tax statements can contain dozens of income events. Failing the whole report on a single missing rate means zero filings get created, requiring a full re-run after the user resolves the missing rate. Isolating errors allows partial progress and gives the user precise information about which records failed.

**Alternatives considered**:
- Abort entire report on first error — too aggressive; partial processing is more user-friendly.
- Retry with exponential backoff — out of scope for this pipeline; NBS rate availability is a data availability problem, not a transient network error.

---

### 5. HolidayConf Construction

**Question**: How does the handler obtain the holiday list for `FilingDeadlineCalculator`?

**Decision**: Call `IHolidayRepository.GetHolidayConfAsync(ct)` once before the report loop. Map `HolidayConfDto.Holidays.Select(h => h.Date)` to construct `new HolidayConf(dates)`.

**Rationale**: Holiday data doesn't change within a single pipeline run. Loading once avoids repeated DB reads inside the per-dividend loop.

---

### 6. Interest WHT

**Question**: Does interest income have withholding tax in IBKR statements?

**Decision**: IBKR does not withhold tax on interest paid to non-US persons in typical broker accounts. The handler passes `whtAmount = 0` for all interest records. `TaxCalculationService` handles zero WHT correctly (skips the second rate lookup, sets `WhtPaidRsd = 0`).

**Rationale**: Confirmed by IBKR documentation and standard IBKR CSV output: interest WHT rows are not present in standard activity statements.

---

### 7. TaxPeriod vs IncomeDate

**Question**: The `Filing` entity has both `TaxPeriod` and `IncomeDate`. What value does each get?

**Decision**: Both are set to the dividend/interest `Date` field from the parsed record. They are semantically equivalent at creation time. `TaxPeriod` is retained for backward compatibility with existing queries; `IncomeDate` is the canonical PP-OPO-relevant date.

---

### 8. InterestType.Debit Records

**Question**: Should `InterestRecord` entries with `InterestType.Debit` be skipped?

**Decision**: The handler processes all `InterestRecord` entries regardless of `InterestType`. Debit interest (bank charges, margin interest) represents an expense, not income, and its `Amount` is stored positive (sign conveyed by `Type`). Future work should add a pre-filter `where interest.Type == InterestType.Credit` to avoid creating zero or near-zero tax filings for expense records.

**Rationale**: The spec does not call this out explicitly, and the current implementation does not filter. Marked as a known improvement rather than a blocking issue, since `TaxCalculationService` will produce a non-negative `TaxPayableRsd` and the filing will correctly reflect a small (or zero) tax obligation.
