# Data Model: Language Selection

## Entities

### UserPreference (NEW)

A generic key-value user preference stored in SQLite. Designed for reuse across future preference types (language is the first consumer).

| Field | Type | Constraints | Description |
|-------|------|-------------|-------------|
| `Key` | `string` | **PK**, required, max 100 chars | Unique preference identifier (e.g., `"Language"`) |
| `Value` | `string` | Required, max 500 chars | Preference value (e.g., `"en"`, `"sr-Latn"`) |

**Domain rules**:
- `Key` must not be null, empty, or whitespace.
- `Key` must not exceed 100 characters.
- `Value` must not be null (empty string is allowed for "reset to default" semantics).
- `Value` must not exceed 500 characters.
- Invalid construction throws `DomainException`.

**Entity implementation pattern** (matches existing entities):

```csharp
namespace Rentier.Domain.Entities;

public sealed class UserPreference
{
    public string Key { get; private set; } = string.Empty;
    public string Value { get; private set; } = string.Empty;

    // Private constructor for EF Core materialization
    private UserPreference() { }

    public UserPreference(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new DomainException("Preference key must not be empty.");
        if (key.Length > 100)
            throw new DomainException("Preference key must not exceed 100 characters.");
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length > 500)
            throw new DomainException("Preference value must not exceed 500 characters.");

        Key = key.Trim();
        Value = value;
    }

    public void UpdateValue(string newValue)
    {
        ArgumentNullException.ThrowIfNull(newValue);
        if (newValue.Length > 500)
            throw new DomainException("Preference value must not exceed 500 characters.");
        Value = newValue;
    }
}
```

**Design notes**:
- Uses `string Key` as PK (not Guid) — preference keys are well-known constants, not generated.
- Private setters for DDD immutability.
- `UpdateValue()` method allows value mutation while enforcing constraints.
- No state machine — preferences are simple read/write.

## Database Schema

### UserPreferences Table (NEW)

```sql
CREATE TABLE UserPreferences (
    Key   TEXT NOT NULL PRIMARY KEY,
    Value TEXT NOT NULL
);
```

### EF Core Configuration

```csharp
namespace Rentier.Infrastructure.Persistence.Configurations;

public sealed class UserPreferenceConfiguration : IEntityTypeConfiguration<UserPreference>
{
    public void Configure(EntityTypeBuilder<UserPreference> builder)
    {
        builder.ToTable("UserPreferences");
        builder.HasKey(p => p.Key);
        builder.Property(p => p.Key).IsRequired().HasMaxLength(100);
        builder.Property(p => p.Value).IsRequired().HasMaxLength(500);
    }
}
```

### Migration

A new EF Core migration adds the `UserPreferences` table. No seed data — preferences are created on first use.

## Well-Known Preference Keys

| Key | Valid Values | Default | Description |
|-----|-------------|---------|-------------|
| `"Language"` | `"en"`, `"sr-Latn"` | `"sr-Latn"` | Application display language |

*Future expansion (out of scope for this feature):*
| Key | Valid Values | Default | Description |
|-----|-------------|---------|-------------|
| `"Theme"` | `"System"`, `"Light"`, `"Dark"` | `"System"` | Could migrate from `ui.json` |

## Repository Interface

```csharp
namespace Rentier.Application.Repositories;

public interface IUserPreferenceRepository
{
    Task<UserPreference?> GetAsync(string key, CancellationToken ct = default);
    Task SaveAsync(UserPreference preference, CancellationToken ct = default);
}
```

**Pattern**: Matches existing repository interfaces (e.g., `ITaxpayerProfileRepository`):
- Async with `CancellationToken`
- Nullable return for "not found"
- Upsert semantics in `SaveAsync` (insert if new, update if exists)

## CQRS Commands & Queries

### GetUserPreferenceQuery

```csharp
namespace Rentier.Application.Queries;

public sealed record GetUserPreferenceQuery(string Key);
```

**Handler returns**: `Result<string?, Error>` — the preference value or null if not found. Null means "use default".

### SetUserPreferenceCommand

```csharp
namespace Rentier.Application.Commands;

public sealed record SetUserPreferenceCommand(string Key, string Value);
```

**Handler returns**: `Result<VoidResult, Error>` — success or infrastructure error.

## Relationships

```text
UserPreference (standalone)
  └── No FK relationships
  └── No navigation properties
  └── Referenced by Application handlers only
  └── Read at startup by App.axaml.cs (via ILocalizationService)
  └── Written by AppearanceSettingsViewModel (via command handler)
```

## State Transitions

N/A — `UserPreference` has no state machine. It is a simple read/write store.
