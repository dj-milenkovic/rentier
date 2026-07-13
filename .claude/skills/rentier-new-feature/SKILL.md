---
name: rentier-new-feature
description: >
  Scaffold a new Rentier feature end-to-end following the repository's exact CQRS
  pattern: command/query record, handler, DTO, CompositionRoot DI registration,
  xUnit v3 unit tests, and ViewModel wiring. Make sure to use this skill whenever
  the user asks to add a feature, use case, command, query, handler, or page, or
  says things like "add a way to …", "I want a button that …", "support X" — even
  when they don't use CQRS vocabulary. Using the templates here avoids re-deriving
  the handler/registration/test shape from existing code every time.
---

# Scaffold a New Rentier Feature

Every feature follows the same vertical slice. Work through the checklist in order —
each step's output is the next step's input. The templates below mirror the real code
(`UpdateFilingStatusCommandHandler`, `DeleteFilingCommandHandlerTests`,
`CompositionRoot`); keep new code consistent with them, not with generic CQRS
examples from training data.

Before starting: if the feature touches money, dates, tax, deadlines, or XML export,
read `.claude/skills/rentier-tax-rules/SKILL.md` first. If it adds or changes a view,
read `.claude/skills/rentier-ui-design/SKILL.md` before writing AXAML.

## Checklist

1. **Command or Query record** — `src/Rentier.Application/Commands/` or `Queries/`
2. **Handler** — `src/Rentier.Application/Handlers/`
3. **DTO** (if data crosses into Desktop) — `src/Rentier.Application/DTOs/`
4. **Repository interface change** (only if needed) — `src/Rentier.Application/Repositories/`,
   implemented in `src/Rentier.Infrastructure/`
5. **DI registration** — `src/Rentier.Desktop/Composition/CompositionRoot.cs`
6. **Unit tests** — `tests/Rentier.UnitTests/Application/`
7. **ViewModel wiring** (if user-facing) — `src/Rentier.Desktop/ViewModels/`
8. **Verify** — build, tests, format (commands below)

## 1. Command/Query record

One `sealed record` per use case. Mutations are Commands, reads are Queries:

```csharp
// src/Rentier.Application/Commands/ArchiveFilingCommand.cs
using Rentier.Domain.Enums;

namespace Rentier.Application.Commands;

public sealed record ArchiveFilingCommand(Guid FilingId);
```

`decimal` for money/rates, `DateOnly` for dates — never `double` or `DateTime`.

## 2. Handler

One handler class per command/query, implementing `ICommandHandler<TCommand, TResult>`
or `IQueryHandler<TQuery, TResult>` (`src/Rentier.Application/Interfaces/`). The result
type is `Result<T, Error>`; use `VoidResult` when there is no payload:

```csharp
// src/Rentier.Application/Handlers/ArchiveFilingCommandHandler.cs
using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.Interfaces;
using Rentier.Application.Repositories;
using Rentier.Domain.Exceptions;

namespace Rentier.Application.Handlers;

/// <summary>One-line summary of the use case.</summary>
public sealed class ArchiveFilingCommandHandler
    : ICommandHandler<ArchiveFilingCommand, Result<VoidResult, Error>>
{
    private readonly IFilingRepository _filings;

    public ArchiveFilingCommandHandler(IFilingRepository filings) => _filings = filings;

    public async Task<Result<VoidResult, Error>> HandleAsync(
        ArchiveFilingCommand command, CancellationToken ct = default)
    {
        var filing = await _filings.GetByIdAsync(command.FilingId, ct);
        if (filing is null)
            return Result<VoidResult, Error>.Failure(
                Error.NotFound($"Filing {command.FilingId} not found."));

        try
        {
            filing.Archive();   // domain method enforces invariants
        }
        catch (DomainException ex)
        {
            return Result<VoidResult, Error>.Failure(Error.Domain(ex.Message));
        }

        await _filings.UpdateAsync(filing, ct);
        return Result<VoidResult, Error>.Success(VoidResult.Value);
    }
}
```

The pattern that matters: **business rules live in the Domain entity/service and throw
`DomainException`; the handler translates that into `Error.Domain(...)`.** Handlers
never contain the rule itself, never throw for expected failures, and never reference
Infrastructure types. `Error` factories (`NotFound`, `Domain`, `Infrastructure`, …)
are in `src/Rentier.Application/Common/Error.cs` — add a new factory + `ErrorCodes`
entry rather than inventing ad-hoc codes.

## 3. DI registration

Register the handler in `CompositionRoot.AddDesktopServices`
(`src/Rentier.Desktop/Composition/CompositionRoot.cs`), next to the related feature
group:

```csharp
services.AddTransient<
    ICommandHandler<ArchiveFilingCommand, Result<VoidResult, Error>>,
    ArchiveFilingCommandHandler>();
```

Handlers are `Transient`; ViewModels holding in-session state are `Singleton`.
`tests/Rentier.UnitTests/Application/DiRegistrationSmokeTests.cs` catches missing
registrations — run it after wiring.

## 4. Unit tests

`tests/Rentier.UnitTests/Application/<Handler>Tests.cs` — xUnit v3 + FluentAssertions
+ NSubstitute. Naming: `MethodName_StateUnderTest_ExpectedBehavior`. Mock only the
repository ports, never the handler; pass `TestContext.Current.CancellationToken`:

```csharp
using FluentAssertions;
using NSubstitute;
using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.Handlers;
using Rentier.Application.Repositories;
using Xunit;

namespace Rentier.UnitTests.Application;

public class ArchiveFilingCommandHandlerTests
{
    private readonly IFilingRepository _repo = Substitute.For<IFilingRepository>();
    private readonly ArchiveFilingCommandHandler _sut;

    public ArchiveFilingCommandHandlerTests() => _sut = new ArchiveFilingCommandHandler(_repo);

    [Fact]
    public async Task HandleAsync_WhenFilingNotFound_ReturnsNotFoundError()
    {
        var result = await _sut.HandleAsync(
            new ArchiveFilingCommand(Guid.NewGuid()), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(ErrorCodes.NOT_FOUND);
    }
}
```

Cover at minimum: the happy path, the not-found path, and each `DomainException`
translation. New Domain logic gets its own no-mock tests under
`tests/Rentier.UnitTests/Domain/` (see `.claude/skills/rentier-unit-tests`).

## 5. ViewModel wiring (user-facing features)

Inject the handler interface, expose a `ReactiveCommand.CreateFromTask`, and consume
the result via `IsSuccess`/`Error` — `Result` has no `Match` method:

```csharp
ArchiveCommand = ReactiveCommand.CreateFromTask<Guid>(async id =>
{
    var result = await _archiveHandler.HandleAsync(new ArchiveFilingCommand(id));
    if (!result.IsSuccess)
        ErrorMessage = result.Error.Message;
});
```

Standard state properties (`IsLoading`, `ErrorMessage`) and ViewModel tests are
covered by `.claude/skills/rentier-ui-tests` — use it for any ViewModel change.

## 6. Verify before claiming done

```bash
dotnet build Rentier.slnx --no-restore -c Release
dotnet test Rentier.slnx --filter "Category!=Integration"
dotnet format Rentier.slnx --no-restore --verify-no-changes
```
