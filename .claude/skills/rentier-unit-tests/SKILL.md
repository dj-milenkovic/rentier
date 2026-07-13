---
name: rentier-unit-tests
description: >
  Write pure unit tests for Rentier — fast, in-process, no I/O. Use this skill whenever
  adding or changing Domain code (entities, value objects, domain services, status machines),
  Application CQRS handlers (commands, queries, their handlers), or fixing any business-logic
  bug. If new domain logic or a handler is written without a test, proactively bring in this
  skill. Also use it when asked "what tests do I need for this class?" for anything in
  Rentier.Domain or Rentier.Application.
---

# Rentier Unit Tests

Unit tests are the cheapest, most reliable tests in the project. They run in milliseconds,
require no database, and cover the business rules that matter most. Write them first.

## Two Kinds of Unit Tests

| Kind | Project | What it tests | Mocks? |
|---|---|---|---|
| **Domain unit** | `Rentier.UnitTests` | Entities, value objects, domain services | Never |
| **Application unit** | `Rentier.UnitTests` | CQRS command/query handlers | Yes — mock the repos |

---

## Domain Unit Tests

### The One Non-Negotiable Rule

**Never use `Substitute.For<>()` in a domain test.** If you reach for a mock, the domain
class has an external dependency it shouldn't. Fix the design instead.

Domain services that need external data (e.g. exchange rates) receive a `Func<>` delegate —
pass a lambda in the test, not an interface mock:

```csharp
Func<DateOnly, string, Task<ExchangeRate>> fixedRate =
    (date, currency) => Task.FromResult(new ExchangeRate(date, currency, 117.21m));

var result = await TaxCalculationService.CalculateAsync(
    IncomeType.Dividend, "AAPL", new DateOnly(2024, 1, 15),
    100m, "USD", 15m, "USD",
    fixedRate);
```

To capture what the delegate received, use a closure variable:
```csharp
string? capturedCurrency = null;
Func<DateOnly, string, Task<ExchangeRate>> capturingRate = (date, currency) => {
    capturedCurrency = currency;
    return Task.FromResult(new ExchangeRate(date, currency, 117m));
};
```

### Financial Precision

```csharp
// CORRECT — exact decimal comparison
result.TaxPayableRsd.Should().Be(1758.15m);

// WRONG — never cast to double; loses precision
result.TaxPayableRsd.Should().Be((double)1758.15); // ❌

// CORRECT — DateOnly everywhere
var paymentDate = new DateOnly(2024, 3, 15);

// WRONG — never DateTime in domain tests
var paymentDate = new DateTime(2024, 3, 15); // ❌
```

When writing `[Theory]` + `[InlineData]` for decimal inputs, use the `m` suffix directly
in the data attribute. Never use `double` parameters and cast:

```csharp
// CORRECT
[Theory]
[InlineData(100.00m, 10, 3.00m)]
[InlineData(500.00m,  1, 1.50m)]
public void Calculate_VariousInputs_ReturnsCorrectPenalty(
    decimal originalTax, int daysLate, decimal expected) { ... }

// WRONG — double in InlineData → precision risk
[Theory]
[InlineData(-0.01)]        // ❌ double literal
public void Calculate_NegativeInput_Throws(double rawTax) // ❌
    => PenaltyCalculator.Calculate((decimal)rawTax, 1);   // ❌ double cast
```

### Testing Domain Exceptions

```csharp
// Synchronous throw
var act = () => filing.MarkFiled();
act.Should().Throw<DomainException>().WithMessage("*Invalid*");

// Async throw
var act = () => TaxCalculationService.CalculateAsync(...);
await act.Should().ThrowAsync<DomainException>().WithMessage("*negative*");
```

Use `*keyword*` wildcards — not the full message string. Messages can be reworded;
the intent keyword should not change.

### Status Transition Tests

Every entity with a state machine (e.g. `Filing`: Init → Filed → Paid) needs:
1. A test for each **valid** transition
2. A test for each **invalid** transition

