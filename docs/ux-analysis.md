# Rentier Desktop — UX/UI Analysis

## Executive Summary

Five critical-to-high pain points across the application:

- **Activation-gap bug**: `ReportsView`, `SyncView`, and all four Settings sub-views are plain `UserControl`, not `ReactiveUserControl<T>`. Their ViewModels' `WhenActivated` blocks — which include the initial `LoadPageCommand.Execute()` data-load — will **never fire** because `IActivatableView` is not implemented on those controls.
- **Raw enum/type bindings on Dashboard**: `DashboardView.axaml` binds `{Binding FilingDeadline}`, `{Binding Status}`, and `{Binding IncomeType}` directly against `UpcomingDeadlineDto` fields with no formatters — the DataGrid renders raw `DateOnly.ToString()` and raw enum names.
- **Sidebar blind spot**: Navigating to `ManualFilingView` sets `CurrentViewModel` directly (bypassing `SelectedEntry`), so the sidebar shows no selected item while the user is creating a filing.
- **Payment reference: silent LostFocus commit**: `SavePaymentRefCommand` fires on tab-out from the TextBox — an uncommonly risky implicit commit gesture for a financial reference number.
- **No success feedback on any destructive or state-changing action**: advancing a filing status, exporting XML, bulk-deleting, or saving a profile all silently succeed (or reload) with no positive confirmation.

---

## Per-Screen Analysis

### 1. MainWindow (`MainWindowViewModel` / `MainWindow.axaml`)

| Finding | Severity | Detail |
|---------|----------|--------|
| Fixed window size | High | `Width="900" Height="600"` with no `MinWidth`/`MinHeight`. Content clips below ~800 wide and at any height below 600. |
| Bare `ListBox` sidebar | Medium | Items have only a `TextBlock` — no icon, no selection indicator beyond the platform highlight, no hover affordance. |
| ManualFiling bypasses `SelectedEntry` | High | `navigateToManualFiling` sets `CurrentViewModel = manualVm` directly. `SelectedEntry` stays on "Filings". The sidebar shows "Filings" selected while the user is on the New Filing form. `ManualFilingView` has no breadcrumb or back-navigation label. |
| No "dirty" navigation guard | Medium | Navigating away from the ManualFiling form (clicking any sidebar item) silently discards a partially filled form. No confirmation prompt. |
| Window title is static "Rentier" | Low | No page/section context in title bar — makes it harder to orient when alt-tabbing between windows. |

**XAML gaps in `MainWindow.axaml`:**
```xml
<!-- Current -->
<Window Width="900" Height="600">

<!-- Recommended -->
<Window Width="900" Height="600" MinWidth="780" MinHeight="520"
        CanResize="True" WindowStartupLocation="CenterScreen">
```

---

### 2. Dashboard (`DashboardViewModel` / `DashboardView.axaml`)

**ViewModel Issues:**

| Property / Method | Issue |
|-------------------|-------|
| `HasOverdueFilings` | Plain computed property with manual `RaisePropertyChanged(nameof(HasOverdueFilings))` at the end of `LoadAsync`. Not a true `ObservableAsPropertyHelper`. If `OverdueFilings` changes from another path it won't notify. |
| `HasData` | Boolean set to `false` on error, `true` on success, but **never read in XAML**. KPI cards render with `0` / `string.Empty` on initial load and on error — no "loading skeleton" or "no data" state. |
| `LastSyncDisplay = "Never"` | Hardcoded string on line 143 of `DashboardViewModel.cs`. `Strings.Dashboard_LastSyncNever` exists in Strings.resx but is unused here. |
| `TotalUnpaidDisplay` format | Uses `CultureInfo.InvariantCulture` → `"1,234.56 RSD"`. Serbian locale uses period as thousands separator, comma as decimal — this will look correct to some users but may be confusing. The same format is used throughout (`FilingRowViewModel.TaxPayableDisplay`). Consider `"N2"` with a Serbian or regional culture, or a consistent Serbian format (`1.234,56 RSD`). |
| No manual refresh | `LoadCommand` fires only on `WhenActivated`. There is no refresh button — users who leave and return to Dashboard get a stale view until they navigate away and back. |

**XAML Issues (`DashboardView.axaml`):**

| Element | Issue |
|---------|-------|
| `Grid.Row="0"` shared by ProgressBar and error TextBlock | Both occupy `Grid.Row="0"`. If `IsLoading` is `true` and `ErrorMessage` is non-null simultaneously, they overlap. The `ProgressBar` should occupy its own row or the error banner should be `DockPanel.Dock="Top"`. |
| `{Binding FilingDeadline}` in UpcomingDeadlines DataGrid | Raw `DateOnly` → calls `DateOnly.ToString()` which returns an ISO-8601 string but with no explicit format. Should use `DeadlineDisplay` (string property) or a `StringFormat`. |
| `{Binding Status}` in UpcomingDeadlines DataGrid | Raw `FilingStatus` enum — renders `"Init"`, `"Filed"`, `"Paid"`. Should use `FilingStatusDisplayConverter.Instance` or a dedicated display property. |
| `{Binding IncomeType}` in UpcomingDeadlines DataGrid | Raw `IncomeType` enum — renders `"Dividend"`, `"Interest"`. Should use `IncomeTypeDisplayConverter.Instance`. |
| `{Binding TaxPayableRsd, StringFormat={}{0:N2}}` in UpcomingDeadlines DataGrid | `N2` uses the current thread culture — unlike `FilingRowViewModel.TaxPayableDisplay` which forces `InvariantCulture`. Inconsistent formatting between Dashboard and Filings. |
| `CanUserSortColumns="True"` on UpcomingDeadlines DataGrid | Client-side sort only (reorders the 30-item page). The Dashboard header says "Upcoming Deadlines (30 days)" — sorting this doesn't change what 30 items are shown, which is misleading. |
| Missing empty/loading states | No `TextBlock` for when `UpcomingDeadlines.Count == 0` (should show "No upcoming deadlines"). No placeholder skeleton when `IsLoading` is true. |
| Missing `AutomationProperties.Name` | `InitCount`, `FiledCount`, `PaidCount` `TextBlock`s have no accessibility labels — a screen reader reads the number but not what it means. |

