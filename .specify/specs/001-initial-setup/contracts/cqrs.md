# Contract: CQRS Interfaces

**Feature**: `001-initial-setup`  
**Layer**: `Rentier.Application/Interfaces/`  
**Date**: 2026-04-06

---

## Overview

Rentier uses the CQRS (Command Query Responsibility Segregation) pattern for all Application-layer
use cases. Commands mutate state and return a result indicating success or failure. Queries read
state without side effects.

All handler interfaces are declared in `Rentier.Application`. They are generic; concrete
implementations are created per command/query in the feature that introduces that use case.
`Rentier.Desktop` resolves handlers from the DI container and invokes them.

---

## Generic Handler Interfaces

### ICommandHandler\<TCommand, TResult\>

**File**: `Rentier.Application/Interfaces/ICommandHandler.cs`

**Purpose**: Defines the contract for all Application-layer command handlers. A command mutates
domain state (e.g., creates a `Filing`, updates `FilingStatus`, syncs a `Mailbox`) and returns
a typed result indicating outcome.

**Contract**:

| Member | Description |
|--------|-------------|
| `Task<TResult> HandleAsync(TCommand command, CancellationToken ct = default)` | Execute the command. Returns a `TResult` encapsulating success or structured failure. Never throws for expected domain errors; unexpected errors propagate as exceptions. |

**Type parameter constraints**:
- `TCommand`: A plain C# class or `record` containing the input data for the command. No framework
  dependencies.
- `TResult`: Typically a `Result<T>` or `Result<T, TError>` discriminated union. The pattern for
  `TResult` is defined by each use case; the interface itself does not constrain it.

**Example usage** (documents expected pattern for future implementors):

```text
Command:  CreateFilingCommand { Guid TaxpayerProfileId, DateOnly TaxPeriod }
Result:   Result<Guid>                         ← returns new Filing.Id on success
Handler:  CreateFilingCommandHandler : ICommandHandler<CreateFilingCommand, Result<Guid>>
```

---

### IQueryHandler\<TQuery, TResult\>

**File**: `Rentier.Application/Interfaces/IQueryHandler.cs`

**Purpose**: Defines the contract for all Application-layer query handlers. A query reads domain
state without mutating it and returns a typed result.

**Contract**:

| Member | Description |
|--------|-------------|
| `Task<TResult> HandleAsync(TQuery query, CancellationToken ct = default)` | Execute the query. Returns a `TResult` containing the query result or a structured "not found" representation. Never throws for expected empty-result cases. |

**Type parameter constraints**:
- `TQuery`: A plain C# class or `record` containing the query parameters (e.g., filter criteria,
  pagination options).
- `TResult`: The query output type. May be a domain entity, DTO, collection, or `Option<T>`.

**Example usage** (documents expected pattern for future implementors):

```text
Query:   GetFilingsQuery { FilingStatus? StatusFilter }
Result:  IReadOnlyList<FilingDto>
Handler: GetFilingsQueryHandler : IQueryHandler<GetFilingsQuery, IReadOnlyList<FilingDto>>
```

---

## Initial Command Catalogue

The following commands are identified from the constitution and spec. They are listed here as
design intent; concrete handler classes are created when each feature is implemented.

| Command | Input Fields | Output (`TResult`) | Description |
|---------|-------------|-------------------|-------------|
| `CreateFilingCommand` | `Guid TaxpayerProfileId`, `DateOnly TaxPeriod` | `Result<Guid>` (new Filing.Id) | Creates a new `Filing` in `Init` status for a given taxpayer and tax period. |
| `UpdateFilingStatusCommand` | `Guid FilingId`, `FilingStatus NewStatus` | `Result` | Advances a `Filing` through its status machine. Rejects invalid transitions with a domain error. |
| `SyncMailboxCommand` | `Guid MailboxId` | `Result<int>` (count of new messages processed) | Initiates an IMAP sync for a configured mailbox and advances its `MailboxCursor`. |

---

## Initial Query Catalogue

The following queries are identified from the constitution and spec. They are listed here as
design intent; concrete handler classes are created when each feature is implemented.

| Query | Input Fields | Output (`TResult`) | Description |
|-------|-------------|-------------------|-------------|
| `GetFilingsQuery` | `FilingStatus? StatusFilter` (optional) | `IReadOnlyList<FilingDto>` | Returns all filings, optionally filtered by status. |
| `GetReportsQuery` | `Guid? ImporterId` (optional) | `IReadOnlyList<ReportDto>` | Returns all imported reports, optionally filtered by importer. |
| `GetMailboxCursorQuery` | `Guid MailboxId` | `MailboxCursorDto?` | Returns the current sync cursor for a mailbox. Returns `null` if the mailbox has never been synced. |

---

## DI Registration Pattern

Command and query handlers are registered in `Rentier.Desktop/Composition/CompositionRoot.cs`
using `Microsoft.Extensions.DependencyInjection`:

```text
services.AddTransient<ICommandHandler<CreateFilingCommand, Result<Guid>>,
                      CreateFilingCommandHandler>();

services.AddTransient<IQueryHandler<GetFilingsQuery, IReadOnlyList<FilingDto>>,
                      GetFilingsQueryHandler>();
```

**Lifetime**: `Transient` for handlers (stateless; no shared mutable state between invocations).

---

## Desktop Integration Pattern

ViewModels in `Rentier.Desktop` invoke handlers via constructor-injected interfaces:

```text
MainWindowViewModel receives ICommandHandler<CreateFilingCommand, Result<Guid>> via DI.
On user action → ReactiveCommand.CreateFromTask(async (ct) => await _handler.HandleAsync(cmd, ct))
UI updates are scheduled via RxApp.MainThreadScheduler (constitution Principle IV).
```

Direct use of `await handler.HandleAsync(...)` is permitted in ViewModel `async Task` methods
when no reactive pipeline is needed. `.Result` and `.Wait()` are prohibited (constitution Principle IV).
