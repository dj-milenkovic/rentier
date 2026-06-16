---
name: rentier-test-scaffold
description: >
  Scaffold the correct boilerplate for any new Rentier test class. Use this skill whenever
  creating a new *Tests.cs file in any test project (Rentier.UnitTests,
  Rentier.Infrastructure.Tests, Rentier.Scenarios.Tests). Picks the right namespace, trait
  tags, setup pattern, and imports based on the layer being tested (Domain, Application,
  Infrastructure, Desktop, Scenario). Invoke proactively when a handler, entity, ViewModel,
  or repository is added without a corresponding test file.
---

# Rentier Test File Scaffolding

Every test class requires five decisions before the first `[Fact]` is written:
1. Which test project does this live in?
2. What namespace and `[Trait]` tag apply?
3. How is the system-under-test constructed?
4. How is shared state initialised?
5. Which cancellation-token pattern applies?

This skill answers all five, per layer.

---

## Layer → Project Mapping

| What you're testing | Test project | Namespace |
|---|---|---|
| Domain entity / value object | `Rentier.UnitTests` | `Rentier.UnitTests.Domain` |
| Domain service | `Rentier.UnitTests` | `Rentier.UnitTests.Domain.Services` |
| CQRS handler | `Rentier.UnitTests` | `Rentier.UnitTests.Application` |
| ViewModel / value converter | `Rentier.UnitTests` | `Rentier.UnitTests.Desktop` |
| Headless Avalonia view | `Rentier.UnitTests` | `Rentier.UnitTests.Desktop.Views` |
| EF Core repository | `Rentier.Infrastructure.Tests` | `Rentier.Infrastructure.Tests` (or `.Repositories`) |
| Parser / serializer | `Rentier.Infrastructure.Tests` | `Rentier.Infrastructure.Tests.Parsers` |
| Full vertical slice | `Rentier.Scenarios.Tests` | `Rentier.Scenarios.Tests` |

---

## Scaffold: Domain Entity or Value Object

```csharp
using FluentAssertions;
using Rentier.Domain.Entities;
using Rentier.Domain.Exceptions;
using Xunit;

namespace Rentier.UnitTests.Domain;

public class FilingTests
{
    // Domain tests need ZERO setup — no constructor, no mocks.
    // Provide a private static factory with sensible defaults:
    private static Filing MakeFiling(
        DateOnly? date  = null,
        decimal gross   = 10_000m,
        decimal wht     = 1_500m)
    {
        var d = date ?? new DateOnly(2024, 6, 15);
        return Filing.CreateFromIncome(
            Guid.NewGuid(), IncomeType.Dividend, "AAPL", d,
            gross, wht,
            grossTaxPayableRsd: gross * 0.15m,
            taxPayableRsd: Math.Max(0m, gross * 0.15m - wht),
            filingDeadline: d.AddDays(30));
    }

    [Fact]
    public void Method_Scenario_ExpectedBehavior()
    {
        var filing = MakeFiling();
        // ...
    }
}
```

No `[Trait]`. No `IAsyncLifetime`. No `Substitute.For<>()` — ever.

---

## Scaffold: Domain Service (Func<> Delegate Injection)

```csharp
using FluentAssertions;
using Rentier.Domain.Enums;
using Rentier.Domain.Services;
using Rentier.Domain.ValueObjects;
using Xunit;

namespace Rentier.UnitTests.Domain.Services;

public class TaxCalculationServiceTests
{
    private static readonly DateOnly TestDate = new(2024, 1, 15);

    // Domain services accept Func<> delegates — never interface mocks
    private static Func<DateOnly, string, Task<ExchangeRate>> FixedRate(decimal rateToRsd)
        => (date, currency) => Task.FromResult(new ExchangeRate(date, currency, rateToRsd));

    [Fact]
    public async Task CalculateAsync_Scenario_ExpectedBehavior()
    {
        var result = await TaxCalculationService.CalculateAsync(
            IncomeType.Dividend, "AAPL", TestDate,
            100m, "USD", 15m, "USD",
            FixedRate(117.21m),
            TestContext.Current.CancellationToken);

        result.TaxPayableRsd.Should().Be(0.00m);
    }
}
```

---

## Scaffold: Application CQRS Handler

```csharp
using FluentAssertions;
using NSubstitute;
using Rentier.Application.Commands;
using Rentier.Application.Handlers;
using Rentier.Application.Repositories;
using Xunit;

namespace Rentier.UnitTests.Application;

public class DeleteFilingCommandHandlerTests
{
    private readonly IFilingRepository _repo;
    private readonly DeleteFilingCommandHandler _sut;

    // Initialise substitutes in the constructor — xUnit creates a fresh
    // instance per test, so every test gets clean substitutes automatically.
    public DeleteFilingCommandHandlerTests()
    {
        _repo = Substitute.For<IFilingRepository>();
        _sut  = new DeleteFilingCommandHandler(_repo);
    }

    [Fact]
    public async Task HandleAsync_ValidCommand_ReturnsSuccess()
    {
        var result = await _sut.HandleAsync(
            new DeleteFilingCommand(Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
    }
}
```

Rules:
- One test class per handler — never combine two handlers
- Substitutes in the **constructor**, not in individual `[Fact]` methods
- Always `TestContext.Current.CancellationToken` in every async call

---

## Scaffold: Infrastructure Repository (IAsyncLifetime)