---

### 3. Filings (`FilingsViewModel` / `FilingsView.axaml`)

**ViewModel Issues:**

| Issue | Detail |
|-------|--------|
| `AdvanceStatusCommand` canExecute is frozen | `FilingRowViewModel.AdvanceStatusCommand` is created with `Observable.Return(HasNextStatus)` — a single-emission cold observable. It will never re-evaluate. Once `HasNextStatus` is evaluated at row construction, the enabled state is frozen forever. For `Paid` rows, the button is disabled permanently; fine in practice (Paid has no transitions) but architecturally wrong. |
| `BulkDeleteCommand` missing canExecute | `BulkDeleteCommand = ReactiveCommand.CreateFromTask(...)` has no `canExecute` parameter. Visibility is controlled by `IsVisible="{Binding HasSelection}"` in XAML, but if a keyboard shortcut or accessibility tool fires the command with no selection, it will enter the async path and return early after a no-op check. Should use `this.WhenAnyValue(x => x.HasSelection)` as canExecute. |
| Payment reference save trigger | `PaymentRef_LostFocus` fires `SavePaymentRefCommand` on any focus loss from the TextBox — including tabbing to the next cell or clicking another row. Financial reference numbers should have an explicit save gesture (Enter key or a dedicated Save button). The equality check (`tb.Text == row.PaymentReference`) prevents redundant writes but the UX signal is still implicit. |
| `Subscribe()` without disposal in code-behind | `FilingsView.axaml.cs` lines 25 and 39: `ViewModel?.SavePaymentRefCommand.Execute(...).Subscribe()` and `ViewModel?.ApplySortCommand.Execute(...).Subscribe()` — subscriptions are created but never disposed. If the command completes synchronously they're moot, but long-running async commands could leak. Use `.DisposeWith()` or `IDisposable` field. |
| No success confirmation on status advance | `AdvanceStatusCommand` reloads the grid silently. Users have no acknowledgment that "Mark as Filed" worked. A toast or temporary status badge change would help. |
| No success confirmation on export | `ExportCommand` silently opens the file picker; on success, nothing. A banner "Export saved to {path}" would close the loop. |

**XAML Issues (`FilingsView.axaml`):**

| Element | Issue |
|---------|-------|
| Empty state `DockPanel.Dock="Top"` | The empty `TextBlock` uses `DockPanel.Dock="Top"` placed before the DataGrid (which fills remaining space). It won't vertically center in the remaining content area — it pins to the top just below the error banner, making it appear too high. |
| ToggleButton filter pair | Two independent `ToggleButton`s for "Unpaid" / "All" bound to `!ShowAll` and `ShowAll`. They look independent, not mutually exclusive. A `RadioButton` group or segmented control would make the exclusive relationship visually clear. |
| Advance-status icon button always visible for Paid rows | The `AdvanceStatusCommand` button renders for every row, disabled for Paid rows. A `Visibility` binding on `HasNextStatus` would reduce visual noise and prevent false affordance. |
| No column header sort indicator | `ApplySortCommand` sorts server-side but there is no sort direction glyph in the column headers. The `SortColumn` and `SortDescending` properties exist on the VM but are not reflected back to any UI element. Users don't know which column is sorted or in which direction. |
| `TrashIcon` resource dependency | `{StaticResource TrashIcon}` referenced in XAML but not defined in the file's own resources — relies on `Icons.axaml` global merge. No fallback. If `Icons.axaml` fails to load, a designer-mode error appears. |
| Action buttons: no text labels | Three icon-only buttons (advance, export, delete) in the Actions column with only tooltip. Tooltips require hover and are invisible to keyboard-only users. At minimum, `AutomationProperties.Name` should be set for screen readers. |
| Payment reference TextBox `LostFocus` uses code-behind | The comment in `FilingsView.axaml.cs` correctly notes this as an accepted exception, but the approach bypasses the MVVM contract — `tb.Text` may not match `row.PaymentReference` due to binding timing. |

---

### 4. Manual Filing (`ManualFilingViewModel` / `ManualFilingView.axaml`)

**ViewModel Issues:**

| Issue | Detail |
|-------|--------|
| `LoadProfileAsync` swallows exceptions silently | Lines 216–219: `catch { // Non-critical }`. If the profile query throws for a network or DB reason, the error is silently discarded. The user sees no indication that the profile couldn't be loaded, and will only discover the problem when they try to Save (at which point `_taxpayerProfileId` is null → `Guid.Empty`, causing a domain error). |
| Duplicated input-parsing logic | `CalculateAsync` and `SaveAsync` both parse `GrossAmountText` and `NetReceivedText` with identical code blocks. A private `ParseInputs()` method would eliminate the duplication and the risk of divergence. |
| `canCalculate` uses `CultureInfo.CurrentCulture` | Line 148: `decimal.TryParse(grossText, NumberStyles.Number, CultureInfo.CurrentCulture, ...)`. If the OS culture uses comma as decimal separator (Serbian `sr-Latn-RS`), users must type `100,00` not `100.00` — but the watermark says `"e.g. 100.00"`. Inconsistency between expected format and actual parsing. |
| No inline field validation | `CalculateCommand.canExecute` silently disables the button when fields are invalid. Users don't know *why* Calculate is grayed out. Each field should show an inline validation hint (e.g., "Ticker is required"). |
| Navigation on Save is immediate | `_navigateBackToFilings()` is called immediately upon save success, with no "Filing saved!" confirmation. |
| No "dirty" back-navigation guard | `CancelCommand` calls `_navigateBackToFilings()` unconditionally. If the user has filled in fields, clicking Cancel silently discards them. |

