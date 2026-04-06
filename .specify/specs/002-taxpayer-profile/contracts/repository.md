# Contract: Repository — `ITaxpayerProfileRepository` (Feature 002)

**Generated**: 2026-04-06  
**Namespace**: `Rentier.Application.Repositories`  
**Status**: Interface already exists; documented here for usage reference.

---

## Interface Definition (existing)

```csharp
// src/Rentier.Application/Repositories/ITaxpayerProfileRepository.cs

using Rentier.Domain.Entities;

namespace Rentier.Application.Repositories;

public interface ITaxpayerProfileRepository
{
    /// <summary>
    /// Returns the single saved TaxpayerProfile, or null if none has been saved.
    /// </summary>
    Task<TaxpayerProfile?> GetAsync(CancellationToken ct = default);

    /// <summary>
    /// Persists the given TaxpayerProfile (insert if new, update if existing by Id).
    /// </summary>
    Task SaveAsync(TaxpayerProfile profile, CancellationToken ct = default);

    /// <summary>
    /// Deletes the saved TaxpayerProfile. Reserved for future maintenance UI.
    /// NOT surfaced in the Settings UI in this feature version.
    /// </summary>
    Task DeleteAsync(CancellationToken ct = default);
}
```

---

## Method Contracts

### `GetAsync`

| Concern | Detail |
|---------|--------|
| **Returns** | `TaxpayerProfile?` — null if no profile has ever been saved |
| **Side effects** | Read-only; no state mutation |
| **Threading** | Must be `await`-ed; never call `.Result` |
| **Implementation** | `FirstOrDefaultAsync` on `DbSet<TaxpayerProfile>` with `AsNoTracking` for read path |
| **Callers** | `GetTaxpayerProfileQueryHandler`, `SaveTaxpayerProfileCommandHandler` (upsert check) |

### `SaveAsync`

| Concern | Detail |
|---------|--------|
| **Behaviour** | Upsert: insert when no prior row exists; update existing row in-place by `Id` |
| **Singleton guarantee** | Only one row must ever exist; Application layer is authoritative (checks `GetAsync` before deciding insert vs. update) |
| **Threading** | Must be `await`-ed; never call `.Result` |
| **Implementation** | `AsNoTracking` read to check existence → `Add` or `Update` → `SaveChangesAsync` |
| **Callers** | `SaveTaxpayerProfileCommandHandler` only |

### `DeleteAsync`

| Concern | Detail |
|---------|--------|
| **Behaviour** | Deletes the single profile row if one exists; no-op if the table is empty |
| **UI exposure** | NOT surfaced in the Settings UI in this feature; reserved for a future maintenance feature |
| **Callers** | None in this feature (method exists on interface for forward-compatibility) |

---

## Infrastructure Implementation: `TaxpayerProfileRepository`

**Namespace**: `Rentier.Infrastructure.Repositories`  
**File**: `src/Rentier.Infrastructure/Repositories/TaxpayerProfileRepository.cs`  
**Status**: NEW in this feature

```csharp
public sealed class TaxpayerProfileRepository : ITaxpayerProfileRepository
{
    private readonly AppDbContext _context;

    public TaxpayerProfileRepository(AppDbContext context) => _context = context;

    public async Task<TaxpayerProfile?> GetAsync(CancellationToken ct = default)
        => await _context.TaxpayerProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);

    public async Task SaveAsync(TaxpayerProfile profile, CancellationToken ct = default)
    {
        var exists = await _context.TaxpayerProfiles
            .AsNoTracking()
            .AnyAsync(ct);
        if (!exists)
            _context.TaxpayerProfiles.Add(profile);
        else
            _context.TaxpayerProfiles.Update(profile);
        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(CancellationToken ct = default)
    {
        await _context.TaxpayerProfiles.ExecuteDeleteAsync(ct);
    }
}
```

### DI Registration (in `CompositionRoot`)

```csharp
services.AddScoped<ITaxpayerProfileRepository, TaxpayerProfileRepository>();
```

> **Note**: `Scoped` lifetime is appropriate because `AppDbContext` is typically registered as
> `Scoped` in EF Core DI patterns; the repository shares the same lifetime as the DbContext.

---

## Architecture Boundary Reminder

```
Desktop layer (SettingsViewModel / ProfileSettingsViewModel)
    │  calls via DI
    ▼
Application handlers (SaveTaxpayerProfileCommandHandler, GetTaxpayerProfileQueryHandler)
    │  calls via interface
    ▼
ITaxpayerProfileRepository (Application layer — interface only)
    │  implemented by
    ▼
TaxpayerProfileRepository (Infrastructure layer)
    │  uses
    ▼
AppDbContext → SQLite
```

**The Desktop layer MUST NOT reference `TaxpayerProfileRepository` or `AppDbContext` directly.**  
All access flows through the Application handler interfaces injected by the DI container.
