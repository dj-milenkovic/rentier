# Research: Manual Filing Creation (034)

**Feature**: 034-manual-filing-creation
**Date**: 2025-07-22

---

## R-001: Filing Creation Orchestration Pattern

**Question**: How does the existing codebase create filings from income events, and can the
manual filing flow reuse the same orchestration?

**Decision**: Reuse the exact same five-step orchestration from `ProcessReportsCommandHandler`
but encapsulated in a new `CreateManualFilingCommandHandler`.

**Rationale**: `ProcessReportsCommandHandler` already demonstrates the proven pattern:
1. Load holidays → convert `HolidayConfDto` to `HolidayConf`
2. Resolve exchange rate via `ExchangeRateResolver.ResolveAsync()` → `RateResolution`
3. Calculate tax via `TaxCalculationService.CalculateAsync()` → `FilingInfo`
4. Check duplicates via `IFilingRepository.ExistsByIncomeAsync()`
5. Calculate deadline via `FilingDeadlineCalculator.CalculateDeadline()`
6. Create filing via `Filing.CreateFromIncome()` with `reportId: null`

The manual handler differs only in that: (a) inputs come from a command record rather than
a parsed CSV, (b) there is no cross-rate fallback (user selects an NBS-supported currency
directly), and (c) `ReportId` is always `null`.

**Alternatives Considered**:
- Extracting a shared "FilingCreationService" from `ProcessReportsCommandHandler`: rejected
  because the batch handler has batch-specific concerns (cross-rate fallback, report status
  transitions, per-event error collection) that would over-complicate a shared abstraction
  for a single-event use case.

---

## R-002: TaxCalculationService Rate Provider Callback

**Question**: `TaxCalculationService.CalculateAsync()` takes a
`Func<DateOnly, string, Task<ExchangeRate>> rateProvider` callback. How should the manual
filing handler supply this?

**Decision**: The handler first resolves the rate via `ExchangeRateResolver.ResolveAsync()`,
then passes a closure `(_, _) => Task.FromResult(resolution.Rate)` to the tax calculation
service. This is the same pattern used in `ProcessReportsCommandHandler` (lines 157–160).

**Rationale**: The rate is already resolved and cached in the `RateResolution` object. The
callback is a domain-layer extension point; the handler pre-resolves the rate and provides
it as a constant.

**Alternatives Considered**:
- Passing the resolver directly as the callback: rejected because `ExchangeRateResolver`
  returns `Result<RateResolution, Error>`, not `ExchangeRate`, so it doesn't match the
  callback signature.

---

## R-003: HolidayConf Conversion from HolidayConfDto

**Question**: How do we obtain a `HolidayConf` domain value object from the repository?

**Decision**: Call `IHolidayRepository.GetHolidayConfAsync()` which returns `HolidayConfDto`,
then convert: `new HolidayConf(dto.Holidays.Select(h => h.Date).ToList())`.

**Rationale**: This is the established pattern from `ProcessReportsCommandHandler` (line 49).
The DTO carries holiday names which are not needed for deadline calculation; only dates are
extracted.

**Alternatives Considered**: None — this is the only existing pattern and it works correctly.

---

## R-004: ViewModel Navigation Pattern for Form Views

**Question**: How should the manual filing form integrate with the existing navigation?

**Decision**: Use the existing delegate-based navigation pattern. `MainWindowViewModel`
creates a `ManualFilingViewModel` and passes a navigation delegate. The FilingsViewModel
receives a delegate to navigate to the manual filing form (show ManualFilingViewModel),
and ManualFilingViewModel receives a delegate to navigate back to the filings list.

**Rationale**: The app uses a simple delegate pattern via `MainWindowViewModel` where
child ViewModels receive `Action` delegates to trigger navigation. There is no IScreen,
RoutingState, or NavigationService. This is the established pattern (see
`DashboardViewModel` receiving `navigateToDashboardFilings`).