**XAML Issues (`ManualFilingView.axaml`):**

| Element | Issue |
|---------|-------|
| Button order: `[Calculate] [Save Filing] [Cancel]` | "Save Filing" appears before the user has calculated. It is disabled (`canSave = preview != null`) but its presence alongside Calculate creates confusion about the intended workflow. Consider hiding "Save Filing" until a Preview is available, or reordering to `[Calculate] [Cancel]` → preview appears → `[Save Filing] [Recalculate] [Cancel]`. |
| `ManualFiling_Error_NoProfile` shown on load as error | If no profile is configured, the error banner fires immediately on screen load (before the user has touched anything), which is alarming. This should be an informational callout with a link to Settings. |
| Preview `ExchangeRateSourceDate` label is "Rate Source" | `ManualFiling_Preview_RateSource` says "Rate Source" but binds to `Preview.ExchangeRateSourceDate` (a date) — does not communicate whether this is an exact or fallback rate. `Strings.ManualFiling_RateSource_Exact` and `Strings.ManualFiling_RateSource_Fallback` exist but are unused in the preview panel. |
| No currency symbol next to amount fields | "Gross Amount" field has no visual currency indicator. The Currency ComboBox is three fields above it — users could set currency to EUR and still type an amount without seeing the EUR context. A `Run` inline label (`[€] [________]`) or field suffix would make the relationship explicit. |
| `ScrollViewer` with `MaxWidth="520"` | On a 1920-wide monitor, the form is pinned to 520px on the left. There is no centering. The layout looks abandoned on wide screens. |
| Loading indicator before error banner in DOM | The `ProgressBar IsVisible="{Binding IsLoading}"` appears *above* the error banner. On load, the bar appears, then an error pushes content down — a small layout jump. Consider combining into a single status zone. |

---

### 5. Reports (`ReportsViewModel` / `ReportsView.axaml`)

> ⚠️ **Critical architectural finding**: `ReportsView.axaml` root is `<UserControl>` — not `<reactive:ReactiveUserControl x:TypeArguments="vm:ReportsViewModel">`. `ReactiveUserControl<T>` implements `IActivatableView`, which is required for `IActivatableViewModel.WhenActivated` to fire via Avalonia ReactiveUI's `AvaloniaActivationForViewFetcher`. With a plain `UserControl`, `ReportsViewModel.WhenActivated` **will never be triggered**, meaning `LoadPageCommand.Execute().Subscribe()` (the initial data load) never runs. The Reports grid will be permanently empty on first navigation.

**ViewModel Issues:**

| Issue | Detail |
|-------|--------|
| `SyncCommand.IsExecuting.Subscribe(v => IsSyncing = v)` outside `WhenActivated` | Line 259: This subscription is created in the constructor and is never disposed. It also does not wait for activation. If the VM is discarded (e.g., re-created), the subscription leaks. |
| `ReportsViewModel` has both a `SyncCommand` and the user can navigate to `SyncView` | Two separate sync entry points with different capabilities. The Reports-page sync uses hardcoded `SyncParameters.Default` (line 465: `new SyncMailboxCommand(SyncParameters.Default)`). The dedicated Sync screen offers mode/strategy selection. This is inconsistent — the same action (sync) produces different results depending on where the user triggers it. |
| No sort indicator | `SortDescending` property exists and drives server-side sort, but no visual sort glyph in any column header. `CanUserSortColumns="False"` in AXAML means there's no sort affordance at all. |
| `SyncStatusMessage` only visible during sync | `IsVisible="{Binding IsSyncing}"` — the success/failure message disappears the moment the sync ends (IsSyncing → false). Users get no summary after completion. |

**XAML Issues (`ReportsView.axaml`):**

| Element | Issue |
|---------|-------|
| Missing `ReactiveUserControl` root | See critical note above. |
| `IsHitTestVisible` vs `IsEnabled` on header checkbox | Header CheckBox uses `IsHitTestVisible="{Binding DataContext.HasItems, ...}"` to disable interaction when list is empty. This does not visually indicate the disabled state (no grayed appearance). Use `IsEnabled` instead. |
| `SyncProgressValue` ProgressBar is determinate (0–100) but driven by `HandleSyncAsync` setting it to 0 then 100 only | The ProgressBar will jump from 0 to 100 with no intermediate steps — functionally identical to an indeterminate bar. No real progress reporting. |
| Actions column header missing | The rightmost column (ViewFilings + Delete buttons) has no `Header` attribute — DataGrid renders an empty header cell. |
| `CanUserSortColumns="False"` | Sorting disabled but `SortDescending` property exists. Consider at least a single sort-toggle control (e.g., a ToggleButton "Newest First / Oldest First") so users can control the order. |

---

### 6. Sync (`SyncViewModel` / `SyncView.axaml`)

> ⚠️ **Critical architectural finding**: `SyncView.axaml` root is `<UserControl>` — not `<reactive:ReactiveUserControl>`. `SyncViewModel.WhenActivated` will never fire. The `IsRunning` subscription (`SyncCommand.IsExecuting.Subscribe(v => IsRunning = v)`) and the `ThrownExceptions` error handler are **both inside `WhenActivated`** and will never be wired. Result: `IsRunning` never changes (always `false`), the Cancel button is always disabled, and unhandled sync exceptions are never surfaced.

