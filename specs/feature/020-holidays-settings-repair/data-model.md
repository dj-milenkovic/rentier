# Data Model: Holidays Settings Repair

**Feature**: 020-holidays-settings-repair
**Date**: 2025-07-15

---

## Schema Changes

**No database schema changes required.** All existing entities and tables remain as-is.

---

## Existing Entities (Reference)

### PublicHoliday (Entity)

**Location**: `Rentier.Domain.Entities.PublicHoliday`
**Table**: `PublicHolidays`

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| `Id` | `Guid` | PK | Auto-generated on `Create()` |
| `Date` | `DateOnly` | Required | Constitution III: all business dates use `DateOnly` |
| `Name` | `string` | Required, non-empty | Validated in constructor |
| `Year` | `int` | Derived from `Date.Year` | Computed, not independently settable |

**Factory method**: `PublicHoliday.Create(DateOnly date, string name)`
**Invariant**: Name must not be null or whitespace (throws `DomainException`).

---

### HolidayYearRange (Entity — Singleton)

**Location**: `Rentier.Domain.Entities.HolidayYearRange`
**Table**: `HolidayYearRange`

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| `Id` | `int` | PK, always `1` | Singleton pattern (const `SingletonId = 1`) |
| `StartYear` | `int` | ≥ 2020 | Min enforced in constructor |
| `EndYear` | `int` | ≥ StartYear, ≤ StartYear + 10 | Max span 10 years |

**Invariants**:
- `StartYear >= 2020`
- `EndYear <= StartYear + 10`
- `EndYear >= StartYear`

All violations throw `DomainException`.

---

### HolidayConf (Value Object)

**Location**: `Rentier.Domain.ValueObjects.HolidayConf`

| Field | Type | Notes |
|-------|------|-------|
| `Holidays` | `IReadOnlyList<DateOnly>` | Used for deadline calculations only |

**Note**: This value object aggregates holiday dates for domain logic (filing deadline calculation). It is populated from `PublicHoliday` entities but does not carry names. Not persisted directly.

---

## DTOs (Application Layer)

### HolidayEntryDto

**Location**: `Rentier.Application.DTOs.HolidayEntryDto`

```csharp
public sealed record HolidayEntryDto(DateOnly Date, string Name);
```

Used for:
- Import results from `IHolidayImporter`
- Save command payload
- Query response payload

### HolidayConfDto

**Location**: `Rentier.Application.DTOs.HolidayConfDto`

```csharp
public sealed record HolidayConfDto(
    IReadOnlyList<HolidayEntryDto> Holidays,
    int StartYear,
    int EndYear);
```

Used for: Full configuration response from `GetHolidayConfQuery`.

---

## ViewModels (Desktop Layer)

### HolidayEntryViewModel

**Location**: `Rentier.Desktop.ViewModels.HolidayEntryViewModel`

| Property | Type | Binding | Notes |
|----------|------|---------|-------|
| `Date` | `DateOnly` | Two-way (via converter) | Requires `DateOnlyToStringConverter` for DataGrid editing |
| `Name` | `string` | Two-way | Standard text binding |

**Mapping**:
- `FromDto(HolidayEntryDto)` → creates ViewModel from DTO
- `ToDto()` → creates DTO from ViewModel

---

## Entity Relationship Diagram

```text
┌─────────────────────┐     ┌──────────────────────┐
│   PublicHoliday      │     │  HolidayYearRange     │
├─────────────────────┤     ├──────────────────────┤
│ Id: Guid (PK)       │     │ Id: int (PK, =1)     │
│ Date: DateOnly      │     │ StartYear: int       │
│ Name: string        │     │ EndYear: int         │
│ Year: int (derived) │     └──────────────────────┘
└─────────────────────┘         (singleton)

         ↓ projected to              ↓ included in
┌─────────────────────┐     ┌──────────────────────┐
│  HolidayEntryDto    │────→│  HolidayConfDto       │
├─────────────────────┤     ├──────────────────────┤
│ Date: DateOnly      │     │ Holidays: List<Dto>  │
│ Name: string        │     │ StartYear: int       │
└─────────────────────┘     │ EndYear: int         │
                            └──────────────────────┘
```

---

## State Transitions

This feature does not introduce new domain state machines. The existing `PublicHoliday` entity is a simple CRUD entity without status transitions.

**Holiday data lifecycle**:
```
[Empty DB] → GetHolidayConf → [Seeded defaults]
                                    ↓
[User edits grid] → SaveHolidayConf → [Persisted]
                                    ↓
[User imports]   → ImportFromWeb → [Grid populated, unsaved]
                                    ↓
                  → SaveHolidayConf → [Persisted]
```
