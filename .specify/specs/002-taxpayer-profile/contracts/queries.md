# Contract: Queries — Taxpayer Profile Management (Feature 002)

**Generated**: 2026-04-06  
**Namespace**: `Rentier.Application.Queries` / `Rentier.Application.Handlers`

---

## `GetTaxpayerProfileQuery`

### Definition

```csharp
/// <summary>
/// Retrieves the single saved taxpayer profile, or null if none has been saved yet.
/// </summary>
public sealed record GetTaxpayerProfileQuery();
```

### Handler Interface

```csharp
public interface IQueryHandler<TQuery, TResult>
{
    Task<TResult> HandleAsync(TQuery query, CancellationToken ct = default);
}
```

### Concrete Handler

```csharp
public sealed class GetTaxpayerProfileQueryHandler
    : IQueryHandler<GetTaxpayerProfileQuery, Result<TaxpayerProfileDto?, Error>>
{
    public GetTaxpayerProfileQueryHandler(ITaxpayerProfileRepository repository) { ... }

    public async Task<Result<TaxpayerProfileDto?, Error>> HandleAsync(
        GetTaxpayerProfileQuery query,
        CancellationToken ct = default) { ... }
}
```

### Handler Contract

| Scenario | Condition | Return |
|----------|-----------|--------|
| No profile saved | `GetAsync` returns `null` | `Result.Ok<TaxpayerProfileDto?>(null)` |
| Profile exists | `GetAsync` returns `TaxpayerProfile` | `Result.Ok(new TaxpayerProfileDto(...))` |

### Mapping Logic

```
profile = await repository.GetAsync(ct)
if (profile is null) → return Result.Ok<TaxpayerProfileDto?>(null)
dto = new TaxpayerProfileDto(
    profile.Id,
    profile.Jmbg,
    profile.FullName,
    profile.Address,
    profile.OpstinaCode,
    profile.PhoneNumber,
    profile.Email)
return Result.Ok(dto)
```

### Invariants

- Handler MUST be called only from the Desktop ViewModel (via `WhenActivated` to populate the form).
- No write operations occur in this handler.
- The returned DTO is a value-copy; modifications to DTO properties do not affect the domain entity.
- Raw JMBG values in the returned DTO MUST NOT be logged in this handler.

### DI Registration

```csharp
services.AddTransient<
    IQueryHandler<GetTaxpayerProfileQuery, Result<TaxpayerProfileDto?, Error>>,
    GetTaxpayerProfileQueryHandler>();
```

---

## `TaxpayerProfileDto`

```csharp
// Rentier.Application.DTOs
public sealed record TaxpayerProfileDto(
    Guid Id,
    string Jmbg,
    string FullName,
    string Address,
    string OpstinaCode,
    string? PhoneNumber,
    string? Email);
```

**Usage in ViewModel (`ProfileSettingsViewModel.WhenActivated`)**:

```csharp
this.WhenActivated(disposables =>
{
    Observable
        .FromAsync(ct => _queryHandler.HandleAsync(new GetTaxpayerProfileQuery(), ct))
        .ObserveOn(RxApp.MainThreadScheduler)
        .Subscribe(result =>
        {
            if (result.IsSuccess && result.Value is { } dto)
            {
                Jmbg = dto.Jmbg;
                FullName = dto.FullName;
                Address = dto.Address;
                OpstinaCode = dto.OpstinaCode;
                PhoneNumber = dto.PhoneNumber ?? string.Empty;
                Email = dto.Email ?? string.Empty;
            }
        })
        .DisposeWith(disposables);
});
```