**ViewModel Issues:**

| Issue | Detail |
|-------|--------|
| `ImpactSummary` strings are hardcoded English | Lines 153–171: all 9 combination strings are hardcoded in C# (`"Fetches new emails since last sync. Duplicates are skipped."` etc.). These should be in `Strings.resx` for consistency and future localization. |
| `CancelCommand` canExecute bound to `IsRunning` | Since `IsRunning` never becomes true (see critical note), `CancelCommand` is always disabled. |
| Log auto-scroll missing | `LogEntries` items are added live but `ScrollViewer` (`LogScrollViewer`) has no auto-scroll to bottom behavior. Users must manually scroll to see the latest entry during a long sync. |
| `_cts` not thread-safe | `_cts?.Cancel()` in `CancelCommand` is called from the main thread while `RunSyncAsync` reads/writes `_cts` from the thread pool. Potential race condition on very fast cancellation. |
| Success auto-navigate only when no errors | `if (!HasErrors) _navigateToFilings()` — if there are parse errors but filings were created, the user is left on Sync with no clear CTA. The FilingsView link (`navigateToFilings_sync`) should also appear in the summary even on partial success. |

**XAML Issues (`SyncView.axaml`):**

| Element | Issue |
|---------|-------|
| Missing `ReactiveUserControl` root | See critical note above. |
| "Sync Mode" label is hardcoded English | `Text="Sync Mode"` (line 17) — not from `Strings.resx`. Same for `Text="Duplicates"` (line 30) and `Text="Replay From"` (line 46). |
| Sync Start button uses `IsEnabled="{Binding !IsRunning}"` | Since `IsRunning` is always `false` (see critical note), this is always enabled. A running sync will not disable the Start button. |
| Log `Timestamp` column width is `Width="60"` | `HH:mm:ss` is 8 characters — may be clipped at smaller font sizes. |
| Error and Warning use same icon | `SyncProgressEntryViewModel`: both `Error` and `Warning` severity map to `"⚠"`. No visual distinction. |
| `HasErrors` flag shows error panel | `IsVisible="{Binding HasErrors}"` shows a red panel with `{Binding ErrorMessage}`. `ErrorMessage` is set only on `result.Error.Message` (total failure) or `ThrownExceptions`. For partial success (individual item errors in `result.Value.Errors`), `HasErrors` is true but `ErrorMessage` is null — the red panel shows empty. |

---

### 7. Settings (`SettingsViewModel` / `SettingsView.axaml`)

`SettingsViewModel` is not `IActivatableViewModel` (no `WhenActivated`), which is correct since it only composes sub-VMs. It is registered as `AddTransient<SettingsViewModel>` but `MainWindowViewModel` receives it via constructor injection (singleton-compatible pattern). The four sub-VMs all implement `IActivatableViewModel` but have the activation-gap problem described below.

---

### 8. Profile Settings (`ProfileSettingsViewModel` / `ProfileSettingsView.axaml`)

> ⚠️ **Activation gap**: `ProfileSettingsView.axaml` root is `<UserControl>` — `ProfileSettingsViewModel.WhenActivated` (which contains `Observable.FromAsync(ct => LoadAsync(ct))` — the initial profile load) will never fire. Profile form will always show blank fields.

**XAML Issues (`ProfileSettingsView.axaml`):**

| Element | Issue |
|---------|-------|
| Labels stacked above fields with no spacing | `<TextBlock Text="JMBG (13 digits)" />` then `<TextBox Text="{Binding Jmbg}" />` in a `StackPanel Spacing="12"` — the spacing is between label–field pairs, not within them. Labels and fields have equal spacing, which makes it hard to visually pair them. |
| No `InputScope` or `MaxLength` on JMBG field | JMBG is exactly 13 digits. The TextBox has no `MaxLength="13"` — users can type more without feedback until Save fails. |
| No inline validation errors | JMBG validation (`jmbg.Length == 13 && jmbg.All(char.IsDigit)`) disables the Save button silently. The existing `Strings.Profile_JmbgValidation_Error` resource is unused in the AXAML. |
| `IsLoading` ProgressBar placed after Save button | Loading bar is the last element in the StackPanel — it appears below the success/error messages, making it easy to miss. Move it to the top of the form (below the title or before the first field). |
| `OpstinaCode` field has no helper text | The Opština municipal code is a non-obvious Serbian-specific identifier (5-digit numeric code). A placeholder or helper text like "e.g. 71101 for Stari Grad" would reduce errors. |
| Success message is persistent | `SuccessMessage` is never cleared on subsequent edits — after saving, "Profile saved successfully." remains visible even when the user starts editing fields again. Should clear on any field change. |

---

### 9. Holiday Settings (`HolidaySettingsViewModel` / `HolidaySettingsView.axaml`)

> ⚠️ **Activation gap**: `HolidaySettingsView.axaml` root is `<UserControl>` — `HolidaySettingsViewModel.WhenActivated` (which fires the initial holiday load) will never trigger. Holiday grid shows empty on first visit.

**Additional Issues:**

