# Root Cause Analysis: Holidays Settings Repair

**Feature**: 020-holidays-settings-repair
**Date**: 2025-07-15
**Scope**: HTML parser failures, DataGrid editing, layout clipping, state management

---

## Bug 1: Import Returns Zero Holidays (P1 — Critical)

### Symptom
Clicking Import for any year returns "No holidays found" despite the web source containing valid data.

### Root Cause
**Date extraction queries `<td>` but dates are in `<th>` elements.**

The HTML structure per row is:
```html
<tr class="showrow" data-mask="1">
  <th class="nw">1 Jan</th>          <!-- DATE is here, in <th> -->
  <td class="nw">Friday</td>         <!-- Day-of-week -->
  <td><a href="...">Name</a></td>    <!-- Holiday name -->
  <td>National Holiday</td>          <!-- Type -->
</tr>
```

The parser code:
```csharp
var cells = row.QuerySelectorAll("td");  // Only selects <td>, NOT <th>
var dateText = cells[0].TextContent;     // Gets "Friday" instead of "1 Jan"
```

Since "Friday" cannot parse as any of `"MMM d"`, `"d MMM"`, `"MMM dd"`, `"dd MMM"` formats, the `catch` block silently skips every row. After the loop, `results.Count == 0` → returns `NO_HOLIDAYS_FOUND`.

### Fix
Query the `<th>` element for the date: `row.QuerySelector("th")?.TextContent.Trim()`.

### Contributing Factor — Name Selector Also Wrong
The code tries `row.QuerySelector("td.ce")` but no `ce` class exists in the HTML. The fallback `cells[1]` happens to point at the correct name cell (the anchor `<td>`), so name extraction would work by accident — but only after the date bug is fixed.

### Contributing Factor — No National Holiday Filtering
The parser skips `noshow` and `js-holiday-private` but does NOT filter for `showrow` class or `data-mask="1"`. Without filtering, observances (class `hiderow`), seasons, and optional holidays would all be included if date parsing were fixed. The URL parameter `?hol=1` provides server-side filtering, but the HTML response still includes `hiderow` rows for client-side JS toggling.

---

## Bug 2: DataGrid DateOnly Cell Editing Fails (P1 — Critical)

### Symptom
Double-clicking a date cell in the Holidays DataGrid does not allow editing, or edited values fail to commit. The DataGrid does not know how to convert between `string` and `DateOnly`.

### Root Cause
**Avalonia's `DataGridTextColumn` has no built-in `DateOnly` ↔ `string` conversion.**

Current XAML:
```xml
<DataGridTextColumn Header="Date" Binding="{Binding Date}" Width="*" />
```

The `Date` property is `DateOnly`. Avalonia's DataGrid text column uses `ToString()` for display (which works) but has no converter for `ConvertBack` when the user types a new value. The default binding fails silently, and the cell reverts without committing.

### Fix
Replace `DataGridTextColumn` with `DataGridTemplateColumn` using a custom `DateOnlyToStringConverter : IValueConverter`. The converter:
- `Convert`: formats `DateOnly` → `"yyyy-MM-dd"` string
- `ConvertBack`: parses string → `DateOnly`, returning `BindingNotification.CreateError` on invalid input

This enables Enter to commit, Escape to cancel, and invalid input to be rejected with feedback.

---

## Bug 3: Year Fields Clipped (P2 — Usability)

### Symptom
The NumericUpDown controls for Start Year and End Year do not display all 4 digits. The spinner arrows overlap or truncate the displayed value.

### Root Cause
**Fixed `Width="100"` is too narrow for 4-digit years plus spinner buttons.**

Current XAML:
```xml
<NumericUpDown Value="{Binding StartYear}" Width="100" />
<NumericUpDown Value="{Binding EndYear}" Width="100" />
```

The 100px fixed width must accommodate: left padding + 4-digit text + right padding + up/down spinner buttons. At default font size, this leaves insufficient space.

### Fix
Replace `Width="100"` with `MinWidth="120"` to provide a floor while allowing the control to grow if more space is available. Also add `FormatString="0"` to suppress decimal places.

---

## Bug 4: HasUnsavedChanges Not Set After Import (P2 — State)

### Symptom
After a successful import that replaces grid contents, the Save button does not indicate unsaved changes. The user may navigate away without saving.

### Root Cause
**Missing `HasUnsavedChanges = true` in the ImportCommand success path.**

Current code:
```csharp
if (result.IsSuccess)
{
    Entries.Clear();
    foreach (var dto in result.Value)
        Entries.Add(HolidayEntryViewModel.FromDto(dto));
    // ← Missing: HasUnsavedChanges = true;
}
```

### Fix
Add `HasUnsavedChanges = true;` after populating entries from import.

---

## Bug 5: Missing Import Year Control in XAML (P2 — UX)

### Symptom
The Import button passes `{Binding ImportYear}` as a command parameter, but there is no visible NumericUpDown or TextBox bound to `ImportYear` in the XAML. The user cannot choose which year to import.

### Root Cause
The `ImportYear` property exists on the ViewModel (initialized to `DateTime.Today.Year`) but the AXAML toolbar only has the Import button — no input field for specifying the year.

### Fix
Add a NumericUpDown for import year in the toolbar row, before the Import button.

---

## Bug 6: Error Codes Not Aligned With Spec (Minor)

### Symptom
The spec defines error codes `HOLIDAY_IMPORT_FAILED`, `HOLIDAY_PARSE_ERROR`, `HOLIDAY_NOT_FOUND`. The current scraper uses `FETCH_FAILED`, `PARSE_FAILED`, `NO_HOLIDAYS_FOUND`.

### Root Cause
Error codes were defined before the spec was written. They need alignment.

### Fix
Update scraper error codes to match spec-defined codes.

---

## Impact Summary

| Bug | Severity | Layer | User Impact |
|-----|----------|-------|-------------|
| 1. Date in `<th>` not `<td>` | Critical | Infrastructure | Import always returns zero results |
| 2. No DateOnly converter | Critical | Desktop | Cannot edit dates at all |
| 3. Year field clipping | Medium | Desktop | 4-digit years partially hidden |
| 4. HasUnsavedChanges missing | Medium | Desktop | Silent data loss risk after import |
| 5. No ImportYear input | Medium | Desktop | User cannot select year to import |
| 6. Error code mismatch | Low | Infrastructure | Inconsistent API contract |
