# Data Model — 011 Filing Generation Pipeline

## Entities Modified

### `Filing` (Domain — `Rentier.Domain.Entities.Filing`)

> Aggregate Root. Status machine: Init → Filed → Paid.

| Field | Type | Constraints | Notes |
|---|---|---|---|
| `Id` | `Guid` | PK | `Guid.NewGuid()` in factory |
| `TaxpayerProfileId` | `Guid` | FK → TaxpayerProfiles, ON DELETE CASCADE | Required |
| `TaxPeriod` | `DateOnly` | Required | Set equal to `IncomeDate` at creation |
| `Status` | `FilingStatus` | Required | Init=0, Filed=1, Paid=2 |
| `IncomeType` | `IncomeType` | Required | Dividend=0, Interest=1 |
| `PayingEntity` | `string` | MaxLength 500, Required | Trimmed at construction |
| `IncomeDate` | `DateOnly` | Required | Canonical PP-OPO income date |
| `GrossIncomeRsd` | `decimal` | Precision(18,2), ≥ 0 | Gross income converted to RSD |
| `WhtPaidRsd` | `decimal` | Precision(18,2), ≥ 0 | Withholding tax paid in RSD |
| `GrossTaxPayableRsd` | `decimal` | Precision(18,2), ≥ 0 | GrossIncomeRsd × 15% |
| `TaxPayableRsd` | `decimal` | Precision(18,2), ≥ 0 | max(GrossTax − WHT, 0) |
| `FilingDeadline` | `DateOnly` | Required | IncomeDate + 30 days, adjusted |
| `ReportId` | `Guid?` | FK → Reports, ON DELETE SET NULL | Nullable |

**Factory method**:
```csharp
Filing.CreateFromIncome(
    Guid taxpayerProfileId,
    IncomeType incomeType,
    string payingEntity,          // trimmed; DomainException if blank
    DateOnly incomeDate,
    decimal grossIncomeRsd,       // DomainException if < 0
    decimal whtPaidRsd,           // DomainException if < 0
    decimal grossTaxPayableRsd,   // DomainException if < 0
    decimal taxPayableRsd,        // DomainException if < 0
    DateOnly filingDeadline,
    Guid? reportId = null)
→ Filing
```

**Domain invariants enforced in factory**:
- `payingEntity` must not be null/empty/whitespace
- `grossIncomeRsd`, `whtPaidRsd`, `grossTaxPayableRsd`, `taxPayableRsd` must be ≥ 0

**Status transitions** (enforced by `AdvanceStatus`):
```
Init ──(submit XML)──> Filed ──(confirm payment)──> Paid
All other transitions throw DomainException
```

---

## Value Objects Used

### `FilingInfo` (Domain — `Rentier.Domain.ValueObjects.FilingInfo`)

Intermediate result from `TaxCalculationService.CalculateAsync`. Not persisted; used to populate `Filing.CreateFromIncome`.

| Field | Type |
|---|---|
| `IncomeType` | `IncomeType` |
| `PayingEntity` | `string` |
| `IncomeDate` | `DateOnly` |
| `GrossIncomeRsd` | `decimal` |
| `WhtPaidRsd` | `decimal` |
| `GrossTaxPayableRsd` | `decimal` |
| `TaxPayableRsd` | `decimal` |

### `ExchangeRate` (Domain — `Rentier.Domain.ValueObjects.ExchangeRate`)

```csharp
record ExchangeRate(DateOnly Date, string Currency, decimal RateToRsd)
```

Returned by `IExchangeRateFetcher.FetchRateAsync` and by the cross-rate fallback lambda.

### `HolidayConf` (Domain — `Rentier.Domain.ValueObjects.HolidayConf`)

```csharp
HolidayConf(IReadOnlyList<DateOnly> holidays)
```

Built once per pipeline run from `IHolidayRepository.GetHolidayConfAsync()`.

---

## Parsing Records (Application — `Rentier.Application.Parsing`)

These records are produced by `IStatementParser` and consumed by the handler. They are DTOs and are not persisted.

| Type | Fields |
|---|---|
| `DividendRecord` | `DateOnly Date, string Currency, string EntityName, decimal Amount` |
| `InterestRecord` | `DateOnly Date, string Currency, string EntityName, decimal Amount, InterestType Type` |
| `WithholdingTaxRecord` | `DateOnly Date, string Currency, string EntityName, decimal Amount` |
| `IbkrExchangeRate` | `DateOnly Date, string FromCurrency, string ToCurrency, decimal Rate` |
| `ParseError` | `string Code, string Message, int? RowNumber` |

**WHT matching key**: `(Date, EntityName, Currency)` — all three must match the dividend record.

**Cross-rate key**: `IbkrExchangeRate.FromCurrency == incomeCurrency` (case-insensitive).

---

## Database Schema (EF Migration 0008)

```sql
CREATE TABLE "Filings" (
    "Id"                   TEXT NOT NULL,
    "TaxpayerProfileId"    TEXT NOT NULL,
    "TaxPeriod"            TEXT NOT NULL,
    "Status"               INTEGER NOT NULL,
    "IncomeType"           INTEGER NOT NULL,
    "PayingEntity"         TEXT NOT NULL,
    "IncomeDate"           TEXT NOT NULL,
    "GrossIncomeRsd"       TEXT NOT NULL,   -- SQLite stores decimal as TEXT
    "WhtPaidRsd"           TEXT NOT NULL,
    "GrossTaxPayableRsd"   TEXT NOT NULL,
    "TaxPayableRsd"        TEXT NOT NULL,
    "FilingDeadline"       TEXT NOT NULL,
    "ReportId"             TEXT,
    CONSTRAINT "PK_Filings" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_Filings_TaxpayerProfiles_TaxpayerProfileId"
        FOREIGN KEY ("TaxpayerProfileId") REFERENCES "TaxpayerProfiles" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_Filings_Reports_ReportId"
        FOREIGN KEY ("ReportId") REFERENCES "Reports" ("Id") ON DELETE SET NULL
);
CREATE INDEX "IX_Filings_TaxpayerProfileId" ON "Filings" ("TaxpayerProfileId");
CREATE INDEX "IX_Filings_ReportId" ON "Filings" ("ReportId");
```

> `decimal` columns use `HasPrecision(18, 2)` in EF configuration; stored as TEXT by SQLite provider.

---

## Enums

### `FilingStatus` (Domain)
```csharp
public enum FilingStatus { Init = 0, Filed = 1, Paid = 2 }
```

### `IncomeType` (Domain)
```csharp
public enum IncomeType { Dividend = 0, Interest = 1 }
```

### `ReportStatus` (Domain)
```csharp
public enum ReportStatus { Init = 0, Processed = 1, Error = 2 }
```
