# Technical Research: Holiday Fetcher — timeanddate.com Scraper

**Feature Branch**: `026-holiday-web-scraper`  
**Created**: 2025-07-18

## 1. HTML Source Analysis

### Target URL Pattern

```
https://www.timeanddate.com/holidays/serbia/{year}?hol=1
```

The `hol=1` query parameter filters to public/national holidays only. Without it, the page includes observances, religious holidays, and other non-official dates.

### Page Structure (verified from `holidays.txt` snapshot — 2026 actual HTML)

The holidays table has the following structure:

```html
<table id="holidays-table" class="table table--left table--inner-borders-rows table--full-width table--sticky table--holidaycountry"
       data-tad-control="HolidayCountry.Table">
  <thead>
    <tr>
      <th rowspan=2>Date</th>
      <th rowspan=2>&nbsp;</th>   <!-- weekday — not needed -->
      <th rowspan=2>Name</th>
      <th rowspan=2>Type</th>
    </tr>
  </thead>
  <tbody>
    <!-- month separator rows, e.g.: -->
    <tr id="hol_jan"></tr>

    <!-- public holiday row — class="showrow", data-mask includes bit 0 -->
    <tr id="tr1" class="showrow" data-mask="1" data-date="1767225600000">
      <th class="nw">1 Jan</th>
      <td class="nw">Thursday</td>
      <td><a href="/holidays/serbia/new-year-day">Western New Year&#39;s Day</a></td>
      <td>National Holiday</td>
    </tr>

    <!-- Orthodox public holiday — data-mask=8388609 (bit 0 + bit 23), class="showrow" -->
    <tr id="tr5" class="showrow" data-mask="8388609" data-date="1767744000000">
      <th class="nw">7 Jan</th>
      <td class="nw">Wednesday</td>
      <td><a href="/holidays/serbia/christmas-day">Christmas Day</a></td>
      <td>National Holiday, Orthodox</td>
    </tr>

    <!-- non-public (hidden when hol=1) — class="hiderow" -->
    <tr id="tr3" class="hiderow" data-mask="8388624" data-date="1767484800000">
      <th class="nw">4 Jan</th>
      <td class="nw">Sunday</td>
      <td><a href="/holidays/serbia/fathers-day">Father&#8217;s Day</a></td>
      <td>Observance, Orthodox</td>
    </tr>
  </tbody>
</table>
```

**Key observations from actual HTML**:
- Table ID is `#holidays-table` — stable scraping anchor
- Public-holiday rows have `class="showrow"` AND `data-mask` where **bit 0 is set** (mask & 1 != 0)
- Non-public rows have `class="hiderow"` — can be safely skipped
- Each row carries a `data-date` attribute = **Unix timestamp in milliseconds** — this is the most reliable date source (avoids locale/format ambiguity)
- Date text in `<th class="nw">` is format `d MMM` (e.g., "1 Jan", "15 Feb") — existing scraper already handles this
- Holiday name is in `<td>[1]`, wrapped in `<a>` tag; fallback to `<td>[1].TextContent`
- Holiday type is in `<td>[2]`; National Holidays contain "National Holiday" substring
- Month separator rows (`<tr id="hol_jan">` etc.) have no `<th>` and no `data-mask` — skip them
- The year is NOT in the date text — must be provided from the request year parameter

**Recommended date extraction (most robust)**:
```csharp
// Option A — use data-date (Unix ms timestamp) — most reliable
var dataDate = row.GetAttribute("data-date");
if (long.TryParse(dataDate, out var unixMs))
{
    var dt = DateTimeOffset.FromUnixTimeMilliseconds(unixMs).UtcDateTime;
    date = new DateOnly(dt.Year, dt.Month, dt.Day);
}

// Option B — parse text (existing scraper approach, works fine)
var parsed = DateTime.ParseExact(dateText, "d MMM", CultureInfo.InvariantCulture);
date = new DateOnly(year, parsed.Month, parsed.Day);
```

The existing `TimeAndDateHolidayScraper.cs` already uses Option B correctly — **no change needed to the scraper itself**.

### Known Serbian National Holidays (annual, recurring)

| Date       | Serbian Name          | English Name (timeanddate.com)     |
| ---------- | --------------------- | ---------------------------------- |
| January 1  | Nova godina           | New Year's Day                     |
| January 2  | Nova godina           | New Year Holiday                   |
| January 7  | Božić                 | Orthodox Christmas Day              |
| February 15| Sretenje (Dan državnosti) | Statehood Day               |
| February 16| Sretenje              | Statehood Day Holiday              |
| May 1      | Praznik rada          | Labour Day / May Day               |
| May 2      | Praznik rada          | Labour Day Holiday                 |
| June 28    | Vidovdan              | St. Vitus Day (Vidovdan)           |
| November 11| Dan primirja          | Armistice Day                      |

Note: Some holidays span two days (Jan 1–2, Feb 15–16, May 1–2). These appear as separate rows in the table. Typically 9–11 rows per year with `hol=1`.

---

## 2. HTML Scraping Library: AngleSharp

### Recommendation: **AngleSharp** (already in use)

The project already uses AngleSharp (`AngleSharp` NuGet package, version 1.*) in both:
- `NbsWebScraper.cs` — for NBS exchange rate HTML parsing
- `TimeAndDateHolidayScraper.cs` — for the existing holiday scraper

