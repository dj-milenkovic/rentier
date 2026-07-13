---
name: rentier-ui-tests
description: >
  Write UI tests for Rentier.Desktop — ReactiveUI ViewModel state machines, reactive commands,
  observable properties, navigation delegates, and Avalonia headless rendering. Use this skill
  whenever a ViewModel is added or changed in Rentier.Desktop (DashboardViewModel, FilingsViewModel,
  SyncViewModel, SettingsViewModel, etc.), when fixing bugs in UI state or command behavior,
  when asked "what tests do I need for this ViewModel?", or when implementing any ReactiveCommand,
  IActivatableViewModel lifecycle, navigation delegate, or IValueConverter. Also use it for
  Avalonia headless control rendering tests that require the Avalonia application context.
  If ViewModel or UI code is written without tests, proactively suggest this skill.
---

# Rentier UI Tests

UI tests in Rentier split into two subtypes:

| Subtype | What it tests | Avalonia app needed? | Test project |
|---|---|---|---|
| **ViewModel unit** | ReactiveUI state, commands, navigation | No | `Rentier.UnitTests` |
| **Headless UI** | XAML rendering, bindings, visual state | Yes (headless) | `Rentier.UnitTests` |

Write ViewModel unit tests for all ViewModels. Write headless tests only when the bug is
in XAML binding or visual state, not ViewModel logic.

---

## ViewModel Unit Tests

### The Synchronous Scheduler — Non-Negotiable

ReactiveUI executes commands asynchronously by default. In tests, inject
`ImmediateScheduler.Instance` so all observables fire synchronously:

```csharp
var vm = new FilingsViewModel(
    queryHandler,
    commandHandler,
    ImmediateScheduler.Instance);   // ← makes ReactiveCommand synchronous
```

**Never** pass `RxSchedulers.MainThreadScheduler`, `TaskPoolScheduler.Default`, or `null` in tests —
they cause timing-dependent test flakiness that's hard to diagnose.

### Activating ViewModels

ViewModels that implement `IActivatableViewModel` and load data in `WhenActivated` must be
explicitly activated before asserting loaded state. Wrap in `using` for clean deactivation:

```csharp
[Fact]
public void LoadCommand_OnSuccess_PopulatesFilings()
{
    var vm = CreateVm(handler: MakeHandler(filings));

    using var _ = vm.Activator.Activate();   // triggers WhenActivated block

    vm.Filings.Should().HaveCount(3);
    vm.ErrorMessage.Should().BeNull();
}
```

Forgetting `Activate()` is the #1 cause of false-green tests — the data never loads and
empty-collection assertions pass vacuously.

### Three States — Always Test All Three

Every ViewModel that loads data must have tests for the three lifecycle states:

#### 1. Initial state (before activation)
```csharp
[Fact]
public void Constructor_InitializesWithDefaults()
{
    var vm = CreateVm();
    vm.IsLoading.Should().BeFalse();
    vm.ErrorMessage.Should().BeNull();
    vm.Filings.Should().BeEmpty();
}
```

#### 2. Success state (handler returns data)
```csharp
[Fact]
public void LoadCommand_OnSuccess_PopulatesCollection()
{
    var vm = CreateVm(handler: MakeHandler(rows: [row1, row2]));
    using var _ = vm.Activator.Activate();

    vm.Filings.Should().HaveCount(2);
    vm.ErrorMessage.Should().BeNull();
}
```

#### 3. Failure state (handler returns an error)
```csharp
[Fact]
public void LoadCommand_OnFailure_SetsErrorMessage()
{
    var vm = CreateVm(handler: MakeFailingHandler("Load failed"));
    using var _ = vm.Activator.Activate();

    vm.ErrorMessage.Should().Be("Load failed");
    vm.Filings.Should().BeEmpty();
}
```

### Factory Helpers — Centralize Substitute Setup

Declare handler and ViewModel factories as private static methods. This keeps test bodies
readable and ensures all tests in the class use consistent wiring:

```csharp
private static IQueryHandler<GetFilingsQuery, Result<FilingsPageResult, Error>>
    MakeHandler(IReadOnlyList<FilingRowDto>? rows = null)
{
    var h = Substitute.For<IQueryHandler<GetFilingsQuery, Result<FilingsPageResult, Error>>>();
    h.HandleAsync(Arg.Any<GetFilingsQuery>(), Arg.Any<CancellationToken>())
        .Returns(Result<FilingsPageResult, Error>.Success(
            new FilingsPageResult(rows ?? [], rows?.Count ?? 0, 1)));
    return h;
}

private static IQueryHandler<GetFilingsQuery, Result<FilingsPageResult, Error>>
    MakeFailingHandler(string message = "Load failed")
{
    var h = Substitute.For<IQueryHandler<GetFilingsQuery, Result<FilingsPageResult, Error>>>();
    h.HandleAsync(Arg.Any<GetFilingsQuery>(), Arg.Any<CancellationToken>())
        .Returns(Result<FilingsPageResult, Error>.Failure(new Error("LOAD_ERROR", message)));
    return h;
}

private static FilingsViewModel CreateVm(
    IQueryHandler<GetFilingsQuery, Result<FilingsPageResult, Error>>? handler = null)
    => new(
        handler ?? MakeHandler(),
        Substitute.For<ICommandHandler<DeleteFilingCommand, Result<VoidResult, Error>>>(),
        ImmediateScheduler.Instance);
```