```csharp
public class FilingStatusTransitionTests
{
    [Fact] public void MarkFiled_FromInitStatus_TransitionsToFiled() { ... }
    [Fact] public void MarkPaid_FromFiledStatus_TransitionsToPaid() { ... }
    [Fact] public void MarkFiled_FromPaidStatus_ThrowsDomainException() { ... }
    [Fact] public void MarkPaid_FromInitStatus_ThrowsDomainException() { ... }
}
```

### Namespace

Use flat namespace: `Rentier.UnitTests.Domain` — not `Rentier.UnitTests.Domain.Services`.

---

## Application Unit Tests (CQRS Handlers)

### One Test Class Per Handler

```
AddMailboxCommandHandler  →  AddMailboxCommandHandlerTests
GetFilingsQueryHandler    →  GetFilingsQueryHandlerTests
```

Never combine handlers in one test class.

### Standard Structure

Initialize substitutes in the constructor, not in a setup method.
xUnit constructs a new instance per test — each gets clean substitutes automatically:

```csharp
public class DeleteFilingCommandHandlerTests
{
    private readonly IFilingRepository _repo;
    private readonly DeleteFilingCommandHandler _handler;

    public DeleteFilingCommandHandlerTests()
    {
        _repo    = Substitute.For<IFilingRepository>();
        _handler = new DeleteFilingCommandHandler(_repo);
    }
}
```

### What to Test Per Handler

1. **Happy path** — valid input, correct output, repo called with right args
2. **Not-found / empty** — entity missing → `Result.Failure` or empty collection
3. **Validation failure** — invalid command data (if the handler validates)
4. **Repository interaction** — `Received(1)` on the calls that matter
5. **Error propagation** — repo returns failure → handler passes it through

```csharp
[Fact]
public async Task HandleAsync_FilingExists_DeletesAndReturnsSuccess()
{
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
```

### Async Rules

Every test is `async Task`. Always pass `CancellationToken.None` — never omit it:

```csharp
// CORRECT
var result = await _handler.HandleAsync(command, CancellationToken.None);

// WRONG — omitting the token silently allows the handler to ignore cancellation in tests
var result = await _handler.HandleAsync(command); // ❌
```

### Result<T, Error> — Both Branches

Always test success and failure paths:

```csharp
result.IsSuccess.Should().BeTrue();
result.Value.Should().Be(expectedValue);
// and
result.IsFailure.Should().BeTrue();
result.Error.Code.Should().Be("EXPECTED_ERROR_CODE");
```

### DI Smoke Test

When a new handler is registered, add a line to `DiRegistrationSmokeTests`:

```csharp
provider.GetRequiredService<ICommandHandler<MyNewCommand, Result<VoidResult, Error>>>()
    .Should().NotBeNull();
```

---

## Shared Naming Convention

Both domain and application unit tests use the same pattern:

```
MethodName_StateUnderTest_ExpectedBehavior
```

| Layer | Examples |
|---|---|
| Domain | `Calculate_ZeroDaysLate_ReturnsZero` |
| Domain | `MarkFiled_FromPaidStatus_ThrowsDomainException` |
| Application | `HandleAsync_FilingNotFound_ReturnsFailure` |
| Application | `HandleAsync_ValidCommand_PersistsFilingWithCorrectStatus` |

---

## Parametric Tests

Use `[Theory]` + `[InlineData]` for boundary values and equivalent partitions:

```csharp
[Theory]
[InlineData(null)]
[InlineData("")]
[InlineData("   ")]
public void Constructor_NullOrWhitespaceName_ThrowsDomainException(string? name)
{
    var act = () => new Mailbox(name!, "imap.example.com", 993);
    act.Should().Throw<DomainException>();
}
```

Use `[MemberData]` when data involves complex objects.

---

## Anti-Patterns to Avoid

| Anti-pattern | Fix |
|---|---|
| `Substitute.For<IRepo>()` in a domain test | Domain is pure — no repos |
| `new DateTime(...)` for dates | `new DateOnly(y, m, d)` |
| `(double)result.Amount` | Compare `decimal` directly |
| `double` in `[InlineData]` for monetary params | Use `m` suffix: `117.21m` |
| Catching `DomainException` in `try/catch` | Use `.Should().Throw<DomainException>()` |
| Omitting `CancellationToken.None` in handler calls | Always pass it explicitly |
| Multiple behaviors in one `[Fact]` | One behavior per test |