| Issue | Detail |
|-------|--------|
| Hardcoded English string in AXAML | Line 39 of `HolidaySettingsView.axaml`: `Text="No holidays configured. Click Add or Fetch from web to get started."` — not from `Strings.resx`. There's a similar string `Strings.Holidays_EmptyForRange` for the filtered-empty case, but this one is separate. |
| `HasUnsavedChanges` not surfaced in UI | The VM exposes `HasUnsavedChanges` but there is no warning indicator (asterisk on tab header, banner text) in the AXAML. Users can navigate away without saving and lose their added/deleted rows. |
| Date editing in DataGrid uses `TextBox` with `DateOnlyToStringConverter` | Editing a date by typing a string is error-prone. A `DatePicker` in the `CellEditingTemplate` would be safer for a financial deadline dataset. |
| `FetchFromWebCommand` description gap | The "Fetch from web" button runs for all years in `StartYear`–`EndYear`. No progress indicator during fetch — `IsLoading` is set but the only visual is the indeterminate ProgressBar at the bottom. For multi-year fetches, this could take seconds with no progress. |
| Year range validation missing | `StartYear` and `EndYear` are independent `NumericUpDown` controls. There's no guard that prevents `EndYear < StartYear`. |
| Delete is non-confirmatory | `DeleteRowCommand` immediately removes the selected row from the in-memory collection. No confirmation. `HasUnsavedChanges = true` is set, but the user can lose a row accidentally and must Save to persist (or re-fetch from web to recover). |

---

### 10. Mailbox Settings (`MailboxSettingsViewModel` / `MailboxSettingsView.axaml`)

> ⚠️ **Activation gap**: `MailboxSettingsView.axaml` root is `<UserControl>`.

**Additional Issues:**

| Issue | Detail |
|-------|--------|
| Delete has no confirmation | `DeleteCommand` calls `OnDeleteAsync` which immediately calls the server-side delete. No `ConfirmDialogHelper` call. This is inconsistent with Filing/Report delete (which both confirm). |
| `IsEditMode` flag unused in AXAML | `IsEditMode` is set in the VM but not read in `MailboxSettingsView.axaml` — there's no visual difference between "Add New" and "Edit" states. The form title doesn't change; the Save button label stays "Save". |
| Password field is always blank when switching mailboxes | `Password = string.Empty` on mailbox selection (line 125). The hint "Leave blank to keep existing" explains this, but the form looks like the password was cleared, which could alarm users. Consider a locked `PasswordChar` display showing `••••••••` to indicate an existing password is stored. |
| No connection test | Users must configure and save, then run a full sync to discover IMAP credentials are wrong. A "Test Connection" button would significantly improve the onboarding experience. |
| `AddNewCommand` has no canExecute | Can be executed while `IsLoading` is true — though `OnAddNew()` is synchronous and just resets form fields, so there's no functional bug, but it's inconsistent with the pattern used by `SaveCommand` and `DeleteCommand`. |

---

### 11. Importer Settings (`ImporterSettingsViewModel` / `ImporterSettingsView.axaml`)

> ⚠️ **Activation gap**: `ImporterSettingsView.axaml` root is `<UserControl>`.

**Additional Issues:**

| Issue | Detail |
|-------|--------|
| Delete has no confirmation | Same as Mailbox — `OnDeleteAsync` fires immediately. |
| `AttachmentRegex` field: no validation or example | Users must enter a regex pattern with no syntax check or example. An invalid regex will silently fail during sync. At minimum, a helper text showing a working example (e.g., `Activity.*\.csv`) should be provided. |
| `FromFilter` / `SubjectFilter`: no documentation | These fields are email header filters (presumably IMAP search terms or substring matches). There is no tooltip or helper text explaining the format. Power users configuring this for the first time will be confused. |
| `SaveCommand` has no canExecute | `ReactiveCommand.CreateFromTask(OnSaveAsync)` with no `canExecute` — fires while `IsLoading` is true. |
| Three sequential queries in `LoadAsync` | `LoadAsync` fires three separate queries sequentially (importers → profile → mailboxes). A loading failure on any one silently stops displaying a partial error (only `importersResult` sets `ErrorMessage`; profile and mailbox failures are ignored). |

---

### 12. Dialog Helpers (`ConfirmDialogHelper` / `ImportDialogHelper`)

| Issue | Detail |
|-------|--------|
| Destructive "Delete" button has no danger styling | `ConfirmDialogHelper.ShowAsync`: the `confirmButton` is a plain `Button` with `Content = confirmText`. For delete confirmations, the confirm button should have a red/danger style to signal the irreversibility. |
| Button order: Confirm left, Cancel right | In `ConfirmDialogHelper.ShowAsync`, `Children = { confirmButton, cancelButton }` puts confirm on the left. Windows/Fluent convention is "destructive action on left, Cancel on right" — this is actually fine for Windows, but the buttons have identical styling making them undifferentiated. |
| `ImportDialogHelper` has hardcoded English strings | Line 133: `Text = "Select importer:"` — not from `Strings.resx`. Line 117: `Content = "Import"` — not from `Strings.resx`. |
| No keyboard shortcut support | `ConfirmDialogHelper` dialog: Escape doesn't cancel, Enter doesn't confirm. `IsDefault` and `IsCancel` should be set on the buttons. |
| `ImportDialogHelper` step ordering | The file is picked *before* the importer is selected. If the user picks a `.csv` file intended for a specific importer, then cancels the importer dialog, the file read has already happened but is discarded — wasted I/O. Consider selecting the importer first. |

---

## User Journey Map — Full Flow with Gaps

