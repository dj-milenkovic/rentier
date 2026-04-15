# Rentier Testing Strategy

> **Scope:** Clean Architecture + Avalonia 11 + ReactiveUI 20 + EF Core 8 desktop application.  
> This document defines all recommended test categories, the recommended framework stack for each,
> and the custom instructions (coding conventions) that Copilot should follow when generating tests
> in that category.

---

## Table of Contents

1. [Test Taxonomy Overview](#1-test-taxonomy-overview)
2. [Domain Unit Tests](#2-domain-unit-tests)
3. [Application Unit Tests (CQRS Handlers)](#3-application-unit-tests-cqrs-handlers)
4. [ViewModel Unit Tests](#4-viewmodel-unit-tests)
5. [Infrastructure Integration Tests](#5-infrastructure-integration-tests)
6. [Property-Based Tests](#6-property-based-tests)
7. [Avalonia Headless UI Tests](#7-avalonia-headless-ui-tests)
8. [Scenario / Functional Tests](#8-scenario--functional-tests)
9. [Smoke / Wiring Tests](#9-smoke--wiring-tests)
10. [Snapshot Tests](#10-snapshot-tests)
11. [Live / Manual Integration Tests](#11-live--manual-integration-tests)
12. [End-to-End Tests](#12-end-to-end-tests)
13. [Coverage & CI Guidance](#13-coverage--ci-guidance)

---

## 1. Test Taxonomy Overview

| # | Category | Scope | Speed | Isolation | Project |
|---|----------|-------|-------|-----------|---------|
| 2 | Domain unit | Pure domain logic, value objects, services | < 1 ms | None (no mocks) | `Rentier.Domain.Tests` |
| 3 | Application unit | CQRS command/query handlers | < 5 ms | Mocked repos (NSubstitute) | `Rentier.Application.Tests` |
| 4 | ViewModel unit | ReactiveUI ViewModels, commands, state | < 10 ms | Mocked handlers | `Rentier.Desktop.Tests` |
| 5 | Infrastructure integration | EF Core repos, parsers, serializers | 10–200 ms | In-memory / SQLite | `Rentier.Infrastructure.Tests` |
| 6 | Property-based | Financial calculation invariants | 100 ms–1 s | None | `Rentier.Domain.Tests` |
| 7 | Avalonia headless UI | Control rendering, bindings, interactions | 50–500 ms | Headless Avalonia | `Rentier.Desktop.Tests` |
| 8 | Scenario / functional | Full pipeline: command → repo → result | 200 ms–2 s | In-memory DB | Dedicated `Rentier.Scenarios.Tests` |
| 9 | Smoke / wiring | DI container wiring, DB schema | < 50 ms | None / in-memory | Each test project |
| 10 | Snapshot | XML/CSV serialization output | < 10 ms | None | `Rentier.Infrastructure.Tests` |
| 11 | Live integration | IMAP, NBS web scraper | Minutes | Requires live server | `Rentier.Infrastructure.Tests` |
| 12 | E2E | Full desktop launch, user flows | Minutes | Runs the real app | Dedicated `Rentier.E2E.Tests` |

---

## 2. Domain Unit Tests

### When to write
Test every **domain entity**, **value object**, **domain service**, and **business rule** in
`Rentier.Domain`. These tests must never have external dependencies and never use mocks.

### Recommended frameworks
| Framework | Package | Purpose |
|-----------|---------|---------|
| **xUnit 2.x** | `xunit` | Test runner |
| **FluentAssertions 6.x** | `FluentAssertions` | Expressive assertion DSL |
| *(no mocking framework)* | — | Domain is pure; mocks are a code smell here |

### Custom instructions

```
## Domain Unit Tests — Copilot Instructions

1. PROJECT: All domain tests live in `Rentier.Domain.Tests`. No reference to any other layer.

2. NO MOCKS. Domain is pure C#. If you find yourself writing `Substitute.For<>()`, stop — the
   design is wrong. Inject test data directly via constructors or factory methods.

3. NAMING: `MethodName_StateUnderTest_ExpectedBehavior`
   Examples:
   - `CreateFromIncome_WhtExceedsGrossTax_ClampsToZero`
   - `MarkFiled_FromInitStatus_TransitionsToFiled`
   - `MarkFiled_FromPaidStatus_ThrowsDomainException`

4. DECIMAL PRECISION. Always assert `decimal` amounts exactly. Use `.Be(1234.56m)` — never
   cast to `double`. Use `MidpointRounding.AwayFromZero` in boundary tests.

5. DATEONLY. All dates are `DateOnly`. Never use `DateTime` in domain tests.

6. DOMAIN EXCEPTIONS. Test invalid transitions and guard clauses with:
   ```csharp
   act.Should().Throw<DomainException>().WithMessage("*keyword*");
   ```
   For async:
   ```csharp
   await act.Should().ThrowAsync<DomainException>().WithMessage("*keyword*");
   ```

7. THEORY + INLINEDATA for parametric cases (boundary values, multiple invalid inputs).
   Use `[MemberData]` when test data is complex.

8. ARRANGE-ACT-ASSERT. Always separate into three blocks. Comment `// Arrange`, `// Act`,
   `// Assert` when the block is longer than 5 lines.

9. PRIVATE HELPERS. Extract repeated setup into `private static` factory methods. Do not
   use xUnit fixtures for pure domain tests; they add unnecessary complexity.

10. NO I/O. Domain tests must never read files, hit a network, or open a DB connection.
    If a domain service requires a delegate (e.g., `Func<DateOnly, string, Task<ExchangeRate>>`),
    pass a lambda directly in the test.
```

### Example skeleton
```csharp
public class FilingStatusTransitionTests
{
    [Fact]
    public void MarkFiled_FromInitStatus_TransitionsToFiled()
    {
        var filing = FilingFactory.CreateInit();

        filing.MarkFiled();

        filing.Status.Should().Be(FilingStatus.Filed);
    }

    [Fact]
    public void MarkFiled_FromPaidStatus_ThrowsDomainException()
    {
        var filing = FilingFactory.CreatePaid();

        var act = () => filing.MarkFiled();

        act.Should().Throw<DomainException>().WithMessage("*Invalid*");
    }
}
```

---

## 3. Application Unit Tests (CQRS Handlers)

### When to write
Every **command handler** and **query handler** in `Rentier.Application` gets its own test class.
Repositories are mocked; no real DB is used.

### Recommended frameworks
| Framework | Package | Purpose |
|-----------|---------|---------|
| **xUnit 2.x** | `xunit` | Test runner |
| **FluentAssertions 6.x** | `FluentAssertions` | Assertions |
| **NSubstitute 5.x** | `NSubstitute` | Mock repositories and services |
| **Microsoft.Extensions.DependencyInjection 8.x** | `Microsoft.Extensions.DependencyInjection` | DI smoke sub-tests |

### Custom instructions

```
## Application Unit Tests — Copilot Instructions

1. PROJECT: `Rentier.Application.Tests`. May reference `Rentier.Tests.Common` for shared fakes.

2. ONE TEST CLASS PER HANDLER. `AddMailboxCommandHandlerTests`, `GetFilingsQueryHandlerTests`, etc.

3. SUBSTITUTE PATTERN. Repos are always mocked:
   ```csharp
   _repo = Substitute.For<IFilingRepository>();
   _handler = new GetFilingsQueryHandler(_repo);
   ```
   Always declare substitutes as fields; initialize in constructor (not `[SetUp]`).

4. RETURN VALUES. Use `.Returns(...)` to set up happy-path returns. Use `Result<T, Error>` patterns:
   ```csharp
   _repo.GetFilingsAsync(Arg.Any<FilingsFilter>(), Arg.Any<CancellationToken>())
       .Returns(new FilingsPageResult(filings, total, page));
   ```

5. VERIFY CALLS. After `await handler.HandleAsync(command, ct)`, verify repo interactions:
   ```csharp
   await _repo.Received(1).AddAsync(Arg.Is<Filing>(f => f.PayingEntity == "AAPL"), ct);
   ```
   Only verify calls that matter for correctness — do not over-specify.

6. ERROR PATH. Always add at least one test for error/failure returns. Use
   `Result<T, Error>.Failure(...)` from repo mocks and assert `result.IsFailure`.

7. CANCELLATION TOKEN. Always pass `CancellationToken.None` in tests; do not omit it.

8. ASYNC. All handler test methods are `async Task`. No `.Result` or `.Wait()`.

9. NAMING: `HandleAsync_StateUnderTest_ExpectedBehavior`
   Examples:
   - `HandleAsync_FilingNotFound_ReturnsFailure`
   - `HandleAsync_ValidCommand_PersistsFilingWithCorrectStatus`

10. SHARED HELPERS. Put builder methods (e.g., `MakeFiling(...)`, `MakeCommand(...)`) in
    `private static` helpers or in `Rentier.Tests.Common` if reused across test classes.
```

### Example skeleton
```csharp
public class DeleteFilingCommandHandlerTests
{
    private readonly IFilingRepository _repo;
    private readonly DeleteFilingCommandHandler _handler;

    public DeleteFilingCommandHandlerTests()
    {
        _repo = Substitute.For<IFilingRepository>();
        _handler = new DeleteFilingCommandHandler(_repo);
    }

    [Fact]
    public async Task HandleAsync_FilingExists_DeletesAndReturnsSuccess()
    {
        var id = Guid.NewGuid();
        _repo.ExistsAsync(id, Arg.Any<CancellationToken>()).Returns(true);

        var result = await _handler.HandleAsync(new DeleteFilingCommand(id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _repo.Received(1).DeleteAsync(id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_FilingNotFound_ReturnsFailure()
    {
        _repo.ExistsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(false);

        var result = await _handler.HandleAsync(
            new DeleteFilingCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("FILING_NOT_FOUND");
    }
}
```

---

## 4. ViewModel Unit Tests

### When to write
Every **ViewModel** in `Rentier.Desktop` gets tests covering: initial state, command success path,
command failure path, observable derived properties, and navigation delegates.

### Recommended frameworks
| Framework | Package | Purpose |
|-----------|---------|---------|
| **xUnit 2.x** | `xunit` | Test runner |
| **FluentAssertions 6.x** | `FluentAssertions` | Assertions |
| **NSubstitute 5.x** | `NSubstitute` | Mock command/query handlers |
| **Avalonia 11.x** | `Avalonia` | Avalonia runtime (needed for some ReactiveUI types) |
| **Avalonia.ReactiveUI 11.x** | `Avalonia.ReactiveUI` | ReactiveUI scheduler support |
| **ReactiveUI** (transitive) | — | `ImmediateScheduler.Instance` for sync test execution |

### Custom instructions

```
## ViewModel Unit Tests — Copilot Instructions

1. PROJECT: `Rentier.Desktop.Tests`. References both `Rentier.Desktop` and `Rentier.Application`.

2. IMMEDIATE SCHEDULER. All ViewModels that accept an `IScheduler` must receive
   `ImmediateScheduler.Instance` in tests. This makes `ReactiveCommand` execute synchronously.
   Never use `TaskPoolScheduler.Default` or `RxApp.MainThreadScheduler` in tests.

3. ACTIVATABLE VMS. ViewModels that implement `IActivatableViewModel` (i.e., load-on-activate)
   must be activated before asserting loaded state:
   ```csharp
   using var _ = vm.Activator.Activate();
   vm.Items.Should().HaveCount(3);
   ```
   Always wrap in `using` to cleanly deactivate.

4. MOCK HANDLERS. Every `ICommandHandler<,>` or `IQueryHandler<,>` is mocked with NSubstitute.
   Use a private `Make*Handler()` factory method that returns a configured substitute.

5. RESULT<T, ERROR> RETURNS. Set up happy-path:
   ```csharp
   handler.HandleAsync(Arg.Any<...>(), Arg.Any<CancellationToken>())
       .Returns(Result<MyDto, Error>.Success(dto));
   ```
   Set up failure-path with `Result<MyDto, Error>.Failure(new Error("CODE", "message"))`.

6. STATE ASSERTIONS. Always test the three standard ViewModel states:
   - **Initial state** (before activation/execute): `IsLoading=false`, `ErrorMessage=null`
   - **Success state**: correct data loaded, `ErrorMessage=null`
   - **Failure state**: `ErrorMessage` set, data empty/unchanged

7. NAVIGATION DELEGATES. Pass a captured lambda to test navigation side effects:
   ```csharp
   var called = false;
   var vm = new MyViewModel(..., navigateTo: () => called = true);
   vm.NavigateCommand.Execute().Subscribe();
   called.Should().BeTrue();
   ```

8. NO UI RENDERING. ViewModel tests must not launch an Avalonia `Application` or open windows.
   They only instantiate the ViewModel class directly.

9. NAMING: `PropertyOrCommand_Scenario_ExpectedOutcome`
   Examples:
   - `LoadCommand_OnSuccess_PopulatesFilings`
   - `DeleteCommand_OnFailure_SetsErrorMessage`
   - `Constructor_InitializesWithDefaults`

10. COLLECTIONS. When testing `ObservableCollection` contents, use `.Should().HaveCount(n)` and
    `.Should().ContainSingle(x => x.Id == id)`. Do not index into collections directly.
```

### Example skeleton
```csharp
public class FilingsViewModelTests
{
    private static IQueryHandler<GetFilingsQuery, Result<FilingsPageResult, Error>>
        MakeQueryHandler(IReadOnlyList<FilingRowDto>? rows = null)
    {
        var h = Substitute.For<IQueryHandler<GetFilingsQuery, Result<FilingsPageResult, Error>>>();
        h.HandleAsync(Arg.Any<GetFilingsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<FilingsPageResult, Error>.Success(
                new FilingsPageResult(rows ?? [], rows?.Count ?? 0, 1)));
        return h;
    }

    [Fact]
    public void Constructor_InitializesWithDefaults()
    {
        var vm = new FilingsViewModel(MakeQueryHandler(), /* ... */ ImmediateScheduler.Instance);

        vm.IsLoading.Should().BeFalse();
        vm.ErrorMessage.Should().BeNull();
        vm.Filings.Should().BeEmpty();
    }
}
```

---

## 5. Infrastructure Integration Tests

### When to write
Test **EF Core repositories**, **parsers** (CSV, XML, HTML), **serializers**, and
**external service adapters**. Use an in-memory or on-disk SQLite database — never a real
PostgreSQL/SQL Server or live external service.

### Recommended frameworks
| Framework | Package | Purpose |
|-----------|---------|---------|
| **xUnit 2.x** | `xunit` | Test runner |
| **FluentAssertions 6.x** | `FluentAssertions` | Assertions |
| **NSubstitute 5.x** | `NSubstitute` | Mock external dependencies |
| **EF Core InMemory 8.x** | `Microsoft.EntityFrameworkCore.InMemory` | Fast in-memory EF provider |
| **EF Core SQLite 8.x** | `Microsoft.EntityFrameworkCore.Sqlite` | SQLite for migration/schema tests |
| **Embedded resources** | *(built-in)* | Fixture CSV/XML files embedded in test project |

### Custom instructions

```
## Infrastructure Integration Tests — Copilot Instructions

1. PROJECT: `Rentier.Infrastructure.Tests`. References `Rentier.Infrastructure` and
   `Rentier.Tests.Common`.

2. DB SETUP. Every repository test class uses a private helper to create an isolated
   `AppDbContext` with SQLite in-memory:
   ```csharp
   private static AppDbContext CreateContext()
   {
       var options = new DbContextOptionsBuilder<AppDbContext>()
           .UseSqlite("Data Source=:memory:")
           .Options;
       var ctx = new AppDbContext(options);
       ctx.Database.EnsureCreated();
       return ctx;
   }
   ```
   Each test gets its **own** context instance. Never share state across tests.

3. EF IN-MEMORY vs SQLITE. Prefer SQLite in-memory for repositories (respects FK constraints,
   indexes, migrations). Use EF InMemory only when testing pure EF logic without SQL semantics.

4. EMBEDDED FIXTURES. Parser tests load fixture files from embedded resources:
   ```csharp
   var assembly = typeof(IbkrCsvParserTests).Assembly;
   using var stream = assembly.GetManifestResourceStream(
       "Rentier.Infrastructure.Tests.Parsers.Fixtures.sample.csv")!;
   ```
   All `.csv`, `.xml`, `.txt` fixture files must be in `Parsers/Fixtures/` with
   `<EmbeddedResource>` in the `.csproj`.

5. DATEONLY BOUNDARY. When seeding test data, always use `DateOnly` via `new DateOnly(y,m,d)`.
   Never insert `DateTime` values into the DB directly.

6. DECIMAL PRECISION. When asserting money/rate values from DB, use exact `decimal` equality.
   Confirm EF Core value converters produce the expected precision.

7. TRAIT ANNOTATION. Integration tests that touch the DB must be tagged:
   ```csharp
   [Trait("Category", "Integration")]
   ```
   This allows CI to separate fast unit tests from slower integration tests.

8. PARSER TESTS. Each parser (IBKR CSV, XML serializer, HTML scraper) gets its own test class.
   Parse a known good fixture → assert exact DTOs. Parse a malformed fixture → assert a
   meaningful `Result.Failure` or exception.

9. REPOSITORY TESTS. For every public repository method, write:
   - A "found" / "success" case (seed data, call, assert)
   - A "not found" / "empty" case
   - A "constraint violation" or "duplicate" case if applicable

10. NO NETWORK. Tests must not hit real HTTP endpoints. If a service wraps an `HttpClient`,
    inject a mock or use `HttpMessageHandler` stub. Never rely on internet connectivity in CI.
```

### Example skeleton
```csharp
[Trait("Category", "Integration")]
public class FilingRepositoryTests
{
    private static AppDbContext CreateContext()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=:memory:").Options;
        var ctx = new AppDbContext(opts);
        ctx.Database.EnsureCreated();
        return ctx;
    }

    [Fact]
    public async Task GetByIdAsync_ExistingFiling_ReturnsFiling()
    {
        await using var ctx = CreateContext();
        var repo = new FilingRepository(ctx);
        var filing = FilingFactory.Create();
        await ctx.Filings.AddAsync(filing);
        await ctx.SaveChangesAsync();

        var result = await repo.GetByIdAsync(filing.Id, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Id.Should().Be(filing.Id);
    }
}
```

---

## 6. Property-Based Tests

### When to write
Use property-based testing for **financial calculation invariants** in `Rentier.Domain` where
exhaustive hand-crafted edge cases are hard to enumerate — e.g., tax calculation, rounding,
deadline shifting.

### Recommended frameworks
| Framework | Package | Purpose |
|-----------|---------|---------|
| **FsCheck 3.x** (recommended) | `FsCheck.Xunit` | Property-based testing; xUnit integration |
| **Hedgehog** (alternative) | `Hedgehog` | Functional property testing; better shrinking |

> **FsCheck is recommended** because it has first-class xUnit integration via `[Property]`
> attribute and is widely used in the .NET ecosystem.

### Custom instructions

```
## Property-Based Tests — Copilot Instructions

1. PROJECT: Add `FsCheck.Xunit` to `Rentier.Domain.Tests` (or a dedicated
   `Rentier.Domain.PropertyTests` project for isolation).

2. USE [Property] NOT [Fact]. Property-based tests use `[Property]` attribute from FsCheck.Xunit:
   ```csharp
   [Property]
   public Property TaxPayable_IsNeverNegative(
       PositiveDecimal income, PositiveDecimal wht)
   ```

3. FINANCIAL INVARIANTS TO TEST:
   - `TaxPayable` is always `>= 0`  (WHT cannot produce a negative tax bill)
   - `TaxPayable` is always `<= GrossTax`
   - `GrossIncome = Amount * Rate`, rounded to 2 decimal places AwayFromZero
   - Deadline is never a weekend or public holiday
   - Deadline is always `>= paymentDate + 30 days`

4. CUSTOM GENERATORS. Define `Arb` generators for domain types:
   ```csharp
   public static class Generators
   {
       public static Arbitrary<decimal> PositiveDecimal() =>
           Arb.Default.PositiveInt().Generator
               .Select(x => (decimal)x.Get / 100m)
               .ToArbitrary();
   }
   ```

5. SHRINKING. Let FsCheck shrink failing cases automatically. Do not write your own shrink
   logic unless the generator is extremely complex.

6. SEED REPRODUCIBILITY. When a property test fails, FsCheck reports the seed. Document the
   seed in the failure comment and add a regression `[Fact]` for that specific case.

7. KEEP PROPERTIES SIMPLE. Each `[Property]` method tests ONE invariant. Split complex
   scenarios into multiple properties.

8. AVOID I/O. Property-based tests in `Rentier.Domain.Tests` must not touch DB or network.
   Pass fake rate providers as in standard domain tests.
```

### Example skeleton
```csharp
public class TaxCalculationProperties
{
    [Property]
    public Property TaxPayable_NeverNegative(
        PositiveInt incomeInt, PositiveInt whtInt, PositiveInt rateInt)
    {
        var income = (decimal)incomeInt.Get;
        var wht    = (decimal)whtInt.Get;
        var rate   = (decimal)rateInt.Get / 100m + 0.01m; // avoid zero rate

        var result = TaxCalculationService.CalculateAsync(
            IncomeType.Dividend, "TEST", new DateOnly(2024, 1, 15),
            income, "USD", wht, "USD",
            (_, _) => Task.FromResult(new ExchangeRate(default, "USD", rate)))
            .GetAwaiter().GetResult();

        return (result.TaxPayableRsd >= 0m).ToProperty();
    }
}
```

---

## 7. Avalonia Headless UI Tests

### When to write
Test **Avalonia controls and views** when bindings, control templates, or visual state need
verification beyond ViewModel logic — e.g., that a button is disabled when `IsLoading=true`,
that a `DataGrid` renders the correct number of rows, or that a dialog appears on command.

### Recommended frameworks
| Framework | Package | Purpose |
|-----------|---------|---------|
| **Avalonia.Headless.XUnit 11.x** | `Avalonia.Headless.XUnit` | Headless Avalonia test runner |
| **Avalonia.Headless 11.x** | `Avalonia.Headless` | Core headless renderer |
| **xUnit 2.x** | `xunit` | Test runner |
| **FluentAssertions 6.x** | `FluentAssertions` | Assertions |

> **Setup requirement:** The test assembly must call `UseHeadless()` on the `AppBuilder`.
> Add an `[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]` attribute in the project.

### Custom instructions

```
## Avalonia Headless UI Tests — Copilot Instructions

1. PROJECT: Add `Avalonia.Headless.XUnit` to `Rentier.Desktop.Tests`.

2. APPBUILDER SETUP. Create a `TestAppBuilder` class once in the test project:
   ```csharp
   public class TestAppBuilder
   {
       public static AppBuilder BuildAvaloniaApp() =>
           AppBuilder.Configure<App>()
               .UseHeadless(new AvaloniaHeadlessOptions { UseHeadlessDrawing = true })
               .UseReactiveUI();
   }
   ```
   Register with assembly attribute: `[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]`

3. USE [AvaloniaFact] AND [AvaloniaTheory]. Replace `[Fact]` with `[AvaloniaFact]` and
   `[Theory]` with `[AvaloniaTheory]` for tests that need the Avalonia dispatcher.

4. WINDOW LIFECYCLE. Each test creates and shows a window, then disposes it:
   ```csharp
   [AvaloniaFact]
   public void DataGrid_WhenFilingsLoaded_RendersCorrectRowCount()
   {
       var window = new Window { Content = new FilingsView() };
       window.Show();
       // interact / assert
       window.Close();
   }
   ```

5. WAIT FOR RENDERING. Use `window.UpdateLayout()` or `Dispatcher.UIThread.RunJobs()` to flush
   pending layout/rendering before asserting visual state.

6. BINDING ASSERTIONS. Query controls by type/name using the Avalonia visual tree:
   ```csharp
   var grid = window.FindControl<DataGrid>("FilingsGrid");
   grid.Should().NotBeNull();
   grid!.Items.Cast<object>().Should().HaveCount(3);
   ```

7. KEEP TESTS NARROW. Headless UI tests are slower than ViewModel unit tests. Only use them
   to verify things you cannot test at the ViewModel level (template triggers, styles, converters
   that need a render pass).

8. SEPARATE TRAIT. Tag all headless tests:
   ```csharp
   [Trait("Category", "UI")]
   ```
   This allows CI to skip them on resource-constrained runners.

9. NO EXTERNAL DEPENDENCIES. Mock all application services (same as ViewModel tests). The UI
   test only verifies rendering/binding — not business logic.

10. CONVERTERS. Test `IValueConverter` implementations in isolation as standard `[Fact]` tests
    (no Avalonia runtime needed). Only use `[AvaloniaFact]` when the converter relies on
    Avalonia styling or control templates.
```

---

## 8. Scenario / Functional Tests

### When to write
Test **full vertical slices**: a command enters the Application layer, flows through domain logic,
persists to a real SQLite in-memory DB, and is read back. This validates that all layers wire
together correctly for the most critical business flows.

### Recommended frameworks
| Framework | Package | Purpose |
|-----------|---------|---------|
| **xUnit 2.x** | `xunit` | Test runner |
| **FluentAssertions 6.x** | `FluentAssertions` | Assertions |
| **EF Core SQLite 8.x** | `Microsoft.EntityFrameworkCore.Sqlite` | Realistic in-memory DB |
| **Microsoft.Extensions.DependencyInjection 8.x** | `Microsoft.Extensions.DependencyInjection` | Real DI container |

> **Project:** Create a dedicated `Rentier.Scenarios.Tests` project so these tests are
> clearly separated and can be run independently in CI.

### Custom instructions

```
## Scenario / Functional Tests — Copilot Instructions

1. PROJECT: `Rentier.Scenarios.Tests`. References Application, Infrastructure, Domain, and
   Tests.Common. Does NOT reference Desktop.

2. REAL IMPLEMENTATIONS. Scenario tests use real command/query handlers and real EF Core
   repositories. Only external services (IMAP, NBS HTTP, credential store) are mocked.

3. SCENARIO PER FEATURE. Name test classes after the user-facing workflow:
   `ImportAndProcessDividendScenario`, `FilingLifecycleScenario`, `TaxpayerProfileScenario`.

4. SETUP PATTERN. Each test class owns a `ScenarioFixture` that bootstraps the DI container
   with SQLite in-memory:
   ```csharp
   public class ScenarioFixture : IDisposable
   {
       public IServiceProvider Services { get; }
       public ScenarioFixture()
       {
           var services = new ServiceCollection();
           services.AddDbContext<AppDbContext>(o =>
               o.UseSqlite("Data Source=:memory:"));
           // register real repos and handlers
           // mock external adapters (NSubstitute)
           Services = services.BuildServiceProvider();
           Services.GetRequiredService<AppDbContext>().Database.EnsureCreated();
       }
       public void Dispose() =>
           Services.GetRequiredService<AppDbContext>().Dispose();
   }
   ```

5. GIVEN-WHEN-THEN NAMING:
   `{Feature}_{GivenCondition}_{ThenResult}`
   Examples:
   - `ImportDividend_CsvWithTwoRows_CreatesTwoReportsAndFilings`
   - `FilingLifecycle_FromInitToFiled_UpdatesStatusAndDeadline`

6. ASSERT END STATE. After executing a sequence of commands, query the DB (via a repository)
   to assert the final persisted state — not just the return value.

7. ERROR SCENARIOS. At least one test per scenario covers a failure path (duplicate import,
   missing rate, etc.) and asserts the partial-failure `Result` and DB state.

8. DATA ISOLATION. Each test case uses a fresh `ScenarioFixture` (via xUnit constructor injection
   or `IClassFixture`). Never share DB state between test methods.

9. TRAIT ANNOTATION:
   ```csharp
   [Trait("Category", "Scenario")]
   ```

10. NO UI. Scenario tests do not instantiate any ViewModel. They call handlers directly via
    the service provider.
```

---

## 9. Smoke / Wiring Tests

### When to write
Verify that the **DI container**, **DB schema**, and **assembly-level wiring** are configured
correctly. These tests catch registration mistakes and missing migrations before a release.

### Recommended frameworks
Same as Application Tests — xUnit + FluentAssertions + NSubstitute.

### Custom instructions

```
## Smoke / Wiring Tests — Copilot Instructions

1. LOCATION. Place smoke tests in the same test project as the layer being verified:
   - Application DI smoke tests → `Rentier.Application.Tests`
   - DB schema smoke tests → `Rentier.Infrastructure.Tests`
   - ViewModel wiring smoke tests → `Rentier.Desktop.Tests`

2. DI SMOKE TESTS. Resolve every registered service and assert it is not null. Use
   NSubstitute to stub any external services (IMAP, HTTP) that cannot be instantiated in CI.
   ```csharp
   var provider = BuildProductionContainer(withStubs: true);
   provider.GetRequiredService<IFilingRepository>().Should().NotBeNull();
   ```

3. DB SCHEMA SMOKE TEST. One test per DbContext: create the schema and verify key tables exist:
   ```csharp
   ctx.Database.EnsureCreated();
   ctx.Filings.Should().NotBeNull();
   // optionally: ctx.Database.GetPendingMigrations().Should().BeEmpty();
   ```

4. NAMING: `{Subject}_Constructed_DoesNotThrow` or `{Service}_Resolved_IsNotNull`.

5. RUN FAST. Smoke tests should complete in < 200 ms total. If a smoke test needs a real
   external service, it is not a smoke test — it is a live integration test (see §11).

6. SMOKE TESTS ARE NOT UNIT TESTS. They are allowed to touch DI and DB. But they must not
   test business logic — that belongs to domain or application tests.
```

---

## 10. Snapshot Tests

### When to write
Test **serialization output** (PP-OPO XML, exported CSV) by comparing the actual output against
a stored "golden file" snapshot. Particularly valuable for the `PpOpoXmlSerializer` where the
exact XML structure matters for tax authority submission.

### Recommended frameworks
| Framework | Package | Purpose |
|-----------|---------|---------|
| **Verify 23.x** (recommended) | `Verify.Xunit` | Snapshot/approval testing; diff on failure |
| *(manual file comparison)* | — | Simple alternative: embed expected output as embedded resource |

> **Verify** is recommended because it auto-manages snapshot files, produces clean diffs, and
> integrates natively with xUnit.

### Custom instructions

```
## Snapshot Tests — Copilot Instructions

1. PACKAGE. Add `Verify.Xunit` to `Rentier.Infrastructure.Tests`.

2. NAMING CONVENTION. Snapshot files are stored in `Snapshots/` subfolder alongside the test
   class. Verify handles naming automatically; do not manually create snapshot files.

3. USAGE PATTERN:
   ```csharp
   [Fact]
   public async Task Serialize_ValidFiling_MatchesSnapshot()
   {
       var filing = FilingFactory.Create();
       var xml = PpOpoXmlSerializer.Serialize(filing);
       await Verify(xml).UseExtension("xml");
   }
   ```

4. FIRST RUN. On first run, Verify creates the snapshot file. Review the output manually, then
   commit the snapshot file. On subsequent runs it is compared automatically.

5. DIFF ON FAILURE. When a snapshot test fails, Verify opens a diff tool (configured in
   `VerifySettings`). Always investigate diffs before updating snapshots.

6. UPDATE SNAPSHOTS. To update snapshots after intentional changes, run:
   `dotnet test -- --update-snapshots` or set `[ModifiesSnapshots]` attribute.

7. SCOPE. Use snapshot tests only for serialization output, not for domain logic. Keep the
   test data minimal: one representative filing, one representative CSV row.

8. NO DYNAMIC DATA IN SNAPSHOTS. Never include timestamps, GUIDs, or random data in snapshot
   output. Canonicalize all IDs and dates before snapshotting.
```

---

## 11. Live / Manual Integration Tests

### When to write
Tests that require a **real external service** (live IMAP server, NBS website, real credential
store). These are always skipped in CI and run manually on developer machines.

### Custom instructions

```
## Live / Manual Integration Tests — Copilot Instructions

1. ALWAYS SKIP IN CI. Use `[Fact(Skip = "Requires live IMAP server")]` or a custom
   `[LiveTest]` attribute that skips unless an environment variable is set.

2. LOCATION. Place in `Rentier.Infrastructure.Tests` in a dedicated file like
   `ImapSyncIntegrationTests.cs` or `NbsLiveTests.cs`.

3. CATEGORY TRAIT:
   ```csharp
   [Trait("Category", "Live")]
   ```
   CI pipeline filters with: `dotnet test --filter "Category!=Live"`

4. DOCUMENT PREREQUISITES. Every live test must have a comment explaining:
   - What external resource is required
   - How to configure credentials (env vars, local config — never hardcode)
   - What the expected outcome is

5. NEVER COMMIT CREDENTIALS. Live tests read configuration from environment variables or
   the OS credential store. Never commit passwords, API keys, or tokens.

6. CLEANUP. Live tests must clean up any data they create (test mailbox folders, test files).
   Use `try/finally` or xUnit `IDisposable` fixture to guarantee cleanup.
```

---

## 12. End-to-End Tests

### When to write
E2E tests launch the **real Avalonia desktop application** and simulate user interactions.
They are expensive, fragile, and slow — use sparingly for the most critical user flows only
(e.g., "user imports a CSV, views filings, marks one as filed").

### Recommended frameworks
| Framework | Package | Purpose |
|-----------|---------|---------|
| **Appium + WinAppDriver** | `Appium.WebDriver` | Windows UI automation for desktop apps |
| **Avalonia.UITests** (experimental) | community package | Avalonia-specific UI automation |
| **FlaUI** (alternative) | `FlaUI.Core`, `FlaUI.UIA3` | UIA3-based Windows automation |

> **Recommendation:** FlaUI is preferred for Avalonia desktop apps on Windows because it uses
> the UIA3 accessibility tree, which Avalonia exposes natively. Appium/WinAppDriver is more
> mature but heavier to set up.

### Custom instructions

```
## End-to-End Tests — Copilot Instructions

1. PROJECT: Dedicated `Rentier.E2E.Tests` project. Must NOT be included in the standard
   `dotnet test` run. Execute only via a separate CI job or manual trigger.

2. FRAMEWORK. Use FlaUI.UIA3 on Windows:
   ```csharp
   var app = FlaUI.Core.Application.Launch("Rentier.Desktop.exe");
   var automation = new UIA3Automation();
   var window = app.GetMainWindow(automation);
   ```

3. AUTOMATION IDs. All interactive Avalonia controls must have `AutomationProperties.AutomationId`
   set in XAML. E2E tests find controls by automation ID:
   ```csharp
   var btn = window.FindFirstDescendant(cf => cf.ByAutomationId("ImportButton")).AsButton();
   btn.Invoke();
   ```

4. TEST DATA ISOLATION. E2E tests use a dedicated test profile/database, never the user's real
   data. Point the app at a test SQLite file via environment variable or launch argument.

5. ALWAYS CLEAN UP. Kill the app process in `Dispose`/`IClassFixture` teardown regardless of
   test outcome.

6. ONE HAPPY PATH PER FEATURE. E2E tests cover only the critical golden path. Negative/edge
   cases are handled at lower test levels (Unit, Integration).

7. FLAKINESS MITIGATION:
   - Always wait for elements to appear with a timeout (never `Thread.Sleep`)
   - Use `WaitUntilEnabled` / `WaitUntilClickable` helpers
   - Re-run failed tests once automatically before marking as failed

8. CATEGORY TRAIT:
   ```csharp
   [Trait("Category", "E2E")]
   ```
   Filter from normal test runs: `dotnet test --filter "Category!=E2E"`

9. CI ENVIRONMENT. E2E tests require a Windows runner with a display. On CI, use a Windows
   GitHub Actions runner with virtual display or use Avalonia headless mode as a substitute
   for simple cases.

10. ARIA/ACCESSIBILITY FIRST. Write E2E tests by selecting elements via `AutomationId` or
    accessible name — never by position or pixel coordinates. This makes tests resilient to
    layout changes and simultaneously validates accessibility.
```

---

## 13. Coverage & CI Guidance

### Coverage targets by layer

| Layer | Target | Rationale |
|-------|--------|-----------|
| Domain | ≥ 95% | Pure logic, no I/O — high coverage is cheap |
| Application | ≥ 85% | Handlers have well-defined inputs/outputs |
| Infrastructure | ≥ 70% | External adapters are harder to fully stub |
| Desktop (ViewModels) | ≥ 80% | Commands, state, navigation |
| Desktop (Views/XAML) | excluded | XAML binding coverage measured via UI tests |

### Coverage collection

```bash
dotnet test --collect:"XPlat Code Coverage" --results-directory ./coverage
dotnet tool run reportgenerator -reports:coverage/**/*.xml -targetdir:coverage/report -reporttypes:Html
```

All test projects already include `coverlet.collector` — no additional setup needed.

---

## 14. GitHub Actions CI/CD Strategy

### Design goals
- **Fail fast** — cheapest tests run first; expensive tests are gated on them
- **Parallel jobs** — unit, integration, and UI tiers run concurrently after the unit job passes
- **Cross-platform** — critical tests run on `ubuntu-latest` and `windows-latest`
- **Efficient** — live and E2E tests never run on every push; they need an explicit trigger

### Job topology

```
push / pull_request
        │
        ▼
┌───────────────────┐
│   Job: unit       │  ubuntu-latest + windows-latest (matrix)
│   < 60 s          │  Domain.Tests + Application.Tests
│   Fast; no DB     │  --filter "Category!=Integration&Category!=Scenario&Category!=UI&Category!=Live&Category!=E2E"
└────────┬──────────┘
         │ needs: unit (all matrix legs must pass)
         ▼
┌──────────────────────┐   ┌─────────────────────────────┐
│   Job: integration   │   │   Job: ui                   │
│   ubuntu-latest      │   │   ubuntu-latest             │
│   < 3 min            │   │   < 3 min                   │
│                      │   │                             │
│   Infrastructure.    │   │   Desktop.Tests             │
│   Tests +            │   │   (Avalonia headless)       │
│   Scenarios.Tests    │   │   --filter "Category=UI"    │
│   --filter           │   │                             │
│   "Category=Integra- │   │                             │
│   tion|Category=     │   │                             │
│   Scenario"          │   │                             │
└──────────────────────┘   └─────────────────────────────┘
         │                           │
         └───────────┬───────────────┘
                     │ both pass
                     ▼
         ┌────────────────────┐
         │   Job: coverage    │
         │   merge + report   │
         │   (ubuntu-latest)  │
         └────────────────────┘

workflow_dispatch / schedule (weekly)
        │
        ▼
┌───────────────────┐
│   Job: e2e        │  windows-latest only
│   (FlaUI/WinApp)  │  --filter "Category=E2E"
│   Manual trigger  │
└───────────────────┘
```

### Complete annotated workflow

```yaml
# .github/workflows/tests.yml
name: Tests

on:
  push:
    branches: [main, develop]
  pull_request:

jobs:
  # ─────────────────────────────────────────────────────────────────
  # Tier 1: Unit tests — fast, no I/O, runs on every push
  # ─────────────────────────────────────────────────────────────────
  unit:
    name: Unit Tests (${{ matrix.os }})
    runs-on: ${{ matrix.os }}
    strategy:
      fail-fast: true           # cancel sibling if one OS fails
      matrix:
        os: [ubuntu-latest, windows-latest]

    steps:
      - uses: actions/checkout@v4

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: "8.x"

      - name: Restore
        run: dotnet restore

      - name: Build
        run: dotnet build --no-restore --configuration Release

      - name: Unit Tests
        run: >
          dotnet test
          --no-build
          --configuration Release
          --filter "Category!=Integration&Category!=Scenario&Category!=UI&Category!=Live&Category!=E2E"
          --collect:"XPlat Code Coverage"
          --results-directory ./coverage/unit
          --logger "github-actions"

      - name: Upload coverage
        uses: actions/upload-artifact@v4
        with:
          name: coverage-unit-${{ matrix.os }}
          path: coverage/unit/**/*.xml

  # ─────────────────────────────────────────────────────────────────
  # Tier 2a: Integration + Scenario tests — needs DB, gated on unit
  # ─────────────────────────────────────────────────────────────────
  integration:
    name: Integration & Scenario Tests
    runs-on: ubuntu-latest
    needs: unit                 # only runs if all unit matrix legs pass

    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: "8.x"

      - name: Restore & Build
        run: dotnet build --configuration Release

      - name: Integration & Scenario Tests
        run: >
          dotnet test
          --no-build
          --configuration Release
          --filter "Category=Integration|Category=Scenario"
          --collect:"XPlat Code Coverage"
          --results-directory ./coverage/integration
          --logger "github-actions"

      - name: Upload coverage
        uses: actions/upload-artifact@v4
        with:
          name: coverage-integration
          path: coverage/integration/**/*.xml

  # ─────────────────────────────────────────────────────────────────
  # Tier 2b: Avalonia Headless UI tests — gated on unit
  # ─────────────────────────────────────────────────────────────────
  ui:
    name: UI Tests (Avalonia Headless)
    runs-on: ubuntu-latest
    needs: unit

    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: "8.x"

      - name: Restore & Build
        run: dotnet build --configuration Release

      - name: UI Tests
        run: >
          dotnet test
          --no-build
          --configuration Release
          --filter "Category=UI"
          --collect:"XPlat Code Coverage"
          --results-directory ./coverage/ui
          --logger "github-actions"
        env:
          # Required by Avalonia headless on Linux (no display needed with headless, but set for safety)
          DISPLAY: ""

      - name: Upload coverage
        uses: actions/upload-artifact@v4
        with:
          name: coverage-ui
          path: coverage/ui/**/*.xml

  # ─────────────────────────────────────────────────────────────────
  # Tier 3: Coverage merge — runs after all tier-2 jobs pass
  # ─────────────────────────────────────────────────────────────────
  coverage:
    name: Coverage Report
    runs-on: ubuntu-latest
    needs: [integration, ui]

    steps:
      - uses: actions/checkout@v4

      - name: Download all coverage artifacts
        uses: actions/download-artifact@v4
        with:
          pattern: coverage-*
          merge-multiple: true
          path: ./coverage-all

      - name: Install ReportGenerator
        run: dotnet tool install --global dotnet-reportgenerator-globaltool

      - name: Merge & generate HTML report
        run: >
          reportgenerator
          -reports:"coverage-all/**/*.xml"
          -targetdir:"coverage-report"
          -reporttypes:"Html;Cobertura;MarkdownSummaryGithub"

      - name: Publish coverage to GitHub Summary
        run: cat coverage-report/SummaryGithub.md >> $GITHUB_STEP_SUMMARY

      - name: Upload HTML report
        uses: actions/upload-artifact@v4
        with:
          name: coverage-report
          path: coverage-report/

  # ─────────────────────────────────────────────────────────────────
  # Tier 4: E2E tests — Windows only, manual trigger or weekly schedule
  # ─────────────────────────────────────────────────────────────────
  e2e:
    name: E2E Tests (Windows / FlaUI)
    runs-on: windows-latest
    if: github.event_name == 'workflow_dispatch' || github.event_name == 'schedule'

    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: "8.x"

      - name: Build (Release)
        run: dotnet build --configuration Release

      - name: E2E Tests
        run: >
          dotnet test
          --no-build
          --configuration Release
          --filter "Category=E2E"
          --logger "github-actions"
        env:
          RENTIER_TEST_DB: ":memory:"   # point app at isolated test DB
```

### Key decisions explained

| Decision | Rationale |
|----------|-----------|
| Unit job runs on `matrix: [ubuntu, windows]` | Catches path-separator bugs and platform-specific behavior early |
| Integration job is `ubuntu-latest` only | SQLite in-memory works identically on all platforms; saving Windows runner minutes |
| `needs: unit` on integration + UI | Prevents wasting expensive runner time if basic logic is broken |
| Integration and UI jobs run **in parallel** | They are independent; combined they finish as fast as the slower one (~3 min vs ~6 min serial) |
| Coverage merged in a separate job | Avoids uploading partial coverage; ensures all tiers contribute before reporting |
| E2E gated behind `workflow_dispatch` | E2E tests are fragile and slow; should not block every PR |
| `--logger "github-actions"` | Native test result annotation in the GitHub PR interface (no plugin needed) |

### xUnit parallelism within a job

By default xUnit 2.x parallelizes test collections across test assemblies.
When running `dotnet test` on the solution with a filter, **multiple assemblies run in parallel automatically**.
No `xunit.runner.json` tuning is needed for the current test count.

If a future build becomes slow, add `xunit.runner.json` to the slowest project:
```json
{
  "$schema": "https://xunit.net/schema/current/xunit.runner.schema.json",
  "parallelizeAssembly": true,
  "parallelizeTestCollections": true,
  "maxParallelThreads": 4
}
```

> ⚠️ Never parallelize tests that share a `DbContext` instance or write to the same SQLite file.
> Each infrastructure test already uses an isolated `CreateContext()` — they are safe to parallelize.

---

*Document generated: 2026-04-15 | Rentier v1.x | .NET 8 | Avalonia 11 | ReactiveUI 20*
