---
name: rentier-assertions
description: >
  Idiomatic FluentAssertions patterns for Rentier-specific types: Result<T,Error>, DateOnly,
  decimal money, DomainException, and domain collections. Use this skill when writing assertions
  in any test layer — especially when asserting financial values, domain errors, or Result
  success/failure paths. Prevents precision loss from double-casting, fragile full-message
  matching, and index-based collection assertions. Invoke whenever a developer writes
  .Should()... or Assert. on a Rentier domain type.
---

# Rentier Assertion Patterns

FluentAssertions is the assertion library across all test layers.
Financial correctness is the highest priority — get the assertion type right first.

---

## Result<T, Error> — Always Test Both Branches

Every Application handler returns `Result<T, Error>`. Write a test for each path.

```csharp
// ── Success path ─────────────────────────────────────────────────────────
result.IsSuccess.Should().BeTrue();
result.Value.Should().NotBeNull();
result.Value.Id.Should().Be(expectedId);

// ── Failure path ─────────────────────────────────────────────────────────
result.IsFailure.Should().BeTrue();
result.Error.Code.Should().Be("FILING_NOT_FOUND");
result.Error.Message.Should().Contain("not found");     // partial match is intentional

// ── With because: for non-obvious assertions ─────────────────────────────
result.IsFailure.Should().BeTrue(
    because: "Init → Paid is not a valid Filing status transition");
```

Never access `result.Error` after asserting `IsSuccess` — it throws.
Never access `result.Value` after asserting `IsFailure` — it throws.

---

## Decimal Money — Exact Comparison, Never Double

```csharp
// ✅ CORRECT — exact decimal literal
result.TaxPayableRsd.Should().Be(1758.15m);
result.GrossIncomeRsd.Should().Be(11721.00m);
result.TaxPayableRsd.Should().Be(0m,
    because: "WHT credit fully offsets Serbian tax");

// ❌ WRONG — never cast to double; loses precision
result.TaxPayableRsd.Should().Be((double)1758.15);         // ❌
result.TaxPayableRsd.Should().BeApproximately(1758.15, 0.01); // ❌ not for money

// ✅ CORRECT — [Theory][InlineData] with m suffix
[Theory]
[InlineData(100.00m, 117.21m, 11721.00m)]
[InlineData(  0.01m, 117.21m,     1.17m)]
public void Calculate_KnownRate_ProducesCorrectGross(
    decimal income, decimal rate, decimal expectedGross) { ... }

// ❌ WRONG — double literals in InlineData → precision risk
[Theory]
[InlineData(100.0, 117.21, 11721.0)]  // ❌
```

---

## DateOnly — Never DateTime

```csharp
// ✅ CORRECT
result.FilingDeadline.Should().Be(new DateOnly(2024, 7, 15));
result.IncomeDate.Should().Be(new DateOnly(2024, 3, 15));
result.FilingDeadline.Should().Be(incomeDate.AddDays(30));

// ✅ Asserting a deadline is a business day
result.FilingDeadline.DayOfWeek.Should().NotBe(DayOfWeek.Saturday);
result.FilingDeadline.DayOfWeek.Should().NotBe(DayOfWeek.Sunday);

// ❌ WRONG
result.FilingDeadline.Should().Be(new DateTime(2024, 7, 15));  // ❌

// ✅ Round-trip through EF Core value converter (integration tests)
loaded.PaymentDate.Should().Be(new DateOnly(2024, 3, 15));  // no time component
loaded.TaxPayableRsd.Should().Be(1758.15m);                  // exact decimal
```

---

## DomainException — Fluent Throw Assertions

