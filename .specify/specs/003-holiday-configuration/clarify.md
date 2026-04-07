# Clarification Session: 003-holiday-configuration

**Date**: 2026-04-06  
**Feature**: Holiday Configuration  
**Status**: All ambiguities resolved

---

## Resolved Questions

### Q1 — Seeding re-trigger when user saves empty list
**Decision**: **Seed once on first run only** — determined by checking whether the `HolidayYearRange` singleton record exists in the DB. If no `HolidayYearRange` row exists, the app seeds current-year Serbian holidays and creates the range record. If the user deliberately saves an empty list, the empty list is respected; no re-seed occurs.  
**Rationale**: Re-seeding on empty silently overrides a deliberate user action.

### Q2 — Dirty-state on tab navigation
**Decision**: **No dirty-state warning**. If the user navigates away from the Holidays tab with unsaved changes, changes are silently discarded. The UI will show a simple "unsaved changes" label but no blocking dialog.  
**Rationale**: Simple desktop UX. The user can always re-open the tab and re-load.

### Q3 — Web import failure UX
**Decision**: Show an inline `ErrorMessage` string in the ViewModel (bound to a styled `TextBlock` in the view). No modal dialogs. If the HTTP request fails (timeout, DNS, HTTP error), display the error message and leave the current DataGrid contents unchanged.  
**Rationale**: Consistent with the pattern established in `ProfileSettingsViewModel`.

### Q4 — HTML parser library
**Decision**: Use **AngleSharp** (NuGet: `AngleSharp`, version 1.x). It is a pure .NET HTML5 parser with no native dependencies, cross-platform, and available under MIT licence. It provides CSS selector support needed to find table rows and filter out hidden elements.  
**Rationale**: AngleSharp is the most widely used HTML parser for .NET. HtmlAgilityPack is older and has LGPL licence concerns.

### Q5 — Year range validation
**Decision**: `StartYear >= 2020` and `EndYear <= StartYear + 10`. Validation in Domain (`HolidayYearRange` entity). Invalid range throws `DomainException`.

---

## Encoded Assumptions

| ID | Assumption |
|----|-----------|
| A-001 | `HolidayConf` value object in Domain remains unchanged. Persistence uses a new `PublicHoliday` entity. |
| A-002 | `PublicHoliday` entity: `Guid Id`, `DateOnly Date`, `string Name`, `int Year` (computed = Date.Year). Owned by Domain. |
| A-003 | `HolidayYearRange` entity: `Id = 1` (singleton), `int StartYear`, `int EndYear`. Seeded on first run. |
| A-004 | Seeding fires once: when `HolidayYearRange` row does not exist. Current-year holidays are seeded then. |
| A-005 | On save, ALL existing `PublicHoliday` rows are replaced (truncate + insert pattern). No soft-deletes. |
| A-006 | `IHolidayRepository` interface defined in Application with: `GetHolidayConfAsync`, `GetYearRangeAsync`, `SaveHolidaysAsync`. |
| A-007 | `IHolidayImporter` interface in Application: `ImportAsync(int year, CancellationToken) → Result<IReadOnlyList<HolidayEntryDto>, Error>`. |
| A-008 | `TimeAndDateHolidayScraper` in Infrastructure implements `IHolidayImporter`. Uses HttpClient + AngleSharp. Parses `https://www.timeanddate.com/holidays/serbia/{year}?hol=1`. Filters rows where the `<tr>` does not have `class="showrow"` hidden state — only visible rows included. |
| A-009 | Constitution amendment: `timeanddate.com` endpoint is an explicitly approved exception for user-initiated holiday import. The scraper is only called when the user clicks "Import from Web". No background polling. |
| A-010 | `GetHolidayConfQuery` returns `HolidayConfDto` with `IReadOnlyList<HolidayEntryDto>`, `int StartYear`, `int EndYear`. |
| A-011 | `SaveHolidayConfCommand` contains `IReadOnlyList<HolidayEntryDto> Holidays`, `int StartYear`, `int EndYear`. |
| A-012 | `HolidayEntryDto` record: `DateOnly Date`, `string Name`. |
| A-013 | UI DataGrid columns: `Date` (DateOnly, editable), `Name` (string, editable). Add button adds blank row. Delete button removes selected row. Import button prompts for year input, fires scraper, replaces DataGrid content. Save button persists to DB. |
| A-014 | No duplicate `DateOnly` values in HolidayConf. `HolidayConf` constructor validates this (new invariant). |
| A-015 | `HolidaySettingsViewModel` added to `SettingsViewModel` as `HolidayTab` property. `SettingsView.axaml` gets a new "Holidays" tab. |
| A-016 | AngleSharp added as `<PackageReference>` to `Rentier.Infrastructure.csproj` only. |
| A-017 | HttpClient for scraping registered in `InfrastructureServiceExtensions` via `services.AddHttpClient<TimeAndDateHolidayScraper>()`. |
| A-018 | Serbian public holiday seeds for current year: New Year (Jan 1, Jan 2), Sretenje (Feb 15, Feb 16), Labour Day (May 1, May 2), Vidovdan (Jun 28), Armistice Day (Nov 11), Christmas (Jan 7). |

---

## Constitution Amendment Note

> **CA-EXT-001**: The `IHolidayImporter` / `TimeAndDateHolidayScraper` introduces outbound HTTP access to `https://www.timeanddate.com` — a third-party site outside the currently approved endpoints (IMAP + NBS). This is approved as a **user-initiated, on-demand exception**. The scraper MUST NOT be called automatically. It is only invoked when the user explicitly clicks "Import from Web". This exception must be recorded in the constitution if/when amended.

---

## Functional Requirements Preview (FR-001 to FR-018)

| ID | Requirement |
|----|------------|
| FR-001 | Settings → Holidays tab shows all holidays for the configured year range |
| FR-002 | User can add a holiday row (Date + Name) |
| FR-003 | User can edit an existing holiday row inline |
| FR-004 | User can delete a holiday row |
| FR-005 | User can click Save to persist changes to SQLite |
| FR-006 | HolidayConf validates no duplicate DateOnly values |
| FR-007 | Year range (StartYear/EndYear) is configurable and validated |
| FR-008 | Data persists across app restarts |
| FR-009 | On first run with no holidays, app seeds current-year Serbian holidays |
| FR-010 | User can import holidays from web for a given year |
| FR-011 | Import loads holidays into DataGrid only — user must click Save to persist |
| FR-012 | Import failure shows inline error, does not modify DataGrid |
| FR-013 | Only visible rows from the web table are imported (hidden rows excluded) |
| FR-014 | GetHolidayConfQuery returns HolidayConfDto with holidays and year range |
| FR-015 | SaveHolidayConfCommand replaces all holidays and updates year range |
| FR-016 | All dates are DateOnly — no DateTime |
| FR-017 | No network calls except explicit user-triggered import |
| FR-018 | All user-visible strings in Strings.resx |
