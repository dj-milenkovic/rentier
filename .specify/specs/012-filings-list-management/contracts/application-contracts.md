# Application Layer Contracts: Filings List and Management UI (012)

**Feature**: `012-filings-list-management`  
**Created**: 2025-01-08  
**Scope**: Public interfaces exposed by the Application layer consumed by Desktop

---

## Contract Overview

The Desktop layer (`FilingsViewModel`) communicates with the Application layer exclusively through
four typed handler interfaces. No direct repository access or infrastructure calls are permitted
from the Desktop.

```
FilingsViewModel
  ├── IQueryHandler<GetFilingsQuery, Result<FilingsPageResult, Error>>
  ├── ICommandHandler<UpdateFilingStatusCommand, Result<VoidResult, Error>>
  ├── ICommandHandler<UpdatePaymentReferenceCommand, Result<VoidResult, Error>>
  └── ICommandHandler<DeleteFilingCommand, Result<VoidResult, Error>>
```

---

## Query Contract

### `GetFilingsQuery` → `FilingsPageResult`

**Interface**: `IQueryHandler<GetFilingsQuery, Result<FilingsPageResult, Error>>`  
**Handler**: `GetFilingsQueryHandler`  
**Namespace**: `Rentier.Application.Handlers`

#### Input

```csharp
public sealed record GetFilingsQuery(
    FilingFilterMode Filter,    // Unpaid (default) | All
    int Page,                   // 1-based; must be >= 1
    int PageSize = 20           // default 20; range [1, 100]
);
```

#### Output (success)

```csharp
public sealed record FilingsPageResult(
    IReadOnlyList<FilingRowDto> Rows,   // items for this page, sorted FilingDeadline ASC
    int TotalCount,                     // total filtered records
    int TotalPages                      // ceil(TotalCount / PageSize); minimum 1
);

public sealed record FilingRowDto(
    Guid         Id,
    FilingStatus Status,
    IncomeType   IncomeType,
    string       PayingEntity,
    DateOnly     FilingDeadline,
    decimal      TaxPayable,        // monetary value; always decimal, always >= 0
    string?      PaymentReference   // nullable; max 200 chars
);
```

#### Output (failure)

```csharp
Result<FilingsPageResult, Error>.Failure(new Error("..."))
// Possible Error messages:
//   "Page must be >= 1."
//   "PageSize must be between 1 and 100."
//   Any repository-level exception message (wrapped)
```

#### Behaviour

- Returns an empty `Rows` list (not an error) when no filings match the filter.
- `TotalPages` is always at least 1 even when `TotalCount` is 0 (avoids "Page 1 of 0").
- Rows are always sorted ascending by `FilingDeadline`.
- `Filter = Unpaid` → only `Status ∈ { Init, Filed }` included.
- `Filter = All` → all statuses included.
- Caller is responsible for clamping `Page` if `Page > TotalPages` after a filter/delete changes the set.

---

## Command Contracts

### `UpdateFilingStatusCommand` → `VoidResult`

**Interface**: `ICommandHandler<UpdateFilingStatusCommand, Result<VoidResult, Error>>`  
**Handler**: `UpdateFilingStatusCommandHandler`

#### Input

```csharp
public sealed record UpdateFilingStatusCommand(
    Guid         FilingId,     // must identify an existing Filing
    FilingStatus NewStatus     // target status; domain enforces valid transition
);
```

#### Output (success)

```csharp
Result<VoidResult, Error>.Success(VoidResult.Instance)
// Side effect: Filing.Status updated in database
```

#### Output (failure)

```csharp
Result<VoidResult, Error>.Failure(new Error("..."))
// Possible Error messages:
//   "Filing not found."                           → FilingId does not exist
//   "Invalid Filing status transition: X → Y."   → DomainException from AdvanceStatus
//   Any repository-level exception message
```

#### Behaviour