### Testing Navigation Delegates

ViewModels receive navigation as delegates (not `INavigationService`) to stay testable.
Capture calls with a lambda:

```csharp
[Fact]
public void NavigateToFilingsCommand_Executed_CallsDelegate()
{
    var called = false;
    var vm = new DashboardViewModel(
        MakeHandler(),
        navigateToFilings: () => called = true,
        ImmediateScheduler.Instance);

    vm.NavigateToFilingsCommand.Execute().Subscribe();

    called.Should().BeTrue();
}
```

### Derived and Formatted Properties

Test computed/display properties explicitly:

```csharp
[Fact]
public void TotalUnpaidDisplay_WithAmount_FormatsAsRsd()
{
    var vm = CreateVm(handler: MakeHandler(dto: new DashboardDto(totalUnpaid: 1234.56m)));
    using var _ = vm.Activator.Activate();

    vm.TotalUnpaidDisplay.Should().Be("1,234.56 RSD");
}

[Fact]
public void LastSyncDisplay_WhenNeverSynced_ShowsNever()
{
    var vm = CreateVm();
    using var _ = vm.Activator.Activate();

    vm.LastSyncDisplay.Should().Be("Never");
}
```

### Collection Assertions

Prefer semantic assertions over index access:

```csharp
// GOOD
vm.Filings.Should().HaveCount(2);
vm.Filings.Should().ContainSingle(f => f.PayingEntity == "AAPL");

// FRAGILE — fails if ordering changes
vm.Filings[0].PayingEntity.Should().Be("AAPL"); // ❌ unless ordering is guaranteed
```

### Value Converter Tests

Test `IValueConverter` implementations as plain `[Fact]` — no Avalonia runtime needed:

```csharp
[Fact]
public void DateOnlyToStringConverter_ValidDate_FormatsAsIso()
{
    var converter = new DateOnlyToStringConverter();
    var result = converter.Convert(new DateOnly(2024, 7, 15), typeof(string), null, null);
    result.Should().Be("2024-07-15");
}
```

---

## Avalonia Headless UI Tests

Use headless tests only when the issue is in XAML binding or visual control state —
not in ViewModel logic. Requires `Avalonia.Headless.XUnit` package.

Mark these tests so CI can isolate them:

```csharp
[Trait("Category", "UI")]
public class FilingsViewHeadlessTests : AvaloniaTest
{
    [AvaloniaFact]
    public void FilingsDataGrid_WhenViewModelHasRows_ShowsRows()
    {
        // Arrange
        var vm = FilingsViewModelFactory.WithTwoFilings();
        var view = new FilingsView { DataContext = vm };

        // Act — open in headless window
        using var window = new HeadlessWindow();
        window.Show(view);
        window.Pulse();   // lets Avalonia process bindings

        // Assert
        var grid = view.FindControl<DataGrid>("FilingsGrid")!;
        grid.ItemsSource.Should().HaveCount(2);
    }
}
```

Run headless tests with: `dotnet test --filter "Category=UI"`

---

## Naming Convention

```
PropertyOrCommand_Scenario_ExpectedOutcome
```

Examples:
- `Constructor_InitializesWithDefaults`
- `LoadCommand_OnSuccess_PopulatesFilings`
- `DeleteCommand_FilingNotFound_SetsErrorMessage`
- `NavigateToFilingsCommand_Executed_CallsDelegate`
- `TotalUnpaidDisplay_WithLargeAmount_FormatsWithThousandsSeparator`
- `LastSyncDisplay_WhenNeverSynced_ShowsNever`

---

## Anti-Patterns to Avoid

| Anti-pattern | Fix |
|---|---|
| `RxSchedulers.MainThreadScheduler` in tests | `ImmediateScheduler.Instance` |
| Forgetting `vm.Activator.Activate()` | Wrap in `using var _ = vm.Activator.Activate()` |
| Testing only the success path | Always test initial + success + failure states |
| Inline `Substitute.For<>()` in every test | Extract `MakeHandler()` / `CreateVm()` factories |
| `vm.Filings[0].X.Should().Be(...)` | Semantic assertions: `.ContainSingle(f => ...)` |
| Opening an Avalonia `Window` in ViewModel tests | Instantiate VM directly — no rendering |
