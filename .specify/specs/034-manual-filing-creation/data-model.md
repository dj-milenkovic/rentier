# Data Model: Manual Filing Creation (034)

**Feature**: 034-manual-filing-creation
**Date**: 2025-07-22

---

## Entities

This feature creates **no new domain entities**. All domain types already exist. The
following documents the existing entities and value objects used by this feature, plus the
new Application and Desktop types introduced.

---

### Existing Domain Types (read-only reference)

#### Filing (Aggregate Root)

| Field | Type | Constraints |
|-------|------|-------------|
| Id | `Guid` | PK, auto-generated |
| TaxpayerProfileId | `Guid` | FK, required |
| TaxPeriod | `DateOnly` | Computed |
| Status | `FilingStatus` | Init on creation |
| IncomeType | `IncomeType` | Dividend or Interest |
| PayingEntity | `string` | Non-empty, trimmed |
| IncomeDate | `DateOnly` | Required |
| GrossIncomeRsd | `decimal` | ≥ 0 |
| WhtPaidRsd | `decimal` | ≥ 0 |
| GrossTaxPayableRsd | `decimal` | ≥ 0 |
| TaxPayableRsd | `decimal` | ≥ 0 |
| FilingDeadline | `DateOnly` | Computed from IncomeDate + 30 days (adjusted) |
| ReportId | `Guid?` | **null for manual filings** |
| PaymentReference | `string?` | Max 200 chars |
| ExchangeRateSourceDate | `DateOnly?` | Rate provenance |
| ExchangeRateSourceType | `ExchangeRateSourceType?` | Exact or Fallback |

**Factory**: `Filing.CreateFromIncome(...)` — validates payingEntity non-empty, all amounts ≥ 0.

#### ExchangeRate (Value Object)

| Field | Type | Constraints |
|-------|------|-------------|
| Date | `DateOnly` | Required |
| Currency | `string` | Non-empty, uppercased |
| RateToRsd | `decimal` | > 0 |

#### FilingInfo (Value Object / Record)

| Field | Type | Description |
|-------|------|-------------|
| IncomeType | `IncomeType` | Dividend or Interest |
| PayingEntity | `string` | Ticker/entity name |
| IncomeDate | `DateOnly` | Income date |
| GrossIncomeRsd | `decimal` | amount × exchange rate, rounded 2dp |
| WhtPaidRsd | `decimal` | WHT × exchange rate, rounded 2dp |
| GrossTaxPayableRsd | `decimal` | GrossIncomeRsd × 15%, rounded 2dp |
| TaxPayableRsd | `decimal` | max(GrossTaxPayableRsd − WhtPaidRsd, 0), rounded 2dp |

#### HolidayConf (Value Object)

| Field | Type | Constraints |
|-------|------|-------------|
| Holidays | `IReadOnlyList<DateOnly>` | Non-null |

---

### New Application Types

#### CreateManualFilingCommand (Record)

```csharp
public sealed record CreateManualFilingCommand(
    Guid       TaxpayerProfileId,
    IncomeType IncomeType,
    string     Ticker,          // raw input — handler trims + uppercases
    DateOnly   IncomeDate,
    string     Currency,        // NBS-supported currency code
    decimal    GrossAmount,     // in original currency
    decimal?   NetReceived);    // in original currency, null = no WHT
```

| Field | Type | Validation |
|-------|------|------------|
| TaxpayerProfileId | `Guid` | Must not be empty |
| IncomeType | `IncomeType` | Dividend or Interest |
| Ticker | `string` | Must not be blank after trim |
| IncomeDate | `DateOnly` | Must be a valid date (not default) |
| Currency | `string` | Must be an NBS-supported currency |
| GrossAmount | `decimal` | Must be > 0 |
| NetReceived | `decimal?` | If provided, must be ≤ GrossAmount and ≥ 0 |

#### CreateManualFilingResult (Record)

```csharp
public sealed record CreateManualFilingResult(
    Guid      FilingId,
    decimal   GrossIncomeRsd,
    decimal   WhtPaidRsd,
    decimal   GrossTaxPayableRsd,
    decimal   TaxPayableRsd,
    DateOnly  FilingDeadline,
    decimal   ExchangeRate,
    DateOnly  ExchangeRateSourceDate,
    ExchangeRateSourceType ExchangeRateSourceType);
```

**Purpose**: Returned by the handler on success. Contains all computed values needed for
the preview display AND the persisted filing ID. The ViewModel uses this to populate the
preview panel. Saving and previewing are a single operation — the handler creates the filing
atomically and returns the result.

