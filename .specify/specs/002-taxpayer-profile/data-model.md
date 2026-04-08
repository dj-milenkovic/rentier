# Data Model: Taxpayer Profile Management (Feature 002)

**Generated**: 2026-04-06  
**Layer**: Domain + Application

---

## Domain Entity: `TaxpayerProfile`

**Namespace**: `Rentier.Domain.Entities`  
**Kind**: Sealed class (entity, not value object — has identity)  
**Invariant note**: At most one instance exists per database. Singleton enforced by Application layer.

### Fields

| Property | Type | Required | Constraint | Notes |
|----------|------|----------|------------|-------|
| `Id` | `Guid` | ✅ Required | Non-empty GUID | System-generated on first insert; preserved on updates |
| `Jmbg` | `string` | ✅ Required | Exactly 13 numeric characters | Primary identity key; unique across table; no whitespace |
| `FullName` | `string` | ✅ Required | Non-null, non-empty, non-whitespace | Taxpayer's full legal name |
| `Address` | `string` | ✅ Required | Non-null, non-empty, non-whitespace | Street address |
| `OpstinaCode` | `string` | ✅ Required | Non-null, non-empty, non-whitespace | Serbian municipality code |
| `PhoneNumber` | `string?` | ❌ Optional | May be null or empty string | Free-text, no format validation in v1 |
| `Email` | `string?` | ❌ Optional | May be null or empty string | Free-text, no format validation in v1 |

### Constructor Invariants

All invariants are enforced in the public constructor. Violations throw `DomainException`.

```
JMBG:
  - IsNullOrWhiteSpace → DomainException("JMBG must be exactly 13 digit characters")
  - Length != 13       → DomainException("JMBG must be exactly 13 digit characters")
  - !All(char.IsDigit) → DomainException("JMBG must be exactly 13 digit characters")

FullName:
  - IsNullOrWhiteSpace → DomainException("FullName must not be null or whitespace")

Address:
  - IsNullOrWhiteSpace → DomainException("Address must not be null or whitespace")

OpstinaCode:
  - IsNullOrWhiteSpace → DomainException("OpstinaCode must not be null or whitespace")

PhoneNumber:  no validation (nullable free-text)
Email:        no validation (nullable free-text)
```

### Modified Signature (after this feature)

```csharp
public sealed class TaxpayerProfile
{
    public Guid Id { get; private set; }
    public string Jmbg { get; private set; }
    public string FullName { get; private set; }
    public string Address { get; private set; }
    public string OpstinaCode { get; private set; }
    public string? PhoneNumber { get; private set; }   // NEW
    public string? Email { get; private set; }         // NEW

    // EF Core materialization constructor (private)
    private TaxpayerProfile() { }

    // Public constructor — enforces all invariants
    public TaxpayerProfile(
        Guid id,
        string jmbg,
        string fullName,
        string address,
        string opstinaCode,
        string? phoneNumber = null,   // NEW (optional)
        string? email = null)         // NEW (optional)
    { ... }
}
```

### JMBG Boundary Test Cases

| Input | Length | Numeric | Expected |
|-------|--------|---------|----------|
| `"1234567890123"` | 13 | ✅ | ✅ Valid |
| `"123456789012"` | 12 | ✅ | ❌ `DomainException` |
| `"12345678901234"` | 14 | ✅ | ❌ `DomainException` |
| `"1234567890ABC"` | 13 | ❌ | ❌ `DomainException` |
| `"             "` (13 spaces) | 13 | ❌ | ❌ `DomainException` |
| `null` | — | — | ❌ `DomainException` |

---

## Application DTO: `TaxpayerProfileDto`

**Namespace**: `Rentier.Application.DTOs`  
**Kind**: `sealed record` (immutable, structural equality)  
**Purpose**: Transports profile data from Application layer to Desktop layer without exposing the Domain entity.

```csharp
public sealed record TaxpayerProfileDto(
    Guid Id,
    string Jmbg,
    string FullName,
    string Address,
    string OpstinaCode,
    string? PhoneNumber,
    string? Email);
```

**Mapping**: Handler maps `TaxpayerProfile` → `TaxpayerProfileDto` inline (no AutoMapper).

---

## Application Command: `SaveTaxpayerProfileCommand`

**Namespace**: `Rentier.Application.Commands`  
**Kind**: `sealed record`

```csharp
public sealed record SaveTaxpayerProfileCommand(
    string Jmbg,
    string FullName,
    string Address,
    string OpstinaCode,
    string? PhoneNumber,
    string? Email);
```

**Handler return type**: `Result<VoidResult, Error>`

**Handler logic**:
1. Construct `TaxpayerProfile` entity from command fields (may throw `DomainException`).
2. Catch `DomainException` → return `Result.Fail(new Error("DOMAIN_VALIDATION", ex.Message))`.
3. Call `repository.GetAsync()` to determine upsert path.
4. If `null`: use `Guid.NewGuid()` as entity Id → `repository.SaveAsync(entity)`.
5. If existing: use `existing.Id` → reconstruct entity → `repository.SaveAsync(entity)`.
6. Return `Result.Ok(VoidResult.Value)`.

