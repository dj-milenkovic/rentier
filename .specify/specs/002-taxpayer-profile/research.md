# Research: Taxpayer Profile Management (Feature 002)

**Generated**: 2026-04-06  
**Status**: Complete — all NEEDS CLARIFICATION items resolved

---

## Decision 1: EF Core Entity Configuration Pattern

**Decision**: Use a separate `IEntityTypeConfiguration<TaxpayerProfile>` class
(`TaxpayerProfileConfiguration`) registered via
`modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly)` in `OnModelCreating`.

**Rationale**: Keeps `AppDbContext` clean and scalable. Each entity gets its own focused
configuration class rather than bloating `OnModelCreating` with multiple `modelBuilder.Entity<T>()`
blocks. This matches the EF Core recommended pattern for non-trivial projects and makes the
configuration easily discoverable.

**Alternatives Considered**:
- Inline `modelBuilder.Entity<T>()` calls in `OnModelCreating`: Rejected — does not scale as
  entities grow; mixes infrastructure concerns with context setup.
- Data Annotations: Rejected — violates Clean Architecture (would import EF attributes into Domain).

**Key configuration details**:
```csharp
builder.ToTable("TaxpayerProfiles");
builder.HasKey(e => e.Id);
builder.Property(e => e.Jmbg).IsRequired().HasMaxLength(13);
builder.HasIndex(e => e.Jmbg).IsUnique();
builder.Property(e => e.FullName).IsRequired().HasMaxLength(200);
builder.Property(e => e.Address).IsRequired().HasMaxLength(500);
builder.Property(e => e.OpstinaCode).IsRequired().HasMaxLength(10);
builder.Property(e => e.PhoneNumber).HasMaxLength(50);   // nullable
builder.Property(e => e.Email).HasMaxLength(254);        // nullable
```

---

## Decision 2: EF Core Materialization — Domain Entity with Get-Only Properties

**Decision**: Add a `private TaxpayerProfile()` parameterless constructor (EF-only) alongside
`HasField` / backing field mapping, OR use property setters with `private set`. The chosen approach
is **private parameterless constructor** + **property setters changed to `private set`**.

**Rationale**: `TaxpayerProfile` currently has `get`-only auto properties and a single public
constructor that enforces invariants. EF Core needs to materialize entities from the database without
going through the guard-clause constructor. The cleanest approach that preserves invariant
enforcement at the public API while allowing EF to materialize is:
1. Keep the public constructor with all validation.
2. Add `private TaxpayerProfile() {}` for EF materialization.
3. Change `public X { get; }` to `public X { get; private set; }` on all properties.

This is the standard C# EF Core pattern for DDD-style entities.

**Alternatives Considered**:
- Shadow properties / `HasField`: More complex, harder to read, not warranted for simple scalar
  properties.
- Owned types or value objects: Not needed; all profile fields are simple scalars.
- EF Core `UsePropertyAccessMode(PropertyAccessMode.Field)` with private backing fields: More
  ceremony, same result; private constructor approach is simpler.

---

## Decision 3: ReactiveUI `WhenActivated` Pattern for ViewModel Initialization

**Decision**: `ProfileSettingsViewModel.WhenActivated` loads the profile on view activation via
`GetTaxpayerProfileQueryHandler.HandleAsync`, dispatches the result to reactive properties on the
`MainThreadScheduler`.

**Rationale**: `WhenActivated` ensures load logic runs only while the view is attached to the visual
tree; it also handles subscription disposal automatically (prevents memory leaks). Using `IDisposable`
disposables collected in the `WhenActivated` block is the ReactiveUI-idiomatic approach.

**Pattern**:
```csharp
this.WhenActivated(disposables =>
{
    Observable.FromAsync(_ => _queryHandler.HandleAsync(new GetTaxpayerProfileQuery(), ct: default))
        .ObserveOn(RxApp.MainThreadScheduler)
        .Subscribe(result => { /* populate properties */ })
        .DisposeWith(disposables);
});
```

**Alternatives Considered**:
- Loading in the constructor: Rejected — constructors must not perform async I/O; blocks object
  graph construction in DI.
- Loading from code-behind `OnAttachedToVisualTree`: Rejected — puts view logic in code-behind,
  violates MVVM.

---

## Decision 4: `Result<T, Error>` Return Pattern

**Decision**: Application handlers return `Result<T, Error>` where:
- `Result<Unit, Error>` for `SaveTaxpayerProfileCommandHandler` (success = `Unit.Value`).
- `Result<TaxpayerProfileDto?, Error>` for `GetTaxpayerProfileQueryHandler` (null DTO = no profile saved).