```
[First Run]
    │
    ▼
Settings → Profile Tab
    │  ⚠️ WhenActivated never fires → form always blank on first open
    │  ⚠️ No JMBG inline validation
    │  ⚠️ No OpstinaCode helper text
    │
    ▼
Settings → Mailboxes Tab
    │  ⚠️ WhenActivated never fires → mailbox list blank
    │  ⚠️ No confirmation on Delete
    │  ⚠️ No connection test
    │
    ▼
Settings → Importers Tab
    │  ⚠️ WhenActivated never fires → importer list blank
    │  ⚠️ No regex validation on AttachmentRegex
    │  ⚠️ No documentation on filter fields
    │
    ▼
Settings → Holidays Tab
    │  ⚠️ WhenActivated never fires → holiday grid blank
    │  ⚠️ HasUnsavedChanges not shown in UI
    │
    ▼  ────────── Onboarding complete (user must know to do this sequence) ──────────
    │
    ▼
Reports → Sync Mailboxes (quick sync) or Sync Page (advanced)
    │  ⚠️ Reports: WhenActivated never fires → grid blank; user sees empty list,
    │            presses "Sync Mailboxes" which does work (called directly) → grid loads
    │  ⚠️ Sync page: WhenActivated never fires → IsRunning never true, Cancel disabled,
    │            ThrownExceptions never surfaced; Start button remains enabled during sync
    │  ⚠️ Two distinct sync entry points with different capabilities
    │
    ▼
Reports (grid populated after sync)
    │  ✓ Import CSV available for manual import
    │  ⚠️ No sort indicator in column headers
    │  ⚠️ Delete has no post-load gap confirmation for cascading filing deletion
    │
    ▼
Reports → "View Filings" (navigates to FilingsView with ReportIdFilter set)
    │  ✓ Navigation works correctly
    │  ⚠️ No breadcrumb in FilingsView showing "Filtered by: Report X"
    │  ⚠️ ReportIdFilter is not cleared when user clicks Filings in sidebar directly
    │
    ▼
Filings (grid shows filtered or all filings)
    │  ✓ Pagination, sort, bulk delete all present
    │  ⚠️ No sort direction indicator
    │  ⚠️ Paid filings show disabled Advance Status button (visual noise)
    │  ⚠️ Payment reference LostFocus commit is implicit
    │  ⚠️ No success confirmation on status advance
    │
    ▼
Filings → "New Filing" → ManualFilingView
    │  ✓ Form loads, Calculate → Preview → Save works
    │  ⚠️ Sidebar shows "Filings" selected, not "New Filing"
    │  ⚠️ No dirty-form guard on Cancel or sidebar navigation
    │  ⚠️ NoProfile error shown as red banner on load (alarming for first-time user)
    │  ⚠️ Button order confusing (Save visible before Calculate)
    │
    ▼
Filings → Export XML → Save file dialog
    │  ✓ Native save dialog with suggested filename
    │  ⚠️ No success confirmation after export
    │
    ▼
Dashboard
    │  ✓ KPI cards, overdue list, upcoming deadlines visible
    │  ✓ DashboardView IS ReactiveUserControl — WhenActivated fires correctly
    │  ⚠️ HasData guard unused in XAML
    │  ⚠️ Raw enum/DateOnly bindings in UpcomingDeadlines DataGrid
    │  ⚠️ No refresh button
    │  ⚠️ LastSyncDisplay uses hardcoded "Never" instead of Strings resource
```

---

## Prioritized Recommendations

### 🔴 HIGH — Fix Before Ship

| # | Recommendation | File(s) | Change |
|---|---------------|---------|--------|
| H1 | Convert all non-reactive views to `ReactiveUserControl<T>` | `ReportsView.axaml`, `SyncView.axaml`, `SettingsView.axaml`, `ProfileSettingsView.axaml`, `HolidaySettingsView.axaml`, `MailboxSettingsView.axaml`, `ImporterSettingsView.axaml` | Change root from `<UserControl>` to `<reactive:ReactiveUserControl x:TypeArguments="vm:TViewModel">` and code-behind base class from `UserControl` to `ReactiveUserControl<TViewModel>`. This unblocks all `WhenActivated` initial data loads. |
| H2 | Fix Dashboard DataGrid raw bindings | `DashboardView.axaml` | Replace `{Binding FilingDeadline}` with `{Binding FilingDeadline, StringFormat='yyyy-MM-dd'}` (or add a display property to `UpcomingDeadlineDto`). Replace `{Binding Status}` and `{Binding IncomeType}` with their respective display converters. Add `RSD` suffix to `TaxPayableRsd`. |
| H3 | Fix sidebar selection during ManualFiling | `MainWindowViewModel.cs` | Instead of setting `CurrentViewModel = manualVm` directly, add a dedicated `NavigationEntry` for ManualFiling (hidden from sidebar, or a sub-entry) and set `SelectedEntry` to it. Alternatively show the ManualFilingView within the Filings panel with a visible breadcrumb. |
| H4 | Add `MinWidth`/`MinHeight` to MainWindow | `MainWindow.axaml` | Add `MinWidth="780" MinHeight="520"` to prevent content clipping on resize. |
| H5 | Fix frozen `AdvanceStatusCommand` canExecute | `FilingRowViewModel.cs` | Bind canExecute to `this.WhenAnyValue(x => x.HasNextStatus)` or use `IsVisible` binding in AXAML to hide the button entirely for Paid rows. |
| H6 | Wire `IsRunning` for `SyncViewModel` | `SyncViewModel.cs` | Move `SyncCommand.IsExecuting.Subscribe(v => IsRunning = v)` and `ThrownExceptions` handler out of `WhenActivated` (or fix H1 first). Alternatively, use `SyncCommand.IsExecuting.ToProperty(this, x => x.IsRunning)` as an `ObservableAsPropertyHelper`. |

---

### 🟡 MEDIUM — Fix in Next Sprint

