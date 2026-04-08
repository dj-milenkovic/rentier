# Research: Holidays Settings Repair

**Feature**: 020-holidays-settings-repair
**Date**: 2025-07-15

---

## R-001: HTML Parsing Library Choice

### Decision
**Keep AngleSharp** (already a project dependency at version 1.*)

### Rationale
- AngleSharp is already in `Rentier.Infrastructure.csproj` — no new dependency needed.
- Full CSS selector support (`QuerySelector`, `QuerySelectorAll`) makes HTML table parsing clean.
- Well-maintained, high-quality .NET library with strong DOM manipulation API.
- Provides `IDocument` interface that enables in-memory parsing from string content.

### Alternatives Considered
| Alternative | Reason Rejected |
|---|---|
| **HtmlAgilityPack** | Would add a new dependency. Uses XPath instead of CSS selectors — less readable for this use case. No advantage over AngleSharp already in use. |
| **Regex** | Fragile, hard to maintain, and error-prone for HTML parsing. Constitution Principle V (quality gates) argues against regex for structured data extraction. |

---

## R-002: HTML Table Structure — timeanddate.com

### Decision
Parse the `<table id="holidays-table">` using these selectors:
- **Date**: `row.QuerySelector("th")?.TextContent` (format: `"d MMM"`, e.g., "1 Jan", "15 Feb")
- **Name**: 3rd `<td>` element's anchor text content → `row.QuerySelectorAll("td")[1].QuerySelector("a")?.TextContent ?? row.QuerySelectorAll("td")[1].TextContent`
- **National holiday filter**: Rows with CSS class `showrow` AND 4th column text containing "National Holiday"
- **Skip rows**: Rows without `showrow` class, separator rows (empty `id` like `hol_jan`), header rows

### Rationale
Analysis of the captured HTML from `holiday-scraped.txt` (Serbia 2016) reveals:

```html
<tr id="tr1" class="showrow" data-mask="1" data-date="1451606400000">
  <th class="nw">1 Jan</th>                      ← DATE (in <th>, not <td>!)
  <td class="nw">Friday</td>                     ← Day of week
  <td><a href="...">Western New Year's Day</a></td> ← NAME (anchor inside 2nd <td>)
  <td>National Holiday</td>                       ← TYPE
</tr>
```

Key structural findings:
1. Dates are in `<th>` elements, NOT `<td>` — this is the primary parser bug.
2. The `td.ce` CSS class referenced in the original code does not exist anywhere in the HTML.
3. National holidays have `class="showrow"` and `data-mask` attribute with bit 0 set (odd values).
4. Non-national holidays (observances, seasons, optional) have `class="hiderow"`.
5. Month separator rows (e.g., `<tr id="hol_feb"></tr>`) are empty and should be skipped.
6. The URL parameter `?hol=1` requests public holidays, but the response HTML still includes all rows with `showrow`/`hiderow` for client-side JS filtering.

### National Holiday Rows for 2016 (class="showrow", data-mask=1 or 8388609)
| Date | Name |
|------|------|
| 1 Jan | Western New Year's Day |
| 2 Jan | Second Day of Western New Year's Day |
| 7 Jan | Christmas Day |
| 15 Feb | Statehood Day of the Republic of Serbia |
| 16 Feb | Statehood Day of the Republic of Serbia (Day 2) |
| 29 Apr | Good Friday |
| 30 Apr | Holy Saturday |
| 1 May | Labor holiday |
| 1 May | Easter Day |
| 2 May | Easter Monday |
| 2 May | Day off for Labor holiday |
| 3 May | Labor Day Holiday |
| 11 Nov | Armistice Day |

Total: **13 national holiday entries** (matches SC-003).

### Date Parsing Strategy
- Parse with format `"d MMM"` using `CultureInfo.InvariantCulture` (English month abbreviations).
- Reconstruct full date as `new DateOnly(year, parsed.Month, parsed.Day)`.
- The year comes from the URL/command parameter, not from the HTML.

---

## R-003: DateOnly↔String Converter for Avalonia DataGrid

### Decision
Create a **`DateOnlyToStringConverter : IValueConverter`** registered as a XAML static resource.