**Rationale**: Matches the Application Layer Pattern in the constitution ("Handlers MUST return
`Result<T>` or `Result<T, Error>` for expected failures"). Avoids exception-based flow for expected
domain failures (e.g., validation error surfaced from command input). Domain `DomainException` from
the entity constructor is caught at the handler boundary and translated to an `Error` result.

**Error type**: Use a simple `sealed record Error(string Code, string Message)` in
`Rentier.Application.Common`. This avoids pulling in external Result libraries and keeps the
Application layer dependency-free.

**Result type**: A lightweight `Result<T, TError>` discriminated union in
`Rentier.Application.Common`:
```csharp
public sealed class Result<T, TError>
{
    public bool IsSuccess { get; }
    public T Value { get; }
    public TError Error { get; }
    public static Result<T, TError> Ok(T value) => ...;
    public static Result<T, TError> Fail(TError error) => ...;
}
```

**Alternatives Considered**:
- LanguageExt or FluentResults NuGet packages: Rejected — introduces external dependency for a
  thin use; simple in-house type sufficient at this stage.
- Throwing exceptions from handlers: Rejected — exceptions are not return values; validation failures
  are expected flows, not exceptional conditions.

---

## Decision 5: Settings Sub-Navigation — `TabControl`

**Decision**: `SettingsView` uses Avalonia's built-in `TabControl` with a single `TabItem` labelled
"Profile". Additional tab items (Mailbox, Importer) are reserved placeholders, NOT introduced in
this feature.

**Rationale**: Clarification Q4 confirmed `TabControl` is the chosen sub-navigation control.
Avalonia's `TabControl` with `FluentTheme` requires no custom styling. The Profile tab content
is a separate `ProfileSettingsView` `ReactiveUserControl<ProfileSettingsViewModel>` hosted as the
`TabItem` `Content`.

**Alternatives Considered**:
- `NavigationView` style sidebar: Over-engineered for two future tabs; deferred.
- Flat single-view (no tabs): Rejected — spec explicitly requires sub-navigation host (FR-009).

---

## Decision 6: JMBG Uniqueness — Defense in Depth

**Decision**: JMBG uniqueness is enforced at three layers:
1. **Domain constructor** — throws `DomainException` if not 13 numeric digits (does not enforce
   uniqueness across instances, only format).
2. **ViewModel validation** — JMBG reactive property observable fires `IObservable<bool>` that
   feeds `SaveCommand.CanExecute`; inline error shown before dispatch.
3. **Database unique index** — `HasIndex(e => e.Jmbg).IsUnique()` in Fluent API; EF migration
   generates `CREATE UNIQUE INDEX` in SQLite. Catches any accidental duplicate insert not caught
   by application logic.

**Rationale**: Singleton profile means only one JMBG can ever exist; the unique index is a safety
net for unanticipated code paths or direct database manipulation.

---

## Decision 7: `TaxpayerProfileRepository.SaveAsync` — Upsert via EF Tracking

**Decision**: `SaveAsync` checks whether the passed entity already has a tracked entry in the
`ChangeTracker`. If `GetAsync` was called first and returned an entity (which is tracked), the
handler reconstructs a new entity with the same `Id` and calls `context.Update(entity)`. If no
entity existed, it calls `context.Add(entity)`. `SaveChangesAsync` persists the result.

**Rationale**: The simplest correct EF Core upsert for a singleton entity. No need for `ExecuteUpdate`
or raw SQL; EF change tracking handles `INSERT` vs `UPDATE` based on `EntityState`.

**Implementation pattern**:
```csharp
public async Task SaveAsync(TaxpayerProfile profile, CancellationToken ct = default)
{
    var existing = await _context.TaxpayerProfiles
        .AsNoTracking()
        .FirstOrDefaultAsync(ct);
    if (existing is null)
        _context.TaxpayerProfiles.Add(profile);
    else
        _context.TaxpayerProfiles.Update(profile);
    await _context.SaveChangesAsync(ct);
}
```

**Alternatives Considered**:
- `ExecuteUpdate` / `ExecuteDelete` (EF 7+): More efficient for bulk ops; overkill for a singleton.
- `AddOrUpdate` via identity resolution: Requires careful handling; the `AsNoTracking` + explicit
  `Add`/`Update` pattern is more transparent and testable.

---

## Resolved Clarifications Summary

| # | Question | Resolution |
|---|----------|------------|
| 1 | New nullable fields on domain entity? | Yes — `PhoneNumber?` and `Email?`, no format validation v1 |
| 2 | Strict singleton? | Yes — `GetAsync()` returns `TaxpayerProfile?`; null on first run |
| 3 | Optional field validation contract? | Free-text nullable; no regex/length validation in v1 |
| 4 | Settings navigation approach? | `TabControl` with Profile tab; other tabs deferred |
| 5 | Insert vs. update distinction? | Handler checks `GetAsync` result; same-Id reconstruct for update |
| 6 | Profile deletion in UI? | Out of scope; `DeleteAsync` retained on interface only |
| 7 | JMBG uniqueness at persistence? | Fluent API unique index + EF migration `UNIQUE` constraint |