| # | Recommendation | File(s) | Change |
|---|---------------|---------|--------|
| M1 | Replace LostFocus payment-ref save with explicit gesture | `FilingsView.axaml.cs`, `FilingsViewModel.cs` | Add a "💾" icon button next to the TextBox that executes `SavePaymentRefCommand`, or trigger save on `KeyDown Enter`. Remove `LostFocus` handler. |
| M2 | Add sort direction indicator to Filings DataGrid | `FilingsView.axaml`, `FilingsViewModel.cs` | Bind column header sort glyph (▲/▼) to `SortColumn` and `SortDescending`. Use a custom `DataGridColumnHeader` style or a `PathIcon` that shows only on the active sort column. |
| M3 | Add success toasts for status advance, export, and saves | `FilingsView.axaml`, `ManualFilingView.axaml`, `ProfileSettingsView.axaml` | Use a timed-dismiss banner (e.g., `AutoDismissInfoBar` or a `TextBlock` with a timer) for positive feedback. |
| M4 | Add dirty-form guard to ManualFilingView | `ManualFilingViewModel.cs`, `MainWindowViewModel.cs` | Track `IsDirty` via `WhenAnyValue` on all form fields. On `CancelCommand` or sidebar navigation, show a confirmation dialog if `IsDirty`. |
| M5 | Add confirmation dialogs to Mailbox and Importer Delete | `MailboxSettingsViewModel.cs`, `ImporterSettingsViewModel.cs` | Inject `Func<string, Task<bool>> confirmDelete` (the same pattern as `FilingsViewModel`) and call before delete. |
| M6 | Inline JMBG validation error in ProfileSettingsView | `ProfileSettingsView.axaml`, `ProfileSettingsViewModel.cs` | Add a `ValidationMessage` computed property that returns `Strings.Profile_JmbgValidation_Error` when JMBG is invalid. Bind to a `TextBlock` immediately below the JMBG TextBox. Add `MaxLength="13"` to the TextBox. |
| M7 | Fix `BulkDeleteCommand` missing `canExecute` | `FilingsViewModel.cs`, `ReportsViewModel.cs` | Pass `this.WhenAnyValue(x => x.HasSelection)` as the `canExecute` parameter to `ReactiveCommand.CreateFromTask(...)`. |
| M8 | Fix `LastSyncDisplay` to use `Strings.Dashboard_LastSyncNever` | `DashboardViewModel.cs` line 143 | Replace `"Never"` with `Strings.Dashboard_LastSyncNever`. |
| M9 | Reorder ManualFiling buttons + hide Save until Preview exists | `ManualFilingView.axaml` | Wrap "Save Filing" and "Recalculate" in a panel with `IsVisible="{Binding Preview, Converter={x:Static ObjectConverters.IsNotNull}}"`. Show only `[Calculate] [Cancel]` initially. |
| M10 | Show `ExchangeRateSourceType` in preview panel | `ManualFilingView.axaml`, `ManualFilingPreviewDto` | Add a `RateSourceDisplay` property to `ManualFilingPreviewDto` that uses `Strings.ManualFiling_RateSource_Exact` / `Strings.ManualFiling_RateSource_Fallback` based on `ExchangeRateSourceType`. |
| M11 | Add keyboard support to `ConfirmDialogHelper` | `ConfirmDialogHelper.cs` | Set `IsDefault="True"` on the confirm button and `IsCancel="True"` on the cancel button. |
| M12 | Add `HasData` state gate to Dashboard XAML | `DashboardView.axaml`, `DashboardViewModel.cs` | Bind KPI card section `IsVisible="{Binding HasData}"` to hide zero-state cards. Add a `TextBlock IsVisible="{Binding !HasData}"` with "No data yet. Run a sync to get started." |
| M13 | Auto-scroll sync log to bottom | `SyncView.axaml`, `SyncView.axaml.cs` | Subscribe to `LogEntries.CollectionChanged` and call `LogScrollViewer.ScrollToEnd()` in code-behind. |
| M14 | Move `NoProfile` error to informational callout | `ManualFilingView.axaml` | Use a dedicated `InfoBar`-style border (blue, not red) with "⚙ Please configure your taxpayer profile in Settings" + a hyperlink-style button that navigates to Settings. Only show if `_taxpayerProfileId == null` at calculate time. |

---

### 🟢 LOW — Polish / Accessibility