- Loads filing by `FilingId` (tracked, not AsNoTracking, to allow mutation).
- Delegates transition to `Filing.AdvanceStatus(NewStatus)`.
- Calls `UpdateAsync(filing)` on success.
- Returns `Failure` without persisting if `DomainException` is thrown.
- The `Paid` status is terminal — any further advance attempt returns `Failure`.

---

### `UpdatePaymentReferenceCommand` → `VoidResult`

**Interface**: `ICommandHandler<UpdatePaymentReferenceCommand, Result<VoidResult, Error>>`  
**Handler**: `UpdatePaymentReferenceCommandHandler`

#### Input

```csharp
public sealed record UpdatePaymentReferenceCommand(
    Guid    FilingId,          // must identify an existing Filing
    string? PaymentReference   // null to clear; non-null trimmed and validated ≤ 200 chars
);
```

#### Output (success)

```csharp
Result<VoidResult, Error>.Success(VoidResult.Instance)
// Side effect: Filing.PaymentReference updated in database
```

#### Output (failure)

```csharp
Result<VoidResult, Error>.Failure(new Error("..."))
// Possible Error messages:
//   "Filing not found."
//   "PaymentReference must not exceed 200 characters."   → DomainException from SetPaymentReference
//   Any repository-level exception message
```

#### Behaviour

- Does not validate filing status (domain has no status precondition for this field).
- UI enforces Filed-only editability; the handler persists whatever is sent.
- Empty string after trim is stored as `null` (see `SetPaymentReference` domain rule).

---

### `DeleteFilingCommand` → `VoidResult`

**Interface**: `ICommandHandler<DeleteFilingCommand, Result<VoidResult, Error>>`  
**Handler**: `DeleteFilingCommandHandler`

#### Input

```csharp
public sealed record DeleteFilingCommand(Guid FilingId);
```

#### Output (success)

```csharp
Result<VoidResult, Error>.Success(VoidResult.Instance)
// Side effect: Filing row removed from database
// Idempotent: success even if FilingId not found
```

#### Output (failure)

```csharp
Result<VoidResult, Error>.Failure(new Error("..."))
// Only for unexpected repository exceptions (not for "not found")
```

#### Behaviour

- Delegates to `IFilingRepository.DeleteAsync(FilingId)` which is a no-op if not found.
- Caller (VM) must obtain user confirmation via `ContentDialog` **before** invoking this command.
- No cascade rules apply at the application level; EF `OnDelete(Cascade)` on `TaxpayerProfileId`
  would cascade in the other direction only.

---

## Repository Contract Extension

### `IFilingRepository.GetPagedAsync`

**Namespace**: `Rentier.Application.Repositories`

```csharp
Task<(IReadOnlyList<Filing> Items, int TotalCount)> GetPagedAsync(
    FilingFilterMode filter,
    int page,
    int pageSize,
    CancellationToken ct = default);
```

| Parameter | Type | Contract |
|-----------|------|----------|
| `filter` | `FilingFilterMode` | Unpaid → `Status ∈ {Init, Filed}`; All → no filter |
| `page` | `int` | 1-based; handler validates before calling |
| `pageSize` | `int` | handler validates before calling |
| `ct` | `CancellationToken` | respected by all async operations |
| Returns `Items` | `IReadOnlyList<Filing>` | ordered `FilingDeadline ASC`; empty list if no results |
| Returns `TotalCount` | `int` | filtered-but-unpaged record count |

---

## `Func<string, Task<bool>>` Delete Confirmation Delegate

**Registered in**: `CompositionRoot.AddDesktopServices`  
**Consumed by**: `FilingsViewModel`

```csharp
// Signature
Func<string, Task<bool>> confirmDelete

// Semantic contract:
//   - Receives a user-facing confirmation message string
//   - Shows a ContentDialog (or equivalent) asynchronously
//   - Returns true  → user confirmed deletion
//   - Returns false → user cancelled
//   - Must be called on the UI thread (Avalonia requirement for ContentDialog)
//   - Never throws; returns false on dialog error
```

**Test double**: In unit tests, replace with `_ => Task.FromResult(true)` (always confirm)
or `_ => Task.FromResult(false)` (always cancel).
