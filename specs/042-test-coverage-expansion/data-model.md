# Data Model: Test Coverage Expansion

**Feature**: 042-test-coverage-expansion  
**Date**: 2025-07-15

> This feature adds only test code — no new entities or schema changes. This document catalogs the existing domain entities and DTOs exercised by the new tests, their relevant fields, validation rules, and state transitions.

## Entities Under Test

### Filing (Aggregate Root)

**Location**: `src/Rentier.Domain/Entities/Filing.cs`

| Field | Type | Validation | Tested By |
|-------|------|------------|-----------|
| `Id` | `Guid` | Auto-generated | — |
| `Status` | `FilingStatus` | State machine: Init→Filed→Paid only | Property: status transitions |
| `PaymentReference` | `string?` | Max 200 chars after trim; whitespace→null | Property: round-trip + length |
| `IncomeDate` | `DateOnly` | Required | Property: deadline calculation |
| `FilingDeadline` | `DateOnly` | Calculated: incomeDate + 30d, skip weekends/holidays | Property: weekday/holiday avoidance |
| `GrossIncomeRsd` | `decimal` | ≥0, 2 decimal places | Property: rounding invariant |
| `WhtPaidRsd` | `decimal` | ≥0 | Property: sum consistency |
| `GrossTaxPayableRsd` | `decimal` | ≥0 | Snapshot: XML output |
| `TaxPayableRsd` | `decimal` | ≥0 | Snapshot: XML output |
| `IncomeType` | `IncomeType` | Dividend or Interest | Snapshot: sifra mapping |

**State Machine**:

```text
Init ──(AdvanceStatus(Filed))──► Filed ──(AdvanceStatus(Paid))──► Paid
  │                                │                                │
  └── any other transition ────────┴── any other transition ────────┘
                        throws DomainException
```

Valid transitions: `{(Init,Filed), (Filed,Paid)}` — 2 of 9 possible pairs.  
Invalid transitions: all other 7 pairs (including self-transitions) throw `DomainException`.

### ExchangeRate (Value Object)

**Location**: `src/Rentier.Domain/ValueObjects/ExchangeRate.cs`

| Field | Type | Validation |
|-------|------|------------|
| `Date` | `DateOnly` | Required |
| `Currency` | `string` | Not null/empty; normalized to uppercase |
| `RateToRsd` | `decimal` | Must be > 0 |

**Conversion**: `amount * RateToRsd → amountInRsd`, rounded to 2 decimal places.

### HolidayConf (Value Object)

**Location**: `src/Rentier.Domain/ValueObjects/HolidayConf.cs`

| Field | Type | Validation |
|-------|------|------------|
| `Holidays` | `IReadOnlyList<DateOnly>` | Not null |

Internal `HashSet<DateOnly>` for O(1) `ContainsHoliday()` lookup.

### Money (Value Object)

**Location**: `src/Rentier.Domain/ValueObjects/Money.cs`

| Field | Type | Validation |
|-------|------|------------|
| `Amount` | `decimal` | ≥0 (non-negative) |
| `Currency` | `string` | Not null/empty; normalized to uppercase |

### FilingStatus (Enum)

**Location**: `src/Rentier.Domain/Enums/FilingStatus.cs`

```
Init = 0, Filed = 1, Paid = 2
```

## DTOs Under Test

### Pagination DTOs

**FilingsPageResult** (`src/Rentier.Application/DTOs/FilingsPageResult.cs`):
- `Rows: IReadOnlyList<FilingRowDto>`, `TotalCount: int`, `TotalPages: int`
- TotalPages = `max(1, ceil(TotalCount / PageSize))`
- Skip = `(Page - 1) * PageSize`

**ReportsPageResult** (`src/Rentier.Application/DTOs/ReportsPageResult.cs`):
- `Rows: IReadOnlyList<ReportRowDto>`, `TotalCount: int`, `TotalPages: int`

### Dashboard Aggregation DTOs

**DashboardDto** (`src/Rentier.Application/DTOs/DashboardDto.cs`):
- `InitCount: int`, `FiledCount: int`, `PaidCount: int`, `TotalUnpaidRsd: decimal`

### ViewModel Input DTOs

**HolidayEntryDto** (`src/Rentier.Application/DTOs/HolidayEntryDto.cs`):
- `record HolidayEntryDto(DateOnly Date, string Name)`

**SyncProgressEntry** (`src/Rentier.Application/DTOs/SyncProgressEntry.cs`):
- `record SyncProgressEntry(DateTimeOffset Timestamp, string Message, SyncProgressSeverity Severity)`
- Severity enum: `Info, Warning, Error, CursorTransition, DuplicateHandled`

**ImporterDto** (`src/Rentier.Application/DTOs/ImporterDto.cs`):
- `record ImporterDto(Guid Id, string DisplayName, ReportType ReportType, ...)`

### Parser Output DTOs

**StatementParseResult** (`src/Rentier.Application/Parsing/StatementParseResult.cs`):
- `Dividends: IReadOnlyList<DividendRecord>`
- `Interest: IReadOnlyList<InterestRecord>`
- `Withholdings: IReadOnlyList<WithholdingTaxRecord>`
- `ExchangeRates: IReadOnlyList<IbkrExchangeRate>`
- `Errors: IReadOnlyList<ParseError>`

## Domain Services Under Test

### FilingDeadlineCalculator

**Location**: `src/Rentier.Domain/Services/FilingDeadlineCalculator.cs`

```
CalculateDeadline(DateOnly incomeDate, HolidayConf holidays) → DateOnly
```

Algorithm: `incomeDate + 30 days` → advance past Saturday (+2), Sunday (+1), or holiday (+1) until working day found. Max iterations guard prevents infinite loop.

### TaxCalculationService

**Location**: `src/Rentier.Domain/Services/TaxCalculationService.cs`

Already covered by existing property tests. New tests extend coverage to exchange-rate rounding edge cases (extremely small/large rates).

## ViewModels Under Test

### HolidayEntryViewModel

**Location**: `src/Rentier.Desktop/ViewModels/HolidayEntryViewModel.cs`

| Property | Type | Behavior |
|----------|------|----------|
| `Date` | `DateOnly` | Get/set with `RaiseAndSetIfChanged` |
| `Name` | `string` | Get/set with `RaiseAndSetIfChanged`, default `""` |

Methods: `static FromDto(HolidayEntryDto) → HolidayEntryViewModel`, `ToDto() → HolidayEntryDto`

### SyncProgressEntryViewModel

**Location**: `src/Rentier.Desktop/ViewModels/SyncProgressEntryViewModel.cs`

| Property | Type | Derivation |
|----------|------|------------|
| `Icon` | `string` | Error→"✕", Warning→"⚠", default→"•" |
| `Message` | `string` | Pass-through from entry |
| `Timestamp` | `string` | Formatted as `HH:mm:ss` |
| `Severity` | `SyncProgressSeverity` | Pass-through from entry |

Constructor: `SyncProgressEntryViewModel(SyncProgressEntry entry)`

### ImporterItemViewModel

**Location**: `src/Rentier.Desktop/ViewModels/ImporterItemViewModel.cs`

| Property | Type | Derivation |
|----------|------|------------|
| `Id` | `Guid` | Pass-through from DTO |
| `DisplayName` | `string` | Pass-through from DTO |
| `ReportTypeDisplay` | `string` | `dto.ReportType.ToDisplayString()` |
| `Dto` | `ImporterDto` (internal) | Pass-through |

Factory: `static From(ImporterDto) → ImporterItemViewModel` (private constructor)
