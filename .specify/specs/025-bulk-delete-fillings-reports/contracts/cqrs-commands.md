# CQRS Command Contracts: Bulk Delete

**Feature**: 025-bulk-delete-fillings-reports  
**Layer**: Application (Rentier.Application)

## Commands

### BulkDeleteFilingsCommand

```csharp
namespace Rentier.Application.Commands;

public sealed record BulkDeleteFilingsCommand(IReadOnlyList<Guid> FilingIds);
```

**Handler**: `BulkDeleteFilingsCommandHandler : ICommandHandler<BulkDeleteFilingsCommand, Result<VoidResult, Error>>`

| Input | Type | Constraints |
|-------|------|-------------|
| `FilingIds` | `IReadOnlyList<Guid>` | Non-null, non-empty |

| Result | Condition |
|--------|-----------|
| `Success(VoidResult)` | All found filings deleted (missing IDs skipped) |
| `Failure(Error.Domain)` | `FilingIds` is null or empty |

**Behaviour**:
1. Validate `FilingIds` is non-null and non-empty.
2. Call `IFilingRepository.DeleteManyAsync(command.FilingIds, ct)`.
3. Return `Success(VoidResult)`.
4. On exception: catch and return `Failure(Error("BULK_DELETE_FILINGS_FAILED", ex.Message))`.

---

### BulkDeleteReportsCommand

```csharp
namespace Rentier.Application.Commands;

public sealed record BulkDeleteReportsCommand(IReadOnlyList<Guid> ReportIds);
```

**Handler**: `BulkDeleteReportsCommandHandler : ICommandHandler<BulkDeleteReportsCommand, Result<VoidResult, Error>>`

| Input | Type | Constraints |
|-------|------|-------------|
| `ReportIds` | `IReadOnlyList<Guid>` | Non-null, non-empty |

| Result | Condition |
|--------|-----------|
| `Success(VoidResult)` | All found reports + linked filings deleted |
| `Failure(Error.Domain)` | `ReportIds` is null or empty |
| `Failure(Error)` | Infrastructure/DB exception |

**Behaviour**:
1. Validate `ReportIds` is non-null and non-empty.
2. For each `reportId` in `ReportIds`: call `IFilingRepository.DeleteByReportIdAsync(reportId, ct)`.
3. Call `IReportRepository.DeleteManyAsync(command.ReportIds, ct)`.
4. Return `Success(VoidResult)`.
5. On exception: catch and return `Failure(Error("BULK_DELETE_REPORTS_FAILED", ex.Message))`.

---

## Repository Interface Extensions

### IFilingRepository — New Method

```csharp
/// <summary>
/// Deletes all Filing records whose Id is in the given list.
/// Missing IDs are silently skipped. Uses load-then-remove pattern.
/// </summary>
Task DeleteManyAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default);
```

### IReportRepository — New Method

```csharp
/// <summary>
/// Deletes all Report records whose Id is in the given list.
/// Missing IDs are silently skipped. Uses load-then-remove pattern.
/// Callers MUST delete linked filings before calling this method.
/// </summary>
Task DeleteManyAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default);
```

---

## DI Registration Contract

New registrations in `CompositionRoot.cs`:

```csharp
// Bulk delete handlers
services.AddTransient<
    ICommandHandler<BulkDeleteFilingsCommand, Result<VoidResult, Error>>,
    BulkDeleteFilingsCommandHandler>();
services.AddTransient<
    ICommandHandler<BulkDeleteReportsCommand, Result<VoidResult, Error>>,
    BulkDeleteReportsCommandHandler>();
```

ViewModel constructors will accept the new handlers via DI alongside existing dependencies.