```csharp
// ✅ Synchronous throw
var act = () => filing.AdvanceStatus(FilingStatus.Paid);   // invalid: skips Filed
act.Should().Throw<DomainException>().WithMessage("*Invalid*");

// ✅ Async throw
var act = () => TaxCalculationService.CalculateAsync(
    IncomeType.Dividend, "AAPL", date, -1m, "USD", 0m, "USD", rate,
    TestContext.Current.CancellationToken);
await act.Should().ThrowAsync<DomainException>().WithMessage("*negative*");

// ✅ Wildcard — not the full message string (messages can be reworded)
act.Should().Throw<DomainException>().WithMessage("*transition*");         // ✅
act.Should().Throw<DomainException>()
   .WithMessage("Invalid Filing status transition from Init to Paid");     // ❌ too brittle

// ❌ WRONG — catch blocks in tests
try { filing.MarkPaid(); Assert.Fail("expected exception"); }
catch (DomainException) { }   // ❌ never catch in tests
```

---

## Collections — Semantic Over Index Access

```csharp
// ✅ Count assertions
result.Value.Should().HaveCount(3);
result.Value.Should().BeEmpty();
result.Value.Should().NotBeEmpty();

// ✅ Semantic content assertions
result.Value.Should().ContainSingle(f => f.PayingEntity == "AAPL");
result.Value.Should().Contain(f => f.Status == FilingStatus.Filed);
result.Value.Should().AllSatisfy(f =>
    f.TaxPayableRsd.Should().BeGreaterThanOrEqualTo(0m));

// ✅ Ordered assertions — only when ordering is guaranteed by the repository
result.Value.Should().BeInAscendingOrder(f => f.IncomeDate);

// ✅ Equivalence for ID sets
result.Value.Select(f => f.Id).Should().BeEquivalentTo(expectedIds);

// ❌ WRONG — index access when ordering is not guaranteed
result.Value[0].PayingEntity.Should().Be("AAPL");  // ❌ fragile
```

---

## NSubstitute Call Verification

```csharp
// ✅ Called exactly once with any CancellationToken
await _repo.Received(1).DeleteAsync(expectedId, Arg.Any<CancellationToken>());

// ✅ Called with specific argument predicate
await _repo.Received(1).SaveAsync(
    Arg.Is<Filing>(f => f.Status == FilingStatus.Filed && f.Id == filing.Id),
    Arg.Any<CancellationToken>());

// ✅ Never called
await _repo.DidNotReceive().SaveAsync(Arg.Any<Filing>(), Arg.Any<CancellationToken>());

// ❌ WRONG — asserting only the Result without verifying side-effects
result.IsSuccess.Should().BeTrue();  // did the repo actually get called?
```

---

## Assertion Order Convention

Always assert in this order:

1. The `Result` success/failure flag
2. The `Result.Value` or `Result.Error` properties
3. Repository side-effects (`Received`)
4. Persisted DB state (integration/scenario tests only)

```csharp
// ✅ Correct order
result.IsSuccess.Should().BeTrue();
result.Value.Id.Should().Be(filing.Id);
await _repo.Received(1).SaveAsync(Arg.Any<Filing>(), Arg.Any<CancellationToken>());
```

---

## Snapshot Assertions (Parsers & Serializers)

```csharp
// ✅ Structural XML assertion
var doc = XDocument.Parse(xml);
doc.Root!.Name.LocalName.Should().Be("PpOpo");
doc.Descendants("Prihod").Should().HaveCount(1);
doc.Descendants("PorezNaTeret").First().Value.Should().Be("1758.15");

// ✅ Full snapshot via Verify.Xunit when complete fidelity is required
await Verify(xml);   // creates/diffs .verified.xml snapshot file

// ❌ Full string equality — breaks on whitespace/attribute ordering
xml.Should().Be(expectedXmlString);   // ❌
```

---

## Anti-Patterns Summary

| Anti-pattern | Correct pattern |
|---|---|
| `(double)result.Amount` in assertion | `.Should().Be(1758.15m)` |
| `BeApproximately` for money | `.Should().Be(exactAmount)` — always exact |
| `try/catch DomainException` in test | `.Should().Throw<DomainException>()` |
| Full exception message match | `WithMessage("*keyword*")` wildcard |
| `[0]` index on unordered collections | `.ContainSingle(x => ...)` |
| Only testing `IsSuccess` in a failure test | Also assert `result.Error.Code` |
| `new DateTime(...)` in assertions | `new DateOnly(y, m, d)` |
| `double` in `[InlineData]` for money | `m` suffix: `117.21m` |
