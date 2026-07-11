---
name: rentier-tax-rules
description: >
  Serbian PP-OPO tax domain rules exactly as Rentier implements them: the 15% tax
  computation with per-step rounding, the foreign withholding-credit cap, NBS
  exchange-rate application, the +30-day filing deadline shifted past weekends and
  Serbian holidays, and the Init→Filed→Paid lifecycle. Make sure to use this skill
  whenever a task touches tax computation, exchange rates, filing deadlines,
  withholding, how holidays affect deadline math, filing status transitions, IBKR
  income parsing, or PP-OPO XML export — even indirectly ("this amount looks wrong",
  "why is the deadline a Monday?", "add a field to the export") — and before
  answering any question about how Rentier calculates tax. Does NOT apply to
  Holiday settings page UI/CRUD bugs (adding, editing, or deleting holiday entries)
  that don't touch the deadline calculation itself. Financial correctness is the
  project's top priority: treat every change in these areas as safety-critical.
---

# Serbian PP-OPO Tax Rules (as implemented)

This skill states the tax rules **as the code implements them**, so you can change
tax-adjacent code without re-deriving the rules — and so you notice when a change
would silently alter a filed amount. If code and this skill disagree, the code is the
source of truth: figure out which one is wrong, fix it, and keep them in sync.
User-facing documentation lives in `TAX-OVERVIEW.md`; update it whenever observable
behavior changes.

## Key files

| Concern | File |
|---|---|
| Tax computation | `src/Rentier.Domain/Services/TaxCalculationService.cs` |
| Deadline calculation | `src/Rentier.Domain/Services/FilingDeadlineCalculator.cs` |
| Holiday calendar | `src/Rentier.Domain/ValueObjects/HolidayConf.cs`, `Entities/PublicHoliday.cs` |
| Filing lifecycle | `src/Rentier.Domain/Entities/Filing.cs`, `Enums/FilingStatus.cs` |
| Computation result | `src/Rentier.Domain/ValueObjects/FilingInfo.cs` |
| NBS exchange rates | `src/Rentier.Infrastructure/ExchangeRates/NbsExchangeRateFetcher.cs`, `NbsWebScraper.cs` |
| IBKR CSV parsing | `src/Rentier.Infrastructure/Parsing/IbkrCsvParser.cs` |
| PP-OPO XML export | `src/Rentier.Infrastructure/Serialization/PpOpoXmlSerializer.cs` |
| User documentation | `TAX-OVERVIEW.md` |

## The computation (order and rounding are load-bearing)

`TaxCalculationService.CalculateAsync` computes, in this exact order:

```
1. grossIncomeRsd      = Round2(incomeAmount × incomeRate.RateToRsd)
2. whtPaidRsd          = Round2(whtAmount × whtRate.RateToRsd)     // 0 when no WHT
3. grossTaxPayableRsd  = Round2(grossIncomeRsd × 0.15m)
4. taxPayableRsd       = Max(grossTaxPayableRsd − whtPaidRsd, 0m)
```

where `Round2(v) = Math.Round(v, 2, MidpointRounding.AwayFromZero)`.

Rules that must never be "simplified" away — the exported XML values go on legal tax
filings, so a fraction of a dinar of drift is a wrong filing, not a cosmetic diff:

- **Each step rounds before the next.** Do not refactor into one unrounded
  expression; the filed amounts are defined on the rounded intermediates.
- **The withholding credit is capped**: `taxPayableRsd` floors at `0m`. Foreign WHT
  at or above 15% means zero Serbian tax due — never a negative amount or a refund.
- **WHT currency must equal income currency**, otherwise `DomainException`.
- Inputs are validated: non-negative amounts, non-empty paying entity and currencies.
  A missing exchange rate is a `DomainException` — never a guessed or interpolated rate.
- All amounts are `decimal`, all dates `DateOnly` — no exceptions, ever.

**Worked example** (US dividend under the 10% treaty rate): $100 gross at NBS rate
108.50 → gross income 10,850.00 RSD → gross tax 1,627.50 RSD → WHT $10 = 1,085.00 RSD
→ **tax due 542.50 RSD**.

**Edge case:** WHT at or above 15% → tax due 0.00 RSD, but the filing is still
created and must still be submitted (informational filing obligation).

## Filing deadline

`FilingDeadlineCalculator.CalculateDeadline`:

- Deadline = income date + **30 calendar days**, then advanced to the first **working
  day**: Saturday → +2 days, Sunday → +1, configured holiday → +1, re-checking after
  each step (a shift can land on another holiday or weekend).
- Working day = not Saturday, not Sunday, not in `HolidayConf.Holidays`.
- Gives up after 14 iterations with `DomainException` (guard against a misconfigured
  calendar marking everything as holiday).
- Serbian holidays are **configuration** (Holiday settings page), never hardcoded —
  Orthodox Easter moves every year and is maintained per-year in the calendar.

Examples: income 2024-03-01 → raw 2024-03-31 (Sunday) → **2024-04-01**; income
2024-04-30 → raw 2024-05-30 (holiday) → **2024-05-31**.

## Exchange rates

- The **NBS middle rate for the income date** is authoritative. Rates are fetched
  per-date, cached in SQLite, and a cached date is never refetched.
- For dates without a published rate (weekends, holidays), the most recent prior
  business-day NBS rate applies.
- IBKR statement rates are a **fallback only**, for currencies NBS does not publish.
- No rate available at all → a processing error surfaced to the user. Never
  substitute, guess, or interpolate a rate.

## Income scope

- Only the **"Dividends"** and **"Interest"** IBKR Activity Statement sections
  produce income events; every other section is ignored.
- **Each income event = one separate PP-OPO filing** (20 dividends → 20 filings).
- Debit interest (margin interest paid to IBKR) is imported for completeness but
  **never creates a filing** — it is not taxable income.

## Filing lifecycle

`FilingStatus`: `Init(0) → Filed(1) → Paid(2)`, enforced by `Filing` in the Domain.
Transitions are strictly sequential — no skipping Init → Paid, no going backwards —
and invalid transitions throw `DomainException` (never a silent no-op).

## Change checklist (safety-critical)

Before merging any change in these areas:

1. **Unit tests for every behavioral change**, including: rounding boundaries
   (`.005` midpoints must round away from zero), zero WHT, WHT above the cap,
   currency-mismatch rejection, and deadline chains (raw deadline on Saturday →
   Monday holiday → Tuesday).
2. **Never change the rounding mode, precision, the 15% rate, or the 30-day period
   without explicit user sign-off** — these are legal parameters, not tunables.
3. Keep `TAX-OVERVIEW.md` (user docs) and this skill consistent with the code.
4. Follow `.claude/skills/rentier-unit-tests` for test structure; tax and deadline
   tests are pure Domain tests — no mocks, no I/O.
