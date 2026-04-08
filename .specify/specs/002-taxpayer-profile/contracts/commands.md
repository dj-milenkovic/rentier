# Contract: Commands — Taxpayer Profile Management (Feature 002)

**Generated**: 2026-04-06  
**Namespace**: `Rentier.Application.Commands` / `Rentier.Application.Handlers`

---

## `SaveTaxpayerProfileCommand`

### Definition

```csharp
/// <summary>
/// Upserts the taxpayer's profile. Inserts a new record if none exists;
/// updates the existing record in-place if one has already been saved.
/// </summary>
public sealed record SaveTaxpayerProfileCommand(
    string Jmbg,
    string FullName,
    string Address,
    string OpstinaCode,
    string? PhoneNumber,
    string? Email);
```

### Handler Interface

```csharp
public interface ICommandHandler<TCommand, TResult>
{
    Task<TResult> HandleAsync(TCommand command, CancellationToken ct = default);
}
```

### Concrete Handler

```csharp
public sealed class SaveTaxpayerProfileCommandHandler
    : ICommandHandler<SaveTaxpayerProfileCommand, Result<VoidResult, Error>>
{
    public SaveTaxpayerProfileCommandHandler(ITaxpayerProfileRepository repository) { ... }

    public async Task<Result<VoidResult, Error>> HandleAsync(
        SaveTaxpayerProfileCommand command,
        CancellationToken ct = default) { ... }
}
```

### Handler Contract

| Scenario | Input | Return |
|----------|-------|--------|
| First-time save (insert path) | All required fields valid; `GetAsync` returns `null` | `Result.Ok(VoidResult.Value)` |
| Edit existing (update path) | All required fields valid; `GetAsync` returns existing profile | `Result.Ok(VoidResult.Value)` |
| Domain validation failure | Invalid JMBG (non-13-digit, non-numeric) | `Result.Fail(new Error("DOMAIN_VALIDATION", "JMBG must be exactly 13 digit characters"))` |
| Domain validation failure | Required field null or whitespace | `Result.Fail(new Error("DOMAIN_VALIDATION", "FullName must not be null or whitespace"))` (or Address/OpstinaCode variant) |

### Upsert Logic

```
1. TaxpayerProfile entity = TaxpayerProfile(Guid.NewGuid(), command.Jmbg, ...)
   — catches DomainException → return Result.Fail
2. existing = await repository.GetAsync(ct)
3. if (existing is null) → keep new Guid; call repository.SaveAsync(entity, ct)
4. if (existing is not null) → reconstruct with existing.Id; call repository.SaveAsync(entity, ct)
5. return Result.Ok(VoidResult.Value)
```

### Invariants

- Handler MUST be called only from the Desktop ViewModel; never called from Domain or Infrastructure.
- The entity is constructed before the upsert decision; Domain invariants are enforced before any I/O.
- `repository.SaveAsync` performs the actual insert/update; the handler does not call EF Core directly.
- Raw JMBG values MUST NOT be written to any log output inside this handler.

### DI Registration

```csharp
services.AddTransient<
    ICommandHandler<SaveTaxpayerProfileCommand, Result<VoidResult, Error>>,
    SaveTaxpayerProfileCommandHandler>();
```

---

## `Result<T, TError>` and `Error` (Common Types)

```csharp
// Rentier.Application.Common
public sealed class Result<T, TError>
{
    public bool IsSuccess { get; }
    public T Value { get; }      // valid only when IsSuccess = true
    public TError Error { get; } // valid only when IsSuccess = false

    public static Result<T, TError> Ok(T value) => ...;
    public static Result<T, TError> Fail(TError error) => ...;
}

public sealed record Error(string Code, string Message);

public sealed record VoidResult
{
    public static readonly VoidResult Value = new();
    private VoidResult() { }
}
```
