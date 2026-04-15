# Data Model: Holiday Fetcher — timeanddate.com Scraper

**Feature Branch**: `026-holiday-web-scraper`  
**Created**: 2025-07-18

## Summary

This feature requires **no database schema changes**. The existing data model fully supports the holiday web fetcher. All changes are behavioral (merge logic, multi-year iteration) in the Application and Desktop layers.

---

## Existing Entities (No Changes Required)

### PublicHoliday (Domain Entity)

| Field | Type     | Constraints                     | Notes                    |
| ----- | -------- | ------------------------------- | ------------------------ |
| Id    | Guid     | Primary key, generated on create | Unique identifier        |
| Date  | DateOnly | Required                        | The holiday calendar date |
| Name  | string   | Required, max 200 characters    | Display name              |
| Year  | int      | Derived from Date               | Indexed for query performance |

**Source**: `src/Rentier.Domain/Entities/PublicHoliday.cs`

**Factory method**: `PublicHoliday.Create(DateOnly date, string name)` — enforces invariants (non-empty name, valid date).

### HolidayYearRange (Domain Entity — Singleton)

| Field     | Type | Constraints                        | Notes                     |
| --------- | ---- | ---------------------------------- | ------------------------- |
| Id        | int  | Always = 1 (singleton)             | Fixed identifier          |
| StartYear | int  | ≥ 2020                             | Lower bound of range      |
| EndYear   | int  | ≤ StartYear + 10, ≥ StartYear      | Upper bound of range      |

**Source**: `src/Rentier.Domain/Entities/HolidayYearRange.cs`

**Relevance to fetcher**: Defines the scope for multi-year fetch operations (StartYear through EndYear inclusive).

### HolidayConf (Domain Value Object)

| Field    | Type                      | Notes                                          |
| -------- | ------------------------- | ---------------------------------------------- |
| Holidays | IReadOnlyList\<DateOnly\> | Flat list of dates used by deadline calculator  |

**Source**: `src/Rentier.Domain/ValueObjects/HolidayConf.cs`

**Relevance to fetcher**: Downstream consumer — after holidays are saved, this value object is loaded by the filing deadline calculator to determine business days.

---

## Existing DTOs (No Changes Required)

### HolidayEntryDto (Application Layer)

```
record HolidayEntryDto(DateOnly Date, string Name)
```

The scraper returns `IReadOnlyList<HolidayEntryDto>` — this is the data transfer format between Infrastructure (scraper) and Desktop (ViewModel).

### HolidayConfDto (Application Layer)

```
record HolidayConfDto(IReadOnlyList<HolidayEntryDto> Holidays, int StartYear, int EndYear)
```

Used by the query handler to load the full holiday configuration. Not directly involved in the fetch operation.

---

## Database Schema (No Migration Needed)

### Table: PublicHolidays

```sql
CREATE TABLE PublicHolidays (
    Id   TEXT NOT NULL PRIMARY KEY,  -- Guid as TEXT
    Date TEXT NOT NULL,              -- DateOnly serialized
    Name TEXT NOT NULL,              -- Max 200 chars (enforced by EF config)
    Year INTEGER NOT NULL            -- Derived, indexed
);

CREATE INDEX IX_PublicHolidays_Year ON PublicHolidays (Year);
```

### Table: HolidayYearRange

```sql
CREATE TABLE HolidayYearRange (
    Id        INTEGER NOT NULL PRIMARY KEY,  -- Always 1
    StartYear INTEGER NOT NULL,
    EndYear   INTEGER NOT NULL
);
```

**Migration**: `20260406142730_0003_HolidayConfiguration.cs` — already applied.

---

## Data Flow: Fetch → Merge → Save

```
User clicks "Fetch from web"
       │
       ▼
  ┌─────────────────────────┐
  │ Desktop Layer            │
  │ FetchFromWebCommand      │
  │ (ReactiveCommand)        │
  │                          │
  │ For each year in range:  │
  │   ▼                      │
  │ ImportHolidaysFromWeb    │
  │   Command(year)          │
  └──────────┬───────────────┘
             │
             ▼
  ┌─────────────────────────┐
  │ Application Layer        │
  │ CommandHandler            │
  │ delegates to             │
  │ IHolidayImporter         │
  └──────────┬───────────────┘
             │
             ▼
  ┌─────────────────────────┐
  │ Infrastructure Layer     │
  │ TimeAndDateHolidayScraper│
  │                          │
  │ HTTP GET → HTML parse    │
  │ → filter National Holiday│
  │ → parse dates (DateOnly) │
  │ → return List<DTO>       │
  └──────────┬───────────────┘
             │
             ▼
  ┌─────────────────────────┐
  │ Desktop Layer (merge)    │
  │                          │
  │ For each fetched DTO:    │
  │   if date NOT in Entries │
  │     → Add to Entries     │
  │   else                   │
  │     → Skip (de-dup)      │
  │                          │
  │ Mark HasUnsavedChanges   │
  └──────────┬───────────────┘
             │
        User clicks "Save"
             │
             ▼
  ┌─────────────────────────┐
  │ Application Layer        │
  │ SaveHolidayConfCommand   │
  │                          │
  │ Truncate + Insert all    │
  │ entries to SQLite        │
  └─────────────────────────┘
```

---

## Merge Logic Detail

The key behavioral change is in the ViewModel. The current implementation replaces:

```
// CURRENT (replace all):
Entries.Clear();
foreach (var dto in result.Value) 
    Entries.Add(HolidayEntryViewModel.FromDto(dto));
```

The new merge-by-date behavior:

```
// NEW (merge by date):
var existingDates = Entries.Select(e => e.Date).ToHashSet();
var added = 0;
foreach (var dto in fetchedResults)
{
    if (!existingDates.Contains(dto.Date))
    {
        Entries.Add(HolidayEntryViewModel.FromDto(dto));
        added++;
    }
}
// Report: "Added {added} new holidays"
```

De-duplication is by `DateOnly` value — this is correct because:
1. A calendar date either is or isn't a holiday (binary for deadline calculations)
2. Users may have customized names for existing entries
3. Manual entries should never be silently overwritten