| # | Recommendation | File(s) | Change |
|---|---------------|---------|--------|
| L1 | Add `AutomationProperties.Name` to icon-only action buttons | `FilingsView.axaml`, `ReportsView.axaml` | `AutomationProperties.Name="Export PP-OPO XML"`, `AutomationProperties.Name="Mark as Filed"`, etc. on all `PathIcon` buttons. |
| L2 | Move string literals from dialogs to Strings.resx | `ImportDialogHelper.cs`, `ConfirmDialogHelper.cs`, `HolidaySettingsView.axaml`, `SyncView.axaml` | "Select importer:", "Import", "OK", "Sync Mode", "Duplicates", "Replay From", "No holidays configured..." |
| L3 | Distinguish Error vs Warning icon in sync log | `SyncProgressEntryViewModel.cs` | Error → `"✕"`, Warning → `"⚠"`, Info → `"•"`. Or use colored `TextBlock`s via a `SyncProgressSeverity`-to-`IBrush` converter. |
| L4 | Clear `SuccessMessage` on field edit in ProfileSettings | `ProfileSettingsViewModel.cs` | `this.WhenAnyValue(...all fields...).Skip(1).Subscribe(_ => SuccessMessage = string.Empty)`. |
| L5 | Add `HasUnsavedChanges` indicator to Holidays tab header | `HolidaySettingsView.axaml` or `SettingsView.axaml` | Show an asterisk or dot indicator on the "Holidays" `TabItem` header when `HasUnsavedChanges == true`. |
| L6 | Add `MaxLength="13"` to JMBG TextBox | `ProfileSettingsView.axaml` | Prevents over-entry. |
| L7 | Replace ToggleButton filter pair with RadioButton group | `FilingsView.axaml` | `RadioButton GroupName="FilingFilter"` is semantically correct for mutually exclusive filters. Or use a `SegmentedControl`-style single control. |
| L8 | Hide disabled Advance-Status button for Paid filings | `FilingsView.axaml` | Add `IsVisible="{Binding HasNextStatus}"` to the advance-status `Button`. Removes visual noise from terminal-state rows. |
| L9 | Add `OpstinaCode` helper text | `ProfileSettingsView.axaml` | Add `TextBlock` with `Opacity="0.6" FontSize="11" Text="5-digit municipal code, e.g. 71101"` below the OpstinaCode TextBox. |
| L10 | Add "Test Connection" to Mailbox Settings | `MailboxSettingsView.axaml`, `MailboxSettingsViewModel.cs` | A `TestConnectionCommand` that performs a minimal IMAP handshake and reports pass/fail inline. |
| L11 | Add regex validation display to Importer's AttachmentRegex | `ImporterSettingsView.axaml`, `ImporterSettingsViewModel.cs` | Try `new Regex(AttachmentRegex)` reactively; show a green "✓ Valid" or red "✕ Invalid regex" label below the field. |
| L12 | Year-range guard in Holiday Settings | `HolidaySettingsViewModel.cs` | Add a `ValidationMessage` property: `EndYear < StartYear ? "End year must be ≥ Start year" : null`. Disable `SaveCommand` and `FetchFromWebCommand` when invalid. |
| L13 | Dispose `Subscribe()` calls in FilingsView code-behind | `FilingsView.axaml.cs` | Store the returned `IDisposable` in a field and dispose in `OnDetachedFromLogicalTree`. |
| L14 | Add "No upcoming deadlines" empty state to Dashboard | `DashboardView.axaml` | `<TextBlock IsVisible="{Binding UpcomingDeadlines.Count, Converter=...IsZero}" Text="No upcoming deadlines in the next 30 days." />` inside the upcoming deadlines `Grid`. |
| L15 | Add `SelectAll/ClearSelection` buttons to Filings and Reports toolbar | `FilingsView.axaml`, `ReportsView.axaml` | `Strings.BulkDelete_SelectAll_Button` and `Strings.BulkDelete_ClearSelection_Button` exist in Strings.resx but are not used in any XAML — expose them as secondary toolbar buttons. |

---

## Accessibility & Financial UX Best Practices Being Violated

### Accessibility

1. **Icon-only buttons without accessible names**: All three per-row action buttons in `FilingsView` and the two in `ReportsView` have no `AutomationProperties.Name`. A screen reader will say "Button" with no context.

2. **Color-only status communication**: `FilingStatusBrushConverter` uses color alone (gold/blue/green pills) to communicate `Init/Filed/Paid`. `StatusDisplayText` is shown in white text on the pill — this is adequate for sighted users, but the pill `IsHitTestVisible="False"` means it carries no accessible role. The text inside is sufficient for screen readers IF the TextBlock is not inside a non-interactive wrapper that swallows accessibility events.

3. **No `AutomationProperties.LabeledBy`** on form fields in ProfileSettings, MailboxSettings, ImporterSettings — the `TextBlock` labels are visually above the `TextBox` controls but not programmatically associated.

4. **No keyboard focus order management**: On `ManualFilingView`, `Tab` order follows DOM order (Income Type → Ticker → Date → Currency → Gross → Net → Calculate → Save → Cancel), which is correct. But the DatePicker control may trap Tab focus.

5. **Dashboard KPI numbers lack context for screen readers**: `TextBlock Text="{Binding InitCount}"` reads as a bare number. A screen reader user hears "3" with no idea it means "3 pending filings."

### Financial UX

1. **Ambiguous number formatting**: `InvariantCulture` throughout means amounts display as `1,234.56 RSD`. Serbian convention is `1.234,56 RSD`. Either commit to a format (InvariantCulture is acceptable for a developer-targeted tool) or use NBS/Serbian culture — but be consistent. The Dashboard `StringFormat={}{0:N2}` on `TaxPayableRsd` breaks the consistency.

2. **Destructive actions lack clear consequence descriptions**: The bulk-delete confirmation for Reports says "Delete {0} report(s) and all their linked filings?" — correct, but the individual report delete dialog (`Strings.Reports_Delete_Confirmation_Message`) says the same. The Filings delete message is shorter. All three could benefit from showing the filing count (e.g., "Deleting this report will permanently remove 12 linked filings").

3. **No audit log or undo**: Tax filings are financial records. There is no undo for status advancement (Init → Filed → Paid). Once `Paid`, a filing has no way back via the UI. A "revert to Filed" action or at minimum a warning ("This action cannot be undone") at the point of advancing status would be appropriate for a financial application.

4. **Exchange rate source not prominent enough**: `ManualFilingViewModel` fetches the NBS exchange rate and exposes `ExchangeRateSourceDate` in the Preview. Whether the rate is exact or a fallback is critically important for tax accuracy, but the "Rate Source" label binds to a date with no type indicator. `Strings.ManualFiling_RateSource_Exact` and `Strings.ManualFiling_RateSource_Fallback` exist but are never rendered.

5. **No deadline proximity indicator**: Filings approaching their deadline (≤7 days) are indistinguishable from those with months to go. The Dashboard's overdue section uses a red banner, but there is no amber/warning indicator for "due soon" filings within the Filings grid itself.
