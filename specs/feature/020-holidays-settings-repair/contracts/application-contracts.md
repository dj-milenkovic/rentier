# Application Contracts: Holidays Settings Repair

**Feature**: 020-holidays-settings-repair
**Date**: 2025-07-15

---

## IHolidayImporter Interface

**Location**: `Rentier.Application.Interfaces.IHolidayImporter`
**Implemented by**: `Rentier.Infrastructure.Scraping.TimeAndDateHolidayScraper`
**Registered via**: `services.AddHttpClient<IHolidayImporter, TimeAndDateHolidayScraper>()`

### Contract (Unchanged)

```csharp
public interface IHolidayImporter
{
    /// <summary>
    /// Imports national holidays for the specified year from an external web source.
    /// MUST NOT be called automatically — only on explicit user action (CA-EXT-001).
    /// </summary>
    /// <param name="year">Calendar year to import (2020–2099).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// Success: list of HolidayEntryDto with Date and Name for each national holiday.
    /// Failure: Error with one of the defined error codes.
    /// </returns>
    Task<Result<IReadOnlyList<HolidayEntryDto>, Error>> ImportAsync(
        int year,
        CancellationToken cancellationToken = default);
}
```

### Error Codes (Updated per FR-010)

| Code | Condition | HTTP Analogy |
|------|-----------|--------------|
| `HOLIDAY_IMPORT_FAILED` | Network error: `HttpRequestException`, `TaskCanceledException` | 502/503 |
| `HOLIDAY_PARSE_ERROR` | HTML structure unrecognizable: missing `#holidays-table`, malformed DOM | 422 |
| `HOLIDAY_NOT_FOUND` | Table parsed successfully but zero national holiday rows found | 404 |

### Behavioral Contract

1. On success, returns only **national holiday** rows (filtered by `showrow` class and "National Holiday" type text).
2. Holiday dates are `DateOnly` with year from the `year` parameter, month+day from HTML content.
3. Holiday names are extracted from anchor text within the name column, stripping HTML entities.
4. Duplicate dates from the source (e.g., "1 May" appears for both "Labor holiday" and "Easter Day") are returned as-is — deduplication is the caller's responsibility.
5. The method MUST be async and MUST NOT block the calling thread.

---

## ImportHolidaysFromWebCommand

**Location**: `Rentier.Application.Commands.ImportHolidaysFromWebCommand`

```csharp
public sealed record ImportHolidaysFromWebCommand(int Year);
```

### Handler: ImportHolidaysFromWebCommandHandler

**Contract**: Delegates directly to `IHolidayImporter.ImportAsync(cmd.Year, ct)`.

| Input | Output |
|-------|--------|
| `ImportHolidaysFromWebCommand(Year)` | `Result<IReadOnlyList<HolidayEntryDto>, Error>` |

No additional business logic in the handler — it's a thin orchestration layer between Desktop and Infrastructure.

---

## SaveHolidayConfCommand

**Location**: `Rentier.Application.Commands.SaveHolidayConfCommand`

```csharp
public sealed record SaveHolidayConfCommand(
    IReadOnlyList<HolidayEntryDto> Holidays,
    int StartYear,
    int EndYear);
```

### Handler: SaveHolidayConfCommandHandler

| Input | Output |
|-------|--------|
| `SaveHolidayConfCommand(Holidays, StartYear, EndYear)` | `Result<VoidResult, Error>` |

### Validation Contract

1. Year range is validated by constructing `HolidayYearRange(StartYear, EndYear)` — `DomainException` → `INVALID_YEAR_RANGE` error.
2. Holidays are checked for duplicate dates — duplicates → `DUPLICATE_DATES` error.
3. On success, **all existing holidays are replaced** (delete-all + insert-all, not upsert).

---

## GetHolidayConfQuery

**Location**: `Rentier.Application.Queries.GetHolidayConfQuery`

```csharp
public sealed record GetHolidayConfQuery();
```