### Rationale
Avalonia's `DataGridTextColumn` performs two-way binding through the `IValueConverter` pipeline:
- **Display mode** (`Convert`): `DateOnly` → `string` (formatted as `"yyyy-MM-dd"`)
- **Edit mode** (`ConvertBack`): `string` → `DateOnly` (parsed from user input)

Using `DataGridTemplateColumn` with separate `CellTemplate` and `CellEditingTemplate` gives full control:
- Display template: `TextBlock` with converter
- Edit template: `TextBox` with two-way converter binding

The converter's `ConvertBack` returns `BindingNotification.CreateError(...)` on invalid input, which Avalonia surfaces as a validation error visual.

### Implementation Pattern
```csharp
public sealed class DateOnlyToStringConverter : IValueConverter
{
    public static readonly DateOnlyToStringConverter Instance = new();
    private const string DisplayFormat = "yyyy-MM-dd";

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is DateOnly date ? date.ToString(DisplayFormat) : string.Empty;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string s && DateOnly.TryParse(s, CultureInfo.InvariantCulture, out var date))
            return date;
        return new BindingNotification(
            new FormatException("Invalid date format. Use yyyy-MM-dd."),
            BindingErrorType.DataValidationError);
    }
}
```

### Alternatives Considered
| Alternative | Reason Rejected |
|---|---|
| **DatePicker in CellEditingTemplate** | DatePicker operates on `DateTimeOffset`, not `DateOnly`. Would require additional conversion and is heavier than a simple TextBox for date entry. |
| **TypeConverter on DateOnly** | Global type converter would affect all DateOnly bindings project-wide. A scoped `IValueConverter` is safer and more explicit. |

---

## R-004: Duplicate Holiday Handling on Re-Import

### Decision
**Replace strategy** — import clears and replaces grid contents, not upsert.

### Rationale
- The spec (acceptance scenario 2.6) explicitly states: "imported holidays replace the current grid contents."
- The current ViewModel already calls `Entries.Clear()` before adding imported entries.
- The save handler performs full replacement (`ExecuteDeleteAsync` + `AddRange`), not incremental upsert.
- Duplicate-date validation happens at save time, not import time.
- This is the simplest and most predictable strategy — no merge logic needed.

---

## R-005: NumericUpDown Layout Fix for Year Fields

### Decision
Replace `Width="100"` with `MinWidth="120"` and add `FormatString="0"`.

### Rationale
- At default Avalonia FluentTheme font sizes, a 4-digit year (e.g., "2025") plus spinner buttons needs approximately 110–120px minimum.
- Using `MinWidth` instead of `Width` allows the control to grow if more space is available in the parent panel.
- `FormatString="0"` prevents display of decimal places (NumericUpDown defaults to decimal display).
- Consistent sizing for all three year-related NumericUpDown controls (StartYear, EndYear, ImportYear).

### Alternatives Considered
| Alternative | Reason Rejected |
|---|---|
| **SharedSizeGroup** | Overkill for this layout; the controls aren't in a Grid that benefits from shared sizing. |
| **Grid.Column with proportional widths** | Would require restructuring the toolbar from StackPanel to Grid, which is a larger change than needed for this fix. |
| **Auto width** | NumericUpDown with `Auto` width may produce inconsistent sizes when values change. `MinWidth` provides a stable floor. |

---

## R-006: Error Code Standardization

### Decision
Align scraper error codes with spec-defined codes.

| Current Code | Spec Code | When Used |
|---|---|---|
| `FETCH_FAILED` | `HOLIDAY_IMPORT_FAILED` | Network error (HttpRequestException, TaskCanceledException) |
| `PARSE_FAILED` | `HOLIDAY_PARSE_ERROR` | HTML parsing failure (malformed HTML, missing table) |
| `NO_HOLIDAYS_FOUND` | `HOLIDAY_NOT_FOUND` | Parser returns zero results for requested year |

### Rationale
The spec defines these codes in FR-010. Existing tests that reference old codes will need updating. The `Error` record already supports arbitrary code strings, so this is a straightforward string change.