> **Design Decision — Calculate + Save as Single Operation**:
> After research, the plan uses an atomic Calculate+Save approach rather than a two-step
> Calculate-then-Save. The handler validates, resolves the rate, computes tax, checks for
> duplicates, persists the filing, and returns the full result in one call. The "preview"
> the user sees is the result of the already-saved filing. This eliminates stale-rate
> problems (rate could change between calculate and save), removes the need to re-validate
> on save, and matches the ProcessReportsCommandHandler pattern where filing creation is
> atomic. The ViewModel's "Calculate" button actually creates the filing; the "preview"
> confirms what was saved. If the user doesn't like it, they can delete the filing from
> the list (existing delete functionality).
>
> **UPDATE — Reverting to Two-Step Calculate-then-Save**:
> On further analysis, the spec explicitly requires (FR-008): "Save Filing button MUST be
> disabled until a successful calculation has been performed. If any input field changes
> after calculation, Save MUST be re-disabled until a new calculation is triggered."
> This requires two distinct steps: (1) Calculate (preview only, no persistence), and
> (2) Save (persist). The two-step approach is required by the spec.

#### ManualFilingPreviewDto (Record)

```csharp
public sealed record ManualFilingPreviewDto(
    decimal   GrossIncomeRsd,
    decimal   WhtPaidRsd,
    decimal   GrossTaxPayableRsd,
    decimal   TaxPayableRsd,
    DateOnly  FilingDeadline,
    decimal   ExchangeRateValue,
    DateOnly  ExchangeRateSourceDate,
    ExchangeRateSourceType ExchangeRateSourceType);
```

**Purpose**: Lightweight preview DTO returned by the Calculate step (no persistence). Holds
all computed values the user reviews before committing.

#### CalculateManualFilingCommand (Record)

```csharp
public sealed record CalculateManualFilingCommand(
    Guid       TaxpayerProfileId,
    IncomeType IncomeType,
    string     Ticker,
    DateOnly   IncomeDate,
    string     Currency,
    decimal    GrossAmount,
    decimal?   NetReceived);
```

**Purpose**: Input to the Calculate step. Same fields as CreateManualFilingCommand.
The handler validates inputs, resolves the exchange rate, computes tax, computes the
deadline, and returns `ManualFilingPreviewDto` without persisting anything.

---

### New Desktop Types

#### ManualFilingViewModel (ReactiveObject + IActivatableViewModel)

| Property | Type | Description |
|----------|------|-------------|
| SelectedIncomeType | `IncomeType` | Default: Dividend |
| Ticker | `string` | User input, free text |
| IncomeDate | `DateTimeOffset?` | Avalonia DatePicker binding (converted to DateOnly) |
| SelectedCurrency | `string` | Default: "USD" |
| GrossAmountText | `string` | User input text → parsed to decimal |
| NetReceivedText | `string` | User input text → parsed to decimal?, empty = null |
| Preview | `ManualFilingPreviewDto?` | Null until calculation succeeds |
| ErrorMessage | `string?` | Inline error display |
| IsLoading | `bool` | Loading indicator during async ops |
| CalculateCommand | `ReactiveCommand<Unit, Unit>` | Triggers calculation |
| SaveCommand | `ReactiveCommand<Unit, Unit>` | Persists the filing |
| CancelCommand | `ReactiveCommand<Unit, Unit>` | Navigates back |

**Command Guards**:
- `CalculateCommand` canExecute: Ticker non-empty AND GrossAmount parseable > 0 AND
  IncomeDate selected AND NOT IsLoading
- `SaveCommand` canExecute: Preview is not null AND NOT IsLoading
- `CancelCommand`: always enabled

**State Transitions**:
```text
[Empty Form] --(fill fields)--> [Fields Valid, No Preview]
    --(Calculate)--> [Preview Shown, Save Enabled]
        --(Save)--> [Navigate to Filings List]
        --(Change Input)--> [Preview Cleared, Save Disabled]
    --(Cancel)--> [Navigate to Filings List]
```

---

## Relationships

```text
TaxpayerProfile 1──────* Filing
                          │
                          ├── uses ExchangeRate (via TaxCalculationService)
                          ├── uses HolidayConf (via FilingDeadlineCalculator)
                          └── ReportId = null (manual filing)
```

---

## Validation Rules Summary

| Rule | Enforced By | Error Message |
|------|-------------|---------------|
| Ticker not blank | Handler + ViewModel canExecute | "Ticker is required" |
| GrossAmount > 0 | Handler + ViewModel canExecute | "Gross amount must be greater than zero" |
| IncomeDate selected | Handler + ViewModel canExecute | "Income date is required" |
| NetReceived ≤ GrossAmount | Handler | "Net received cannot exceed gross amount" |
| NetReceived ≥ 0 (if provided) | Handler | "Net received cannot be negative" |
| Currency supported by NBS | Handler (implicit via ExchangeRateResolver) | "Currency '...' is not supported" |
| Exchange rate available | ExchangeRateResolver | "No rate found for {currency} on {date}..." |
| No duplicate filing | Handler via ExistsByIncomeAsync | "A filing with the same details already exists" |
| TaxpayerProfile exists | ViewModel on activation | "Please configure your taxpayer profile first" |
