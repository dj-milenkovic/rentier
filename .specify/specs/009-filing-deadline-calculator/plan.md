# Feature 009 — Filing Deadline Calculator: Implementation Plan

## Files to create
| File | Notes |
|---|---|
| `src/Rentier.Domain/Services/FilingDeadlineCalculator.cs` | Static service |
| `tests/Rentier.Domain.Tests/Services/FilingDeadlineCalculatorTests.cs` | Theory tests |

## Implementation

### FilingDeadlineCalculator.cs
```csharp
using Rentier.Domain.Exceptions;
using Rentier.Domain.ValueObjects;

namespace Rentier.Domain.Services;

public static class FilingDeadlineCalculator
{
    private const int MaxIterations = 14;

    public static DateOnly CalculateDeadline(DateOnly incomeDate, HolidayConf holidays)
    {
        if (holidays is null)
            throw new DomainException("HolidayConf must not be null");

        var candidate = incomeDate.AddDays(30);

        for (int i = 0; i < MaxIterations; i++)
        {
            if (candidate.DayOfWeek == DayOfWeek.Saturday)
                candidate = candidate.AddDays(2);
            else if (candidate.DayOfWeek == DayOfWeek.Sunday)
                candidate = candidate.AddDays(1);
            else if (holidays.Holidays.Contains(candidate))
                candidate = candidate.AddDays(1);
            else
                return candidate;
        }

        throw new DomainException(
            $"Could not find a working day within {MaxIterations} days of the deadline — check holiday configuration.");
    }
}
```

## Parametrize test dates
Need concrete dates where +30 hits each day of week:
- +30 = Wednesday: 2024-01-01 (Mon) + 30 = 2024-01-31 (Wed) ✓
- +30 = Saturday: 2024-01-06 (Sat) + 30 = 2024-02-05 (Mon) — nope. Try: 2024-01-13 + 30 = 2024-02-12 (Mon). Actually need +30=Sat. 2024-02-03 (Sat) - 30 = 2024-01-04. Check: 2024-01-04 + 30 = 2024-02-03 (Sat) ✓
- +30 = Sunday: 2024-01-05 + 30 = 2024-02-04 (Sun) ✓  
- +30 = Monday holiday: 2024-01-06 + 30 = 2024-02-05 (Mon), use [2024-02-05] as holiday ✓
- +30 = Sat with that Sat in holidays: 2024-01-04 + 30 = 2024-02-03 (Sat), holiday=[2024-02-03] → still goes Mon 2024-02-05 ✓
- Friday: 2024-01-02 + 30 = 2024-02-01 (Thu)... try 2024-01-05 + 30 = 2024-02-04 (Sun). Try 2024-01-03 + 30 = 2024-02-02 (Fri) ✓
- Consecutive Mon+Tue holidays: 2024-01-06 + 30 = 2024-02-05 (Mon), holidays=[Mon,Tue] → Wed 2024-02-07 ✓
- Sat→Mon→holiday: 2024-01-04 + 30 = 2024-02-03 (Sat) → 2024-02-05 (Mon), holiday=[2024-02-05] → Tue 2024-02-06 ✓
- Leap: 2024-01-29 + 30 = 2024-02-28 (Wed) ✓ (non leap: 2023-01-29 + 30 = 2023-02-28 (Tue))
