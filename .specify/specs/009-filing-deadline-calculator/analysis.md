# Feature 009 — Analysis

## CRITICAL

### C1 — Saturday advance: use +2, not +1+1
When `candidate.DayOfWeek == Saturday`, advance by 2 days directly to Monday. Do NOT advance +1 to Sunday then let the loop handle Sunday→Monday — that wastes an iteration and the 14-iteration guard could theoretically fail in a degenerate case. Use `candidate.AddDays(2)` explicitly.

### C2 — Loop-restart semantics
After any advance (Sat, Sun, or holiday), the `for` loop continues to the next iteration, re-evaluating the new candidate from the top (Saturday → Sunday → holiday → return). This is correct and critical for holiday-on-Monday-after-weekend case.

## HIGH

### H1 — Test date arithmetic must be verified
Verify all `[InlineData]` date values before commit:
- `new DateOnly(2024,1,1).AddDays(30)` = `new DateOnly(2024,1,31)` = Wednesday ✓
- `new DateOnly(2024,1,4).AddDays(30)` = `new DateOnly(2024,2,3)` = Saturday ✓
- `new DateOnly(2024,1,5).AddDays(30)` = `new DateOnly(2024,2,4)` = Sunday ✓
- `new DateOnly(2024,1,3).AddDays(30)` = `new DateOnly(2024,2,2)` = Friday ✓
- `new DateOnly(2024,1,6).AddDays(30)` = `new DateOnly(2024,2,5)` = Monday ✓
Run: `Console.WriteLine(new DateOnly(Y,M,D).AddDays(30).DayOfWeek)` to verify.

### H2 — MaxIterations test: must use weekday-only holidays
For the 14-iteration safety guard test, the holiday list must contain 14 consecutive **weekday** days (no Saturdays/Sundays) starting from the initial +30 candidate. If any are weekends, the weekend advance consumes iterations but the holidays are never hit.

### H3 — HolidayConf.Contains uses List<T>.Contains → O(n)
For typical holiday lists (≤50 entries) this is fine. No optimization needed.

## MEDIUM

### M1 — Services directory creation
`src/Rentier.Domain/Services/` — check if it already exists before creating. Feature 008 also creates files there.

### M2 — DayOfWeek.Saturday == 6, DayOfWeek.Sunday == 0 in .NET
The check `candidate.DayOfWeek == DayOfWeek.Saturday` is idiomatic and correct. Do not use integer comparisons.

## Confirmations
- `DateOnly` only — no `DateTime` ✅
- No async — pure synchronous ✅
- No infrastructure imports ✅  
- `HolidayConf` unchanged ✅
- No EF changes ✅
