# UI Contract: Manual Filing Form

**Feature**: 034-manual-filing-creation
**Date**: 2025-07-22

---

## Screen: ManualFilingView

**Entry Point**: "New Filing" button (plus icon) on the Filings toolbar
**Exit Points**: Save (navigates to Filings list with filter=All) | Cancel (navigates back)

---

### Input Fields

| Field | Control Type | Binding | Default | Validation |
|-------|-------------|---------|---------|------------|
| Income Type | ToggleButton/RadioButton group | `SelectedIncomeType` | Dividend | Required |
| Ticker | TextBox | `Ticker` | Empty | Non-blank |
| Income Date | DatePicker | `IncomeDate` | None (unselected) | Required |
| Currency | ComboBox | `SelectedCurrency` | "USD" | Required, from fixed list |
| Gross Amount | TextBox (numeric) | `GrossAmountText` | Empty | > 0, decimal |
| Net Received | TextBox (numeric) | `NetReceivedText` | Empty (optional) | ≤ Gross, ≥ 0 |

**Currency Dropdown Values**: USD, EUR, GBP, CHF, AUD, CAD, CZK, DKK, HUF, JPY, NOK, PLN, SEK, TRY, AED

---

### Action Buttons

| Button | Command | Enabled When | Behaviour |
|--------|---------|-------------|-----------|
| Calculate | `CalculateCommand` | All required fields valid, not loading | Validates → fetches rate → computes tax → shows preview |
| Save Filing | `SaveCommand` | Preview is shown, not loading | Persists filing → navigates to Filings list (filter=All) |
| Cancel / Back | `CancelCommand` | Always | Navigates back without persisting |

---

### Preview Panel

**Visibility**: Hidden until `CalculateCommand` succeeds (bound to `Preview != null`)
**Cleared**: When any input field changes after calculation

| Field | Format | Binding Path |
|-------|--------|-------------|
| Gross Income | `N,NNN.NN RSD` | `Preview.GrossIncomeRsd` |
| WHT Paid | `N,NNN.NN RSD` | `Preview.WhtPaidRsd` |
| Gross Tax Payable | `N,NNN.NN RSD` | `Preview.GrossTaxPayableRsd` |
| Tax Payable | `N,NNN.NN RSD` | `Preview.TaxPayableRsd` |
| Filing Deadline | `yyyy-MM-dd` | `Preview.FilingDeadline` |
| Exchange Rate | `N.NNNN {Currency}/RSD` | `Preview.ExchangeRateValue` |
| Rate Source | `(Exact)` or `(Fallback from {date})` | `Preview.ExchangeRateSourceType + SourceDate` |

---

### Error Display

| Error Scope | Display Location | Pattern |
|-------------|-----------------|---------|
| Field validation (blank ticker, zero gross, etc.) | Inline below the field OR form-level error banner | Same as FilingsView error banner pattern |
| Rate fetch failure | Form-level error banner | `ErrorMessage` property, red text + dismiss button |
| Duplicate filing | Form-level error banner | `ErrorMessage` property |
| Missing taxpayer profile | Form-level error banner on activation | `ErrorMessage` property |

---

### Loading Indicator

**Visibility**: Bound to `IsLoading`
**Control**: `ProgressBar IsIndeterminate="True"` (same as FilingsView pattern)
**During Loading**: All input fields and buttons disabled except Cancel

---

### Localization Keys (Resources/Strings.resx)

| Key | Value |
|-----|-------|
| `ManualFiling_Title` | "New Filing" |
| `ManualFiling_IncomeType_Label` | "Income Type" |
| `ManualFiling_Ticker_Label` | "Ticker / Asset" |
| `ManualFiling_IncomeDate_Label` | "Income Date" |
| `ManualFiling_Currency_Label` | "Currency" |
| `ManualFiling_GrossAmount_Label` | "Gross Amount" |
| `ManualFiling_NetReceived_Label` | "Net Received (optional)" |
| `ManualFiling_Calculate_Button` | "Calculate" |
| `ManualFiling_Save_Button` | "Save Filing" |
| `ManualFiling_Cancel_Button` | "Cancel" |
| `ManualFiling_Preview_Title` | "Filing Preview" |
| `ManualFiling_Preview_GrossIncome` | "Gross Income" |
| `ManualFiling_Preview_WhtPaid` | "WHT Paid" |
| `ManualFiling_Preview_GrossTax` | "Gross Tax Payable" |
| `ManualFiling_Preview_TaxPayable` | "Tax Payable" |
| `ManualFiling_Preview_Deadline` | "Filing Deadline" |
| `ManualFiling_Preview_ExchangeRate` | "Exchange Rate" |
| `ManualFiling_Preview_RateSource` | "Rate Source" |
| `ManualFiling_Error_TickerRequired` | "Ticker is required" |
| `ManualFiling_Error_GrossRequired` | "Gross amount must be greater than zero" |
| `ManualFiling_Error_DateRequired` | "Income date is required" |
| `ManualFiling_Error_NetExceedsGross` | "Net received cannot exceed gross amount" |
| `ManualFiling_Error_RateNotFound` | "Exchange rate not available for {0} on {1}" |
| `ManualFiling_Error_DuplicateFiling` | "A filing with the same details already exists" |
| `ManualFiling_Error_NoProfile` | "Please configure your taxpayer profile in Settings before creating a filing" |
| `ManualFiling_Error_NetworkFailure` | "Could not reach NBS exchange rate service. Please check your connection." |
| `ManualFiling_IncomeType_Dividend` | "Dividend" |
| `ManualFiling_IncomeType_Interest` | "Interest" |
| `ManualFiling_RateSource_Exact` | "Exact ({0})" |
| `ManualFiling_RateSource_Fallback` | "Fallback from {0}" |

---

### Navigation Wiring

```text
MainWindowViewModel
  └── FilingsViewModel (existing)
        ├── receives: Action navigateToManualFiling
        ├── "New Filing" toolbar button → navigateToManualFiling()
        └── ManualFilingViewModel
              ├── receives: Action navigateBackToFilings
              ├── Save success → navigateBackToFilings()
              └── Cancel → navigateBackToFilings()
```

**DI Registration** (CompositionRoot.cs additions):
- `services.AddTransient<ManualFilingViewModel>();` (with navigation delegate factory)
- Handler: `services.AddTransient<ICommandHandler<CalculateManualFilingCommand, ...>, CalculateManualFilingCommandHandler>();`
- Handler: `services.AddTransient<ICommandHandler<CreateManualFilingCommand, ...>, CreateManualFilingCommandHandler>();`
