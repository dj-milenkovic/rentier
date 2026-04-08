# Data Model: Filing Creation Reliability Fixes

**Feature**: 019-filing-creation-reliability-fixes  
**Date**: 2025-07-16

## Schema Changes

### 1. New Enum: `ExchangeRateSourceType`

**Location**: `src/Rentier.Domain/Enums/ExchangeRateSourceType.cs`

```csharp
namespace Rentier.Domain.Enums;

/// <summary>
/// Indicates how an exchange rate was resolved for a filing.
/// </summary>
public enum ExchangeRateSourceType
{
    /// <summary>Rate from the exact income date.</summary>
    Exact = 0,

    /// <summary>Rate from a previous business day (weekend/holiday fallback).</summary>
    Fallback = 1
}
```

**Rationale**: Stored on `Filing` to provide auditability. `Exact` means the NBS rate was available for the income date. `Fallback` means a previous business day's rate was used.

---

### 2. Extended Enum: `ReportStatus`

**Location**: `src/Rentier.Domain/Enums/ReportStatus.cs`

```csharp
namespace Rentier.Domain.Enums;

public enum ReportStatus
{
    Init = 0,
    Processed = 1,
    Error = 2,
    PartialError = 3   // NEW: some events succeeded, some failed
}
```

**Migration impact**: Existing records use values 0, 1, 2 stored as integers. Value `3` is additive-only; no existing data is affected. EF Core stores enums as integers by default in SQLite.

---

### 3. Extended Entity: `Filing`

**Location**: `src/Rentier.Domain/Entities/Filing.cs`

**New properties** (added to existing entity):

| Property | Type | Nullable | Default | DB Column |
|---|---|---|---|---|
| `ExchangeRateSourceDate` | `DateOnly?` | Yes | `null` | `ExchangeRateSourceDate` |
| `ExchangeRateSourceType` | `ExchangeRateSourceType?` | Yes | `null` | `ExchangeRateSourceType` |

**Nullable rationale**: Pre-existing filings (created before this feature) will have `null` values. All new filings will have both fields populated.

**Entity changes**:

```csharp
// New properties on Filing
public DateOnly? ExchangeRateSourceDate { get; private set; }
public ExchangeRateSourceType? ExchangeRateSourceType { get; private set; }
```

**Factory method extension** — `CreateFromIncome` gains two new optional parameters:

```csharp
public static Filing CreateFromIncome(
    Guid taxpayerProfileId,
    IncomeType incomeType,
    string payingEntity,
    DateOnly incomeDate,
    decimal grossIncomeRsd,
    decimal whtPaidRsd,
    decimal grossTaxPayableRsd,
    decimal taxPayableRsd,
    DateOnly filingDeadline,
    Guid? reportId = null,
    DateOnly? exchangeRateSourceDate = null,               // NEW
    ExchangeRateSourceType? exchangeRateSourceType = null)  // NEW
```

**Filing configuration update** (`FilingConfiguration.cs`):

```csharp
builder.Property(f => f.ExchangeRateSourceDate).IsRequired(false);
builder.Property(f => f.ExchangeRateSourceType).IsRequired(false);
```

---

### 4. New DTO: `FilingCreationError`

**Location**: `src/Rentier.Application/DTOs/FilingCreationError.cs`

```csharp
namespace Rentier.Application.DTOs;

/// <summary>
/// Structured error record for a single income event that failed during report processing.
/// </summary>
public sealed record FilingCreationError(
    string EntityName,
    DateOnly IncomeDate,
    string Currency,
    decimal Amount,
    string ErrorCode,
    string Message);
```

**Usage**: Replaces unstructured `string` errors in `ProcessReportsResult`.

---

### 5. Updated DTO: `ProcessReportsResult`

**Location**: `src/Rentier.Application/DTOs/ProcessReportsResult.cs`

**Before**:
```csharp
public sealed record ProcessReportsResult(
    int FilingsCreated,
    int ReportsProcessed,
    int ReportsErrored,
    IReadOnlyList<string> Errors);
```

**After**:
```csharp
public sealed record ProcessReportsResult(
    int FilingsCreated,
    int ReportsProcessed,
    int ReportsErrored,
    int ReportsPartialError,                        // NEW
    IReadOnlyList<FilingCreationError> EventErrors); // CHANGED: structured errors
```

---

### 6. New Value Object: `RateResolution`

**Location**: `src/Rentier.Application/DTOs/RateResolution.cs`

```csharp
namespace Rentier.Application.DTOs;

/// <summary>
/// Result of resolving an exchange rate, including the rate and provenance metadata.
/// </summary>
public sealed record RateResolution(
    ExchangeRate Rate,
    DateOnly SourceDate,
    ExchangeRateSourceType SourceType);
```

**Usage**: Returned by `ExchangeRateResolver` to the handler. `SourceDate` is the date whose rate was actually used (may differ from the requested income date). `SourceType` indicates `Exact` or `Fallback`.

---

### 7. EF Migration: `0010_FilingRateProvenance`

**Migration name**: `{timestamp}_0010_FilingRateProvenance`

**Up**:
```sql
ALTER TABLE "Filings" ADD "ExchangeRateSourceDate" TEXT NULL;
ALTER TABLE "Filings" ADD "ExchangeRateSourceType" INTEGER NULL;
```

**Down**:
```sql
-- SQLite does not support DROP COLUMN natively; EF Core handles via table rebuild
ALTER TABLE "Filings" DROP COLUMN "ExchangeRateSourceDate";
ALTER TABLE "Filings" DROP COLUMN "ExchangeRateSourceType";
```

**Notes**:
- SQLite stores `DateOnly` as `TEXT` (ISO 8601 format)
- SQLite stores enums as `INTEGER`
- Both columns are nullable — no data migration needed
- Migration number `0010` follows the existing sequence (last is `0009_FilingPaymentReference`)

---

## Entity Relationship Summary

```text
Report (1) ──── (*) Filing
  │                    │
  │ Status:            │ New fields:
  │  Init              │  ExchangeRateSourceDate (DateOnly?)
  │  Processed         │  ExchangeRateSourceType (Exact/Fallback?)
  │  Error             │
  │  PartialError ←NEW │
  │                    │
  └── ProcessReportsResult
       FilingsCreated: int
       ReportsProcessed: int
       ReportsErrored: int
       ReportsPartialError: int ←NEW
       EventErrors: FilingCreationError[] ←CHANGED
```

## Validation Rules

| Entity | Rule | Source |
|---|---|---|
| `Filing.ExchangeRateSourceDate` | Must be ≤ `IncomeDate` when not null | Domain invariant: fallback dates are always in the past |
| `Filing.ExchangeRateSourceType` | Must be set when `ExchangeRateSourceDate` is set (and vice versa) | Consistency: both or neither |
| `ReportStatus.PartialError` | Set only when `0 < successCount < totalEventCount` | Application logic in handler |
| `FilingCreationError.Amount` | Must be ≥ 0 | Reflects original income event amount |
| `FilingCreationError.ErrorCode` | Must be one of: `RATE_NOT_FOUND`, `UNSUPPORTED_CURRENCY`, `NBS_HTTP_ERROR`, `NBS_PARSE_ERROR`, `NBS_SCRAPE_ERROR`, `DOMAIN_ERROR` | Enumerated error codes |

## State Transitions

### ReportStatus (updated)

```text
Init ──(all events succeed)──────────→ Processed
Init ──(some succeed, some fail)─────→ PartialError  ← NEW
Init ──(all events fail / exception)─→ Error
```

### FilingStatus (unchanged)

```text
Init ──(submit XML)──→ Filed ──(confirm payment)──→ Paid
```