**Alternatives Considered**:
- ReactiveUI RoutingState: rejected because the existing codebase doesn't use it and
  introducing it would require refactoring all existing navigation.
- Showing the form as a dialog/overlay: rejected because the spec says it's a new
  view/panel within the existing Filings screen navigation, consistent with the app's
  single-content-area pattern.

---

## R-005: ViewModel Validation and Command Enablement Pattern

**Question**: How should the manual filing form handle validation and command enablement?

**Decision**: Follow the `ProfileSettingsViewModel` pattern:
- Use `WhenAnyValue()` with multiple properties to create a `canCalculate` observable.
- Calculate command enabled only when all required fields are filled (ticker non-empty,
  gross > 0, date selected, currency selected) and `IsLoading` is false.
- Save command enabled only when `HasPreview` is true (calculation succeeded).
- Any input change after calculation clears the preview and disables Save.
- Validation errors surfaced via a `string? ErrorMessage` reactive property bound to
  an inline error banner (same pattern as FilingsView error banner).

**Rationale**: This is the established form pattern. `ProfileSettingsViewModel` uses
`WhenAnyValue` with 5 properties for its canSave guard, and `IsLoading` for the loading
indicator pattern.

**Alternatives Considered**:
- ReactiveUI validation helpers (`ReactiveValidationHelper`): rejected because no existing
  ViewModel uses them — the project uses manual `WhenAnyValue` guards consistently.

---

## R-006: Supported Currencies List

**Question**: Where is the authoritative list of NBS-supported currencies defined?

**Decision**: The list is defined in `NbsExchangeRateFetcher` as a static `HashSet<string>`:
USD, EUR, GBP, CHF, AUD, CAD, CZK, DKK, HUF, JPY, NOK, PLN, SEK, TRY, AED. The
ViewModel should expose this as a static list for the currency dropdown. The authoritative
source is the `UNSUPPORTED_CURRENCY` error code returned by the fetcher.

**Rationale**: Hardcoding the list in the ViewModel mirrors what the fetcher already
validates. If a currency is added to NBS support, both the fetcher and the ViewModel
dropdown would need updating — this is acceptable for a 15-item list.

**Alternatives Considered**:
- Extracting the list to a shared constant in Domain or Application: this would be cleaner
  but is a cross-cutting refactor that exceeds the scope of this feature. Can be done as
  a follow-up.

---

## R-007: Duplicate Detection with TaxpayerProfileId

**Question**: The duplicate check requires `TaxpayerProfileId`. How does the manual filing
form obtain this?

**Decision**: The handler receives `TaxpayerProfileId` as part of the command. The ViewModel
obtains it from `ITaxpayerProfileRepository.GetAsync()` on activation and includes it in
the command. If no taxpayer profile exists, the Calculate command surfaces an error message
prompting the user to configure their profile first.

**Rationale**: The existing `ProcessReportsCommandHandler` obtains `TaxpayerProfileId` from
the `Importer` entity. For manual filings, the user's profile is the source. The profile
is a prerequisite for any filing creation.

**Alternatives Considered**:
- Passing the profile ID at ViewModel construction time (from DI): rejected because the
  profile might not exist yet at app startup, and the ViewModel should handle the
  missing-profile case gracefully.

---

## R-008: Preview State Management

**Question**: How should the preview panel state be managed when inputs change?

**Decision**: Introduce a `ManualFilingPreviewDto` record holding the six computed fields.
The ViewModel exposes a nullable `Preview` property. On successful calculation, `Preview`
is set. On any input change, `Preview` is set to null. The Save button's `canExecute`
guard observes `Preview != null`. The preview panel's visibility is bound to
`Preview != null`.

**Rationale**: This is a clean reactive pattern. Using a single nullable DTO avoids
managing six individual observable properties. The `WhenAnyValue` for all input fields
can subscribe to clear the preview on any change.

**Alternatives Considered**:
- Using a separate `IsPreviewVisible` bool: adds unnecessary state that could desync
  from the actual preview data.