```csharp
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Rentier.Infrastructure.Persistence;
using Rentier.Infrastructure.Repositories;
using Xunit;

namespace Rentier.Infrastructure.Tests.Repositories;

[Trait("Category", "Integration")]
public class FilingRepositoryTests : IAsyncLifetime
{
    private SqliteConnection _conn = null!;
    private AppDbContext _ctx      = null!;
    private FilingRepository _repo = null!;

    public async Task InitializeAsync()
    {
        _conn = new SqliteConnection("Data Source=:memory:");
        await _conn.OpenAsync(TestContext.Current.CancellationToken);
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_conn).Options;
        _ctx  = new AppDbContext(opts);
        await _ctx.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        _repo = new FilingRepository(_ctx);
    }

    public async Task DisposeAsync()
    {
        await _ctx.DisposeAsync();
        await _conn.DisposeAsync();
    }

    [Fact]
    public async Task GetByIdAsync_Scenario_ExpectedBehavior()
    {
        // ...
    }
}
```

Rules:
- `[Trait("Category", "Integration")]` is mandatory on every class
- `UseSqlite("Data Source=:memory:")` — never `UseInMemoryDatabase`
- Keep `SqliteConnection` open for the test class lifetime (prevents GC of the DB)
- Prefer `IAsyncLifetime` over per-test `CreateContext()` when there are more than 3 tests

---

## Scaffold: Desktop ViewModel

```csharp
using FluentAssertions;
using NSubstitute;
using Rentier.Application.Common;
using Rentier.Application.Interfaces;
using Rentier.Application.Queries;
using Rentier.Desktop.ViewModels;
using System.Reactive.Concurrency;
using Xunit;

namespace Rentier.UnitTests.Desktop;

public class FilingsViewModelTests
{
    // ── Handler factories — static, never inline in test bodies ──────────────

    private static IQueryHandler<GetFilingsQuery, Result<FilingsPageResult, Error>>
        MockGetFilings(FilingsPageResult? page = null)
    {
        var h = Substitute.For<IQueryHandler<GetFilingsQuery, Result<FilingsPageResult, Error>>>();
        h.HandleAsync(Arg.Any<GetFilingsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<FilingsPageResult, Error>.Success(
                page ?? new FilingsPageResult([], 0, 1)));
        return h;
    }

    private static FilingsViewModel CreateVm(
        IQueryHandler<GetFilingsQuery, Result<FilingsPageResult, Error>>? getFilings = null)
        => new(getFilings ?? MockGetFilings(), ImmediateScheduler.Instance);

    [Fact]
    public void Constructor_InitializesWithDefaults()
    {
        var vm = CreateVm();
        vm.IsLoading.Should().BeFalse();
        vm.ErrorMessage.Should().BeNull();
        vm.Filings.Should().BeEmpty();
    }
}
```

Rules:
- Always `ImmediateScheduler.Instance` — never `RxApp.MainThreadScheduler` in tests
- Handler factories as `private static` methods — never inline `Substitute.For<>()` per test
- `CreateVm()` factory keeps test bodies readable and consistently wired

---

## Scaffold: Scenario Test

```csharp
using FluentAssertions;
using Rentier.Application.Commands;
using Rentier.Application.Handlers;
using Rentier.Application.Repositories;
using Xunit;

namespace Rentier.Scenarios.Tests;

[Trait("Category", "Scenario")]
public sealed class FilingLifecycleScenario : IDisposable
{
    private readonly ScenarioFixture _fixture;
    private readonly IFilingRepository _filingRepo;

    // One ScenarioFixture per class — xUnit creates a fresh class instance per [Fact].
    public FilingLifecycleScenario()
    {
        _fixture    = new ScenarioFixture();
        _filingRepo = _fixture.GetService<IFilingRepository>();
    }

    [Fact]
    public async Task Scenario_HappyPath_CompletesSuccessfully()
    {
        var ct = TestContext.Current.CancellationToken;
        // Build → Act → Assert Result AND DB state
    }

    public void Dispose() => _fixture.Dispose();
}
```

Rules:
- `[Trait("Category","Scenario")]` is mandatory
- `sealed` — scenarios have no base class
- `IDisposable` (sync Dispose), not `IAsyncLifetime`
- Assert both the handler `Result` AND the re-queried DB state

---

## Decision Tree: Which Scaffold?

```
Testing domain logic (invariants, state machines, computations)?
  → Domain scaffold — no mocks, no DB

Testing a CQRS handler's routing / orchestration?
  → Application scaffold — Substitute repos

Testing SQL, value converters, FK constraints?
  → Infrastructure scaffold — real SQLite, IAsyncLifetime

Testing ViewModel state, commands, navigation?
  → Desktop scaffold — ImmediateScheduler

Testing a complete user story end-to-end?
  → Scenario scaffold — ScenarioFixture + real DI
```

---

## Mandatory Checklist for Every New Test File

- [ ] Namespace matches the project/layer table above
- [ ] `[Trait]` applied when required (Integration, UI, Scenario)
- [ ] `TestContext.Current.CancellationToken` used in every async call
- [ ] No `new DateTime(...)` anywhere — always `new DateOnly(y, m, d)`
- [ ] No `double` literals for monetary values — always `m` suffix
- [ ] If Infrastructure: `UseSqlite("Data Source=:memory:")` not `UseInMemoryDatabase`
- [ ] If Domain: no `Substitute.For<>()` anywhere in the file
- [ ] If Application: one handler class per test class