---

## Application Query: `GetTaxpayerProfileQuery`

**Namespace**: `Rentier.Application.Queries`  
**Kind**: `sealed record`

```csharp
public sealed record GetTaxpayerProfileQuery();
```

**Handler return type**: `Result<TaxpayerProfileDto?, Error>`

**Handler logic**:
1. Call `repository.GetAsync()`.
2. If `null`: return `Result.Ok<TaxpayerProfileDto?>(null)`.
3. If existing: map to `TaxpayerProfileDto` → return `Result.Ok(dto)`.

---

## Common Types: `Result<T, TError>` and `Error`

**Namespace**: `Rentier.Application.Common`

```csharp
public sealed class Result<T, TError>
{
    public bool IsSuccess { get; }
    public T Value { get; }        // valid when IsSuccess = true
    public TError Error { get; }   // valid when IsSuccess = false

    private Result(bool isSuccess, T value, TError error) { ... }

    public static Result<T, TError> Ok(T value) =>
        new(true, value, default!);

    public static Result<T, TError> Fail(TError error) =>
        new(false, default!, error);
}

public sealed record Error(string Code, string Message);
```

**`VoidResult` type** (for void-returning commands):

```csharp
public sealed record VoidResult
{
    public static readonly VoidResult Value = new();
    private VoidResult() { }
}
```

---

## Infrastructure: EF Core Configuration

**Table**: `TaxpayerProfiles`

| Column | SQL Type | Nullable | Constraint |
|--------|----------|----------|------------|
| `Id` | `TEXT` (GUID) | NOT NULL | PRIMARY KEY |
| `Jmbg` | `TEXT` (max 13) | NOT NULL | UNIQUE INDEX |
| `FullName` | `TEXT` (max 200) | NOT NULL | — |
| `Address` | `TEXT` (max 500) | NOT NULL | — |
| `OpstinaCode` | `TEXT` (max 10) | NOT NULL | — |
| `PhoneNumber` | `TEXT` (max 50) | NULL | — |
| `Email` | `TEXT` (max 254) | NULL | — |

**Migration**: `0002_TaxpayerProfile`

Creates the `TaxpayerProfiles` table (no prior data migration required — first time this table exists).

---

## ViewModel State: `ProfileSettingsViewModel`

**Namespace**: `Rentier.Desktop.ViewModels`

| Property | Type | Notes |
|----------|------|-------|
| `Jmbg` | `string` | Two-way bound; drives inline validation |
| `FullName` | `string` | Two-way bound; required field |
| `Address` | `string` | Two-way bound; required field |
| `OpstinaCode` | `string` | Two-way bound; required field |
| `PhoneNumber` | `string` | Two-way bound; optional |
| `Email` | `string` | Two-way bound; optional |
| `JmbgError` | `string?` | Null when valid; "JMBG must be exactly 13 digits" when invalid |
| `FullNameError` | `string?` | Null when valid; required field error when blank |
| `AddressError` | `string?` | Null when valid; required field error when blank |
| `OpstinaCodeError` | `string?` | Null when valid; required field error when blank |
| `IsLoading` | `bool` | True during async save; disables form |
| `SuccessMessage` | `string?` | Shown after successful save; cleared on next edit |
| `ErrorMessage` | `string?` | Shown on unexpected save failure |
| `SaveCommand` | `ReactiveCommand<Unit, Unit>` | Disabled when any validation error present |

**CanExecute observable**:
```csharp
var canSave = this.WhenAnyValue(
    x => x.Jmbg, x => x.FullName, x => x.Address, x => x.OpstinaCode,
    (jmbg, fn, addr, ops) =>
        jmbg?.Length == 13 && jmbg.All(char.IsDigit) &&
        !string.IsNullOrWhiteSpace(fn) &&
        !string.IsNullOrWhiteSpace(addr) &&
        !string.IsNullOrWhiteSpace(ops));
```

---

## Strings Added to `Strings.resx`

| Key | Value |
|-----|-------|
| `Settings_Profile_TabHeader` | `Profile` |
| `Profile_Jmbg_Label` | `JMBG` |
| `Profile_FullName_Label` | `Full Name` |
| `Profile_Address_Label` | `Address` |
| `Profile_OpstinaCode_Label` | `Opstina Code` |
| `Profile_PhoneNumber_Label` | `Phone Number` |
| `Profile_Email_Label` | `Email` |
| `Profile_Save_Button` | `Save Profile` |
| `Profile_Saved_Confirmation` | `Profile saved successfully.` |
| `Profile_JmbgValidation_Error` | `JMBG must be exactly 13 digits` |
| `Profile_RequiredField_Error` | `This field is required` |
