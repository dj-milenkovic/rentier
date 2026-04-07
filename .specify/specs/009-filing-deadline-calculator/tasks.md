# Feature 009 — Tasks

## T001 — FilingDeadlineCalculator
**File**: `src/Rentier.Domain/Services/FilingDeadlineCalculator.cs`

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
            $"Could not find a working day within {MaxIterations} days of the deadline.");
    }
}
```

## T002 — FilingDeadlineCalculatorTests
**File**: `tests/Rentier.Domain.Tests/Services/FilingDeadlineCalculatorTests.cs`

Use `[Theory][InlineData]` with concrete DateOnly values. Verified date math:
- 2024-01-01 (Mon) + 30 = 2024-01-31 (Wed) — no advance
- 2024-01-04 (Thu) + 30 = 2024-02-03 (Sat) — advance to 2024-02-05 (Mon)
- 2024-01-05 (Fri) + 30 = 2024-02-04 (Sun) — advance to 2024-02-05 (Mon)
- 2024-01-03 (Wed) + 30 = 2024-02-02 (Fri) — no advance (Friday is working day)
- 2024-01-06 (Sat) + 30 = 2024-02-05 (Mon) — no advance (already Monday)
- 2024-01-29 (Mon) + 30 = 2024-02-28 (Wed) — no advance (leap year 2024 has Feb 29)

```csharp
public class FilingDeadlineCalculatorTests
{
    private static HolidayConf NoHolidays => new(Array.Empty<DateOnly>());
    private static HolidayConf WithHolidays(params DateOnly[] dates) => new(dates);

    [Theory]
    [InlineData("2024-01-01", "2024-01-31")] // Wed — no advance
    [InlineData("2024-01-06", "2024-02-05")] // +30=Mon — no advance
    public void CalculateDeadline_WeekdayNoHolidays_ReturnsDirectly(string income, string expected)
    
    [Theory]
    [InlineData("2024-01-04", "2024-02-05")] // +30=Sat → Mon
    public void CalculateDeadline_Saturday_AdvancesToMonday(string income, string expected)

    [Theory]
    [InlineData("2024-01-05", "2024-02-05")] // +30=Sun → Mon
    public void CalculateDeadline_Sunday_AdvancesToMonday(string income, string expected)

    [Fact]
    public void CalculateDeadline_WeekdayHoliday_AdvancesOneDay()
    // 2024-01-01 + 30 = 2024-01-31 (Wed), holiday=[2024-01-31] → 2024-02-01

    [Fact]
    public void CalculateDeadline_HolidayOnSaturday_WeekendRuleWins()
    // +30=Sat, Sat is in holiday list → still advances to Mon (Sat+2), Mon is clean

    [Fact]
    public void CalculateDeadline_FridayWithMondayHoliday_ReturnsFriday()
    // 2024-01-03+30 = 2024-02-02 (Fri), [2024-02-05 Mon] is holiday → return Fri

    [Fact]
    public void CalculateDeadline_ConsecutiveHolidaysMonTue_ReturnsWednesday()
    // +30=Mon, [Mon,Tue] holidays → Wed

    [Fact]
    public void CalculateDeadline_SaturdayThenHolidayMonday_ReturnsTuesday()
    // +30=Sat→Mon, Mon is holiday → Tue

    [Fact]
    public void CalculateDeadline_LeapYearFeb29_ComputesCorrectly()
    // 2024-01-29+30 = 2024-02-28 (Wed) → no advance

    [Fact]
    public void CalculateDeadline_NullHolidays_ThrowsDomainException()

    [Fact]
    public void CalculateDeadline_MaxIterationsExceeded_ThrowsDomainException()
    // build HolidayConf with 14 consecutive weekday holidays
}
```

## T003 — Build + test
```
dotnet build Rentier.slnx -warnaserror --no-incremental
dotnet test Rentier.slnx --filter "Category!=Integration" --no-build
```
