# Feature 008 — Analysis

## CRITICAL

### C1 — Zero WHT must skip rate provider call
If `whtAmount == 0`, do NOT call `rateProvider(incomeDate, whtCurrency)`. The WHT currency may be a placeholder (e.g., same as income currency) and the caller may not expect a second call. Skip entirely and set `whtPaidRsd = 0m`.

### C2 — ExchangeRate constructor validation
`new ExchangeRate(date, currency, rateToRsd)` throws `DomainException` if `rateToRsd <= 0`. Tests using the real constructor must use positive rates. Mock via simple Task.FromResult.

## HIGH

### H1 — Currency normalization before rateProvider call
Call `rateProvider(date, currency.ToUpperInvariant())`. The rate provider (NBS fetcher) normalizes to uppercase internally, but being explicit avoids subtle bugs when the caller's lambda does a dictionary lookup.

### H2 — Services directory may not exist
`src/Rentier.Domain/Services/` may not exist yet — check and create.

### H3 — Rounding of taxPayableRsd
`taxPayableRsd = Max(grossTaxPayableRsd - whtPaidRsd, 0m)` — this result is NOT additionally rounded because both operands are already rounded to 2dp and the Max(x,0) operation cannot introduce fractional precision beyond 2dp.

## MEDIUM

### M1 — Test: verify rate provider call count for zero WHT
Use a counter lambda to assert the rate provider is called exactly once when `whtAmount == 0`.

### M2 — DomainException import in test
Tests in `Rentier.Domain.Tests` can use `DomainException` directly — same assembly project reference.

## Confirmations
- `decimal` used for all amounts ✅
- `DateOnly` for all dates ✅
- No Application/Infrastructure imports in Domain ✅
- No EF changes ✅
- `Math.Round(x, 2, MidpointRounding.AwayFromZero)` for rounding ✅
