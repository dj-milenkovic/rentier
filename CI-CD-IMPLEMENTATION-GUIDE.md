# Implementation Guide: CI/CD Test Execution Strategy

## Quick Start (5 minutes)

This guide walks through implementing the CI/CD test execution strategy for Rentier.

### What Was Done

✅ **Core Infrastructure**
- `CollectionDefinitions.cs` — xUnit collection groupings for parallel/serial execution
- `DiagnosticHelper.cs` — Diagnostic logging on test failures
- `TestEnvironment.cs` — Environment detection (CI vs local)
- `xunit.runner.json` (2 files) — xUnit parallelization configuration
- Updated `.github/workflows/ci.yml` — Staged test execution + nightly E2E schedule

✅ **Documentation**
- `CI-CD-TEST-STRATEGY.md` — Comprehensive reference guide

### Step 1: Apply Collection Attributes to Tests (15–20 min)

All test classes need the `[Collection(...)]` attribute. Start with these high-impact categories:

**Unit Tests (pure logic, no I/O):**
```csharp
[Collection("Unit Tests")]
[Trait("Category", "Unit")]
public class FilingStatusTransitionTests { }
```

**Integration Tests (in-memory SQLite):**
```csharp
[Collection("Integration Tests")]
[Trait("Category", "Integration")]
public class FilingRepositoryTests : IAsyncLifetime { }
```

**E2E Tests (external APIs, real I/O):**
```csharp
[Collection("E2E Tests")]
[Trait("Category", "E2E")]
public class ImapSyncIntegrationTests : IAsyncLifetime { }
```

**Platform-Specific Tests (OS credential stores):**
```csharp
[Collection("Platform Tests")]
[Trait("Category", "Platform")]
[Explicit]  // Skip in CI, run explicitly only
public class WindowsCredentialStoreTests { }
```

**Guidance:**
- Look at `tests/Rentier.UnitTests/Domain/FilingStatusTransitionTests.cs` — already a good unit test example
- Look at `tests/Rentier.Infrastructure.Tests/FilingRepositoryTests.cs` — already an integration test example
- Use `grep -r "public class.*Tests" tests/` to find all test classes needing updates

### Step 2: Update Repository Tests (5 min)

Infrastructure tests using EF Core in-memory SQLite already have the right patterns:
```csharp
public class FilingRepositoryTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private AppDbContext _context = null!;
    
    async Task IAsyncLifetime.InitializeAsync() { /* setup */ }
    async Task IAsyncLifetime.DisposeAsync() { /* cleanup */ }
}
```

These are already fine. Just add the `[Collection("Integration Tests")]` attribute.

### Step 3: Add Diagnostic Logging to Failing Tests (Optional, 5 min)

When a test fails, log database state for diagnostics:

```csharp
[Fact]
public async Task SomeTest_Fails()
{
    try
    {
        // Test logic
    }
    catch (Exception ex)
    {
        await DiagnosticHelper.LogDatabaseStateAsync(
            _context,
            nameof(SomeTest_Fails),
            new[] { "Filings", "TaxpayerProfiles", "ExchangeRates" }
        );
        throw;  // Re-throw to fail the test
    }
}
```

### Step 4: Test Locally (3 min)

Run the categorized tests locally to verify the configuration:

```bash
# Fast feedback: unit tests only (should complete in <2 min)
dotnet test tests/Rentier.UnitTests --filter "Category=Unit"

# Integration tests (should complete in <3 min, serialized)
dotnet test tests/Rentier.Infrastructure.Tests --filter "Category=Integration"

# All tests except E2E/Platform (normal PR flow)
dotnet test --filter "Category!=E2E&Category!=Platform"

# All tests (full suite, ~10–15 min)
dotnet test
```

### Step 5: Verify CI Pipeline (Automatic)

The GitHub Actions workflow has been updated to:
1. Run unit tests in parallel (fast feedback)
2. Run integration tests serially (safe database access)
3. Skip E2E tests on PR (run nightly only)
4. Post summary with test counts and durations

No additional CI setup needed—it's already in `.github/workflows/ci.yml`.

---

## Expected Results

### After Implementation

| Metric | Current | Target |
|--------|---------|--------|
| Unit tests | 4–5 min | <2 min |
| PR full test | 6–8 min | <5 min |
| Local dev loop | 10–15 min | 3–5 min |

### Parallelization Speedup

- **Unit tests:** 3–4x speedup (full parallel)
- **Integration tests:** 1x (serialized for safety)
- **Overall PR:** 1.2–1.6x speedup

---

## Troubleshooting

### "Tests fail only when parallelized"

**Cause:** Shared static state or fixture pollution.

**Fix:**
1. Make test data immutable or thread-safe
2. Isolate each test (no shared DB context)
3. Mark the test class with `[Collection("Integration Tests")]` to run serially

### "Database is locked" errors

**Cause:** Multiple tests writing to the same SQLite in-memory DB simultaneously.

**Fix:**
1. The Infrastructure.Tests `xunit.runner.json` already sets `maxParallelThreads: 1`
2. Verify the file was updated (check with `cat tests/Rentier.Infrastructure.Tests/xunit.runner.json`)
3. If still failing, move test to `[Collection("E2E Tests")]` and `[Trait("Category", "E2E")]`

### "Test marked [Explicit] not running"

**Cause:** Explicit tests are skipped by default.

**Fix:**
1. Run with `dotnet test --filter "Category=Platform"` to include them
2. Or `dotnet test --include-explicit` (xUnit v3)
3. This is intentional for platform-specific tests (use only on Windows, for example)

---

## File Checklist

✅ **Created Files**
- `tests/Rentier.UnitTests/CollectionDefinitions.cs`
- `tests/Rentier.Tests.Common/DiagnosticHelper.cs`
- `tests/Rentier.Tests.Common/TestEnvironment.cs`
- `CI-CD-TEST-STRATEGY.md` (comprehensive reference)
- This file (`CI-CD-IMPLEMENTATION-GUIDE.md`)

✅ **Updated Files**
- `tests/Rentier.UnitTests/xunit.runner.json` (added parallelization settings)
- `tests/Rentier.Infrastructure.Tests/xunit.runner.json` (set to serial for safety)
- `.github/workflows/ci.yml` (staged test execution + nightly E2E schedule)

❌ **To Do: Apply Attributes to Tests**
- Add `[Collection("Unit Tests")]` to all Domain/Application tests
- Add `[Collection("Integration Tests")]` to all Infrastructure/Repository tests
- Add `[Collection("E2E Tests")]` to slow integration tests (IMAP, web scrapers)
- Add `[Collection("Platform Tests")]` + `[Explicit]` to OS-specific tests

---

## Next Steps

1. **Apply attributes** to test classes (15–20 min)
2. **Run locally** to verify parallelization (5 min)
3. **Merge to main** — CI will automatically use new configuration
4. **Monitor** CI times and adjust `maxParallelThreads` if needed

---

## Questions & Escalation

See "Maintenance & Escalation" section in `CI-CD-TEST-STRATEGY.md` for:
- Metric monitoring
- Flaky test handling
- Performance troubleshooting
- Resource contention resolution