**No new dependency is needed.** AngleSharp is the established choice.

### Why AngleSharp over HtmlAgilityPack

| Criteria                     | AngleSharp          | HtmlAgilityPack     |
| ---------------------------- | ------------------- | ------------------- |
| Already in project           | ✅ Yes              | ❌ No               |
| CSS selector support         | ✅ Native           | ⚠️ Via extension    |
| W3C DOM compliance           | ✅ Full             | ⚠️ Partial          |
| Async parsing                | ✅ Built-in         | ❌ Sync only        |
| .NET 8 compatibility         | ✅ Yes              | ✅ Yes              |
| Active maintenance           | ✅ Yes              | ✅ Yes              |

### Usage Pattern (from existing codebase)

```csharp
// 1. Parse HTML into document
var config = Configuration.Default;
var context = BrowsingContext.New(config);
var document = await context.OpenAsync(req => req.Content(html), ct);

// 2. Query with CSS selectors
var table = document.QuerySelector("#holidays-table");
var rows = table.QuerySelectorAll("tr.showrow");

// 3. Extract data from cells
var dateText = row.QuerySelector("th")?.TextContent.Trim();
var tds = row.QuerySelectorAll("td");
var name = tds[1].QuerySelector("a")?.TextContent.Trim() ?? tds[1].TextContent.Trim();
var type = tds[2].TextContent.Trim();
```

---

## 3. Existing Holiday Data Model

### Domain Layer

**PublicHoliday** (`src/Rentier.Domain/Entities/PublicHoliday.cs`):
- `Guid Id` — unique identifier (generated on creation)
- `DateOnly Date` — the holiday date
- `string Name` — display name (max 200 characters)
- `int Year` — derived from Date, stored for query efficiency

**HolidayConf** (`src/Rentier.Domain/ValueObjects/HolidayConf.cs`):
- `IReadOnlyList<DateOnly> Holidays` — flat list of holiday dates
- Used by deadline calculator to check if a date is a business day

**HolidayYearRange** (`src/Rentier.Domain/Entities/HolidayYearRange.cs`):
- Singleton entity (Id = 1)
- `int StartYear` (≥ 2020)
- `int EndYear` (≤ StartYear + 10)
- Defines the viewable/editable range on the settings page

### Application Layer

**DTOs**:
- `HolidayEntryDto(DateOnly Date, string Name)` — single holiday
- `HolidayConfDto(IReadOnlyList<HolidayEntryDto> Holidays, int StartYear, int EndYear)` — complete configuration

**Interface**:
- `IHolidayImporter.ImportAsync(int year, CancellationToken)` → `Result<IReadOnlyList<HolidayEntryDto>, Error>`
- Flagged with CA-EXT-001: explicit user action only

**Command/Handler**:
- `ImportHolidaysFromWebCommand(int Year)` → delegates to `IHolidayImporter`
- Returns `Result<IReadOnlyList<HolidayEntryDto>, Error>`

### Infrastructure Layer

**TimeAndDateHolidayScraper** (`src/Rentier.Infrastructure/Scraping/TimeAndDateHolidayScraper.cs`):
- Implements `IHolidayImporter`
- Registered via `services.AddHttpClient<IHolidayImporter, TimeAndDateHolidayScraper>()`
- Error codes: `HOLIDAY_IMPORT_FAILED`, `HOLIDAY_PARSE_ERROR`, `HOLIDAY_NOT_FOUND`

### Persistence

- Table `PublicHolidays`: columns (Id, Date, Name, Year), index on Year
- Table `HolidayYearRange`: columns (Id, StartYear, EndYear)
- Save pattern: truncate all → insert all (atomic replace)

---

## 4. Existing Implementation Gaps

The current scraper implementation (`TimeAndDateHolidayScraper.cs`) is functionally complete for single-year fetches. Key gaps to address for this spec:

| Gap                                    | Current State                         | Required State                                  |
| -------------------------------------- | ------------------------------------- | ----------------------------------------------- |
| Merge vs. Replace                      | ViewModel clears all entries on fetch | Merge by date — preserve existing entries        |
| Multi-year support                     | Single year only                      | Loop through StartYear..EndYear range            |
| Progress feedback (multi-year)         | None                                  | Per-year progress indication                     |
| Partial failure reporting              | All-or-nothing error                  | Aggregate success/failure per year               |
| Command naming inconsistency           | `FetchFromWebCommand` declared but unused; `ImportCommand` used | Unify to `FetchFromWebCommand` |
| Validation count in success message    | No count shown                        | "Fetched N holidays for year YYYY"               |

---

## 5. HTTP Considerations

- **User-Agent**: The default `HttpClient` sends a standard .NET user agent. No custom headers needed for timeanddate.com.
- **Rate limiting**: Not observed for low-volume usage. For multi-year fetch (max 11 years), sequential requests with natural processing delay between each suffice.
- **Timeout**: Default `HttpClient` timeout (100 seconds) is adequate. Individual year fetches complete in 1–3 seconds.
- **SSL/TLS**: timeanddate.com serves over HTTPS. No certificate pinning needed.
- **Cancellation**: `CancellationToken` is threaded through to `HttpClient.GetStringAsync()` — user can cancel via UI if supported.