### Handler: GetHolidayConfQueryHandler

| Input | Output |
|-------|--------|
| `GetHolidayConfQuery()` | `Result<HolidayConfDto, Error>` |

### Behavioral Contract

1. If no year range exists in the database (first run), **seeds default Serbian holidays** for the current year and a year range of `[currentYear, currentYear + 3]`.
2. Returns all holidays sorted by date ascending.
3. Always returns `Success` — seeding guarantees data exists.

---

## IHolidayRepository Interface

**Location**: `Rentier.Application.Interfaces.IHolidayRepository`

```csharp
public interface IHolidayRepository
{
    Task<HolidayConfDto> GetHolidayConfAsync(CancellationToken ct = default);
    Task<HolidayYearRange?> GetYearRangeAsync(CancellationToken ct = default);
    Task SaveHolidaysAsync(
        IReadOnlyList<PublicHoliday> holidays,
        HolidayYearRange yearRange,
        CancellationToken ct = default);
}
```

### Contract (Unchanged)

- `GetHolidayConfAsync`: Returns all holidays + year range. Holidays sorted by date ascending.
- `GetYearRangeAsync`: Returns singleton year range or `null` if not yet configured.
- `SaveHolidaysAsync`: Atomic replace — deletes all existing holidays, inserts new list, upserts year range.

---

## Desktop Layer Contracts

### DateOnlyToStringConverter (New)

**Location**: `Rentier.Desktop.Converters.DateOnlyToStringConverter`

```csharp
public sealed class DateOnlyToStringConverter : IValueConverter
{
    public static readonly DateOnlyToStringConverter Instance = new();

    // Convert: DateOnly → "yyyy-MM-dd"
    // ConvertBack: "yyyy-MM-dd" string → DateOnly, or BindingNotification error
}
```

### HolidaySettingsViewModel State Contract

| Property | Type | Trigger |
|----------|------|---------|
| `IsLoading` | `bool` | `true` during any async operation (load, save, import); `false` after completion |
| `ErrorMessage` | `string?` | Set on failure; cleared before each new operation |
| `SuccessMessage` | `string?` | Set on success; cleared before each new operation |
| `HasUnsavedChanges` | `bool` | `true` after add/delete/edit/import; `false` after save or initial load |
| `ImportYear` | `int` | User-controlled; bound to new NumericUpDown in toolbar |
| `Entries` | `ObservableCollection<HolidayEntryViewModel>` | Bound to DataGrid |
| `SelectedEntry` | `HolidayEntryViewModel?` | Bound to DataGrid.SelectedItem |

### Command Enablement

| Command | Enabled When |
|---------|-------------|
| `AddRowCommand` | `!IsLoading` |
| `DeleteRowCommand` | `!IsLoading && SelectedEntry != null` |
| `SaveCommand` | `!IsLoading` |
| `ImportCommand` | `!IsLoading` |

---

## Error Flow Diagram

```text
User clicks Import
    │
    ▼
ImportCommand (ViewModel)
    │ IsLoading = true
    │ ErrorMessage = null
    │ SuccessMessage = null
    ▼
ImportHolidaysFromWebCommandHandler
    │
    ▼
IHolidayImporter.ImportAsync(year)
    │
    ├─ HttpRequestException ──→ Error("HOLIDAY_IMPORT_FAILED", message)
    │                                │
    ├─ HTML parse failure ────→ Error("HOLIDAY_PARSE_ERROR", message)
    │                                │
    ├─ Zero results ──────────→ Error("HOLIDAY_NOT_FOUND", message)
    │                                │
    │                                ▼
    │                          ViewModel: ErrorMessage = prefix + error.Message
    │                          IsLoading = false
    │                          Grid: unchanged (preserved)
    │
    └─ Success(List<Dto>) ───→ ViewModel: Entries.Clear() + Add imported
                               HasUnsavedChanges = true
                               IsLoading = false
```
