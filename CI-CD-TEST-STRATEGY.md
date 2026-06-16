# CI/CD Test Execution & Reporting Strategy — Rentier

## Executive Summary

This document provides a **concrete, implementation-ready strategy** for fast CI/CD feedback + clear diagnostics across Rentier's cross-layer test suite. The strategy balances:
- **Speed:** <5min fast feedback on PRs via parallel unit tests
- **Reliability:** Safe parallelization with zero resource contention
- **Diagnostics:** Comprehensive failure reporting for cross-layer issues
- **Coverage:** Full integration + scenario testing on nightly/release

---

## 1. Test Categorization Scheme

### Category Definition (xUnit `[Trait]`)

Organize tests by execution speed and resource requirements:

```csharp
// Fast: Pure logic, no I/O, <100ms per test
[Trait("Category", "Unit")]
public class FilingStatusTransitionTests { }

// Medium: In-memory SQLite, mocked external services, 100ms–1s
[Trait("Category", "Integration")]
public class FilingRepositoryTests : IAsyncLifetime { }

// Slow: Real I/O, network calls, fixtures, >1s per test
[Trait("Category", "E2E")]
public class ImapSyncIntegrationTests : IAsyncLifetime { }

// Not run in CI fast-path (infrastructure-specific)
[Trait("Category", "Platform")]
public class WindowsCredentialStoreTests { }
```

### Test Distribution (Current Rentier)

| Category | Count | Examples | Speed | Parallelizable |
|----------|-------|----------|-------|---|
| **Unit** | ~80% | Domain logic, value objects, status machines | <100ms | ✅ Yes |
| **Integration** | ~15% | Repository tests (in-memory DB), parsers, serializers | 100ms–1s | ⚠️ Limited |
| **E2E** | ~3% | IMAP sync, web scrapers, migrations | 1s+ | ❌ No |
| **Platform** | ~2% | OS credential store, platform-specific | Varies | ❌ No |

---

## 2. Parallelization Strategy

### xUnit Collection Definitions

**Create `tests/Rentier.UnitTests/CollectionDefinitions.cs`:**

```csharp
using Xunit;

namespace Rentier.UnitTests;

/// <summary>
/// Unit tests (pure logic, no I/O) run in full parallel.
/// </summary>
[CollectionDefinition("Unit Tests", DisableParallelization = false)]
public class UnitTestCollection { }

/// <summary>
/// Integration tests with shared in-memory SQLite DB.
/// Run sequentially to avoid connection contention.
/// </summary>
[CollectionDefinition("Integration Tests (Shared DB)", DisableParallelization = true)]
public class IntegrationSharedDbCollection : IAsyncLifetime
{
    private static SqliteConnection? _sharedConnection;

    public async Task InitializeAsync()
    {
        _sharedConnection = new SqliteConnection("Data Source=:memory:;Cache=Shared");
        await _sharedConnection.OpenAsync();
        // Provide ContextOptions to tests via a static property or fixture
    }

    public async Task DisposeAsync()
    {
        if (_sharedConnection != null)
            await _sharedConnection.DisposeAsync();
    }
}

/// <summary>
/// Slow/external tests run serially, one at a time.
/// </summary>
[CollectionDefinition("E2E Tests", DisableParallelization = true)]
public class E2ETestCollection { }

/// <summary>
/// Platform-specific tests (OS credential stores).
/// Mark as Explicit to skip in fast CI path.
/// </summary>
[CollectionDefinition("Platform Tests", DisableParallelization = true)]
public class PlatformTestCollection { }
```

**Apply to test classes:**

```csharp
[Collection("Unit Tests")]
public class FilingStatusTransitionTests { }

[Collection("Integration Tests (Shared DB)")]
public class FilingRepositoryTests : IAsyncLifetime { }

[Collection("E2E Tests")]
public class ImapSyncIntegrationTests : IAsyncLifetime { }

[Collection("Platform Tests")]
[Trait("Category", "Platform")]
public class WindowsCredentialStoreTests { }
```

### xUnit Parallelization Configuration

**Create `tests/Rentier.UnitTests/xunit.runner.json`:**

```json
{
  "appDomain": "denied",
  "diagnosticMessages": true,
  "methodDisplay": "method",
  "parallelizeAssembly": true,
  "parallelizeTestCollections": true,
  "maxParallelThreads": 0,
  "shadowCopy": false,
  "longRunningTestSeconds": 10
}
```

**Create `tests/Rentier.Infrastructure.Tests/xunit.runner.json`:**

```json
{
  "appDomain": "denied",
  "diagnosticMessages": true,
  "methodDisplay": "method",
  "parallelizeAssembly": true,
  "parallelizeTestCollections": false,
  "maxParallelThreads": 1,
  "shadowCopy": false,
  "longRunningTestSeconds": 5
}
```

---

## 3. Test Execution Order

### CI Pipeline Stages (Fail-Fast Design)

```
┌─────────────────────────────────────────────────────────────┐
│ 1. LINT & FORMAT (2min) — FAST-FAIL                        │
│    dotnet format --verify-no-changes                        │
└─────────────────────────────────────────────────────────────┘
                          ↓
┌─────────────────────────────────────────────────────────────┐
│ 2. FAST FEEDBACK (3–4min) — UNIT TESTS ONLY                │
│    dotnet test --filter "Category=Unit"                    │
│    (Parallel, 3x speedup expected)                          │
└─────────────────────────────────────────────────────────────┘
                          ↓
┌─────────────────────────────────────────────────────────────┐
│ 3. INTEGRATION (2–3min) — IN-MEMORY DB TESTS               │
│    dotnet test --filter "Category=Integration"             │
│    (Serialized, safe DB access)                            │
└─────────────────────────────────────────────────────────────┘
                          ↓
┌─────────────────────────────────────────────────────────────┐
│ 4. NIGHTLY/RELEASE ONLY (5–10min) — E2E TESTS             │
│    dotnet test --filter "Category=E2E"                     │
│    (On schedule or tag-triggered)                          │
└─────────────────────────────────────────────────────────────┘
```

### Test Execution on PR

- **✅ Unit tests:** Every commit (required for merge)
- **✅ Integration tests:** Every commit (required for merge)
- **❌ E2E tests:** Skip on PR (nightly only)
- **❌ Platform tests:** Skip on PR (require native OS)

---

## 4. Failure Reporting & Diagnostics

### Diagnostic Context Capture

**Create `tests/Rentier.Tests.Common/DiagnosticHelper.cs`:**

```csharp
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Rentier.Tests.Common;

/// <summary>
/// Captures diagnostic information on test failure.
/// </summary>
public class DiagnosticHelper
{
    public static async Task LogDatabaseStateAsync(
        AppDbContext context,
        string testName,
        IEnumerable<string> tablesToCapture)
    {
        var diagnostics = new Dictionary<string, object>();

        foreach (var table in tablesToCapture)
        {
            try
            {
                var sql = $"SELECT * FROM {table};";
                var result = await context.Database.SqlQueryRaw<dynamic>(sql).ToListAsync();
                diagnostics[table] = result;
            }
            catch (Exception ex)
            {
                diagnostics[table] = $"ERROR: {ex.Message}";
            }
        }

        var json = JsonSerializer.Serialize(diagnostics, new JsonSerializerOptions 
        { 
            WriteIndented = true 
        });

        System.Diagnostics.Debug.WriteLine(
            $"\n╔════════════════════════════════════════════════════════╗\n" +
            $"║ TEST FAILURE DIAGNOSTIC: {testName}\n" +
            $"╚════════════════════════════════════════════════════════╝\n" +
            $"{json}\n"
        );
    }

    public static void LogFixtureState(string fixtureName, object state)
    {
        var json = JsonSerializer.Serialize(state, new JsonSerializerOptions 
        { 
            WriteIndented = true 
        });

        System.Diagnostics.Debug.WriteLine(
            $"\n[FIXTURE STATE] {fixtureName}:\n{json}\n"
        );
    }
}
```

**Apply in Infrastructure tests:**

```csharp
[Collection("Integration Tests (Shared DB)")]
public class FilingRepositoryTests : IAsyncLifetime
{
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
            throw;
        }
    }

    // Cleanup also logs state for inspection
    async Task IAsyncLifetime.DisposeAsync()
    {
        await DiagnosticHelper.LogDatabaseStateAsync(
            _context,
            "Teardown State",
            new[] { "Filings", "TaxpayerProfiles" }
        );
        await _context.DisposeAsync();
    }
}
```

### Failure Report Format

**Example output to CI logs:**

```
╔════════════════════════════════════════════════════════╗
║ TEST FAILURE DIAGNOSTIC: SomeTest_Fails                ║
╚════════════════════════════════════════════════════════╝

{
  "Filings": [
    {
      "Id": "guid-1",
      "Status": "Init",
      "PayingEntity": "ACME Corp"
    }
  ],
  "TaxpayerProfiles": [],
  "ExchangeRates": "ERROR: Table not found"
}
```

---

## 5. Performance Targets by Category

| Category | Target Duration | Tests | Total | Parallelization |
|----------|---|---|---|---|
| **Unit** | <2 min | 80 | ~1.2s | 3–4x speedup |
| **Integration** | <3 min | 15 | ~2min | 1x (serial) |
| **E2E** | <10 min | 3 | ~6–8min | 1x (serial) |
| **PR Fast Path** | <5 min | 95 | ~3–4min | ✅ All unit + integration |

---

## 6. GitHub Actions Workflow (Updated)

**Replace `.github/workflows/ci.yml` test job with:**

```yaml
# ──────────────────────────────────────────────────────────────
# Build & Test matrix (Windows, macOS, Ubuntu)
# ──────────────────────────────────────────────────────────────
build:
  name: Build & Test (${{ matrix.os }})
  needs: lint
  strategy:
    fail-fast: false
    matrix:
      os: [windows-latest, macos-latest, ubuntu-latest]
      include:
        - os: windows-latest
          is-windows: true
        - os: macos-latest
          is-windows: false
        - os: ubuntu-latest
          is-windows: false
  runs-on: ${{ matrix.os }}

  steps:
    - name: Checkout
      uses: actions/checkout@v6.0.3
      with:
        fetch-depth: 0

    - name: Setup .NET
      uses: actions/setup-dotnet@v5.3.0
      with:
        dotnet-version: '10.x'

    - name: Cache NuGet packages
      uses: actions/cache@v5.0.5
      with:
        path: ~/.nuget/packages
        key: nuget-${{ runner.os }}-${{ hashFiles('**/*.csproj', 'Directory.Build.props', 'Directory.Packages.props') }}
        restore-keys: nuget-${{ runner.os }}-

    # ── Build ────────────────────────────────
    - name: Restore (all projects)
      run: dotnet restore Rentier.slnx

    - name: Build (all projects)
      run: dotnet build Rentier.slnx --no-restore -c Release

    # ── Test: UNIT (Fast path, parallel) ─────────────────────
    - name: Test — Unit (Fast Path, Parallel)
      shell: bash
      run: |
        dotnet test tests/Rentier.UnitTests \
          --no-build -c Release \
          --filter "Category=Unit" \
          --collect:"XPlat Code Coverage" \
          --settings coverlet.runsettings \
          --results-directory ./coverage/unit \
          --logger "trx;LogFileName=unit-results-${{ matrix.os }}.trx" \
          --logger "console;verbosity=normal"

    # ── Test: INTEGRATION (Serialized) ────────────────────────
    - name: Test — Integration (Database, Serialized)
      shell: bash
      run: |
        dotnet test tests/Rentier.UnitTests tests/Rentier.Infrastructure.Tests \
          --no-build -c Release \
          --filter "Category=Integration" \
          --collect:"XPlat Code Coverage" \
          --settings coverlet.runsettings \
          --results-directory ./coverage/integration \
          --logger "trx;LogFileName=integration-results-${{ matrix.os }}.trx" \
          --logger "console;verbosity=normal"

    # ── Test: PLATFORM-SPECIFIC (Serialized, Windows/macOS/Linux) ──────────
    - name: Test — Platform-Specific
      if: ${{ matrix.is-windows || false }}  # Only run on Windows for now
      shell: bash
      run: |
        dotnet test tests/Rentier.Infrastructure.Tests \
          --no-build -c Release \
          --filter "Category=Platform" \
          --collect:"XPlat Code Coverage" \
          --settings coverlet.runsettings \
          --results-directory ./coverage/platform \
          --logger "trx;LogFileName=platform-results-${{ matrix.os }}.trx" \
          --logger "console;verbosity=normal" || true

    # ── Collect Coverage ──────────────────────────────────
    - name: Merge coverage artifacts
      if: always()
      shell: bash
      run: |
        mkdir -p ./coverage/merged
        find ./coverage -name 'coverage.opencover.xml' -exec cp {} ./coverage/merged \;

    - name: Upload coverage
      if: always()
      uses: actions/upload-artifact@v7.0.1
      with:
        name: coverage-${{ matrix.os }}
        path: ./coverage/merged/coverage.opencover.xml

    # ── Artifacts ────────────────────────────
    - name: Upload test results
      if: always()
      uses: actions/upload-artifact@v7.0.1
      with:
        name: test-results-${{ matrix.os }}
        path: ./coverage/**/*.trx

    # ── Step summary ─────────────────────────
    - name: Post summary
      if: always()
      shell: bash
      run: |
        echo "## 🧪 Test Results — ${{ matrix.os }}" >> "$GITHUB_STEP_SUMMARY"
        echo "" >> "$GITHUB_STEP_SUMMARY"

        # Parse TRX files for statistics
        total=0; passed=0; failed=0
        for trx in $(find ./coverage -name '*.trx' 2>/dev/null); do
          t=$(grep -Eo 'total="[0-9]+' "$trx" | cut -d'"' -f2 | head -1)
          p=$(grep -Eo 'passed="[0-9]+' "$trx" | cut -d'"' -f2 | head -1)
          f=$(grep -Eo 'failed="[0-9]+' "$trx" | cut -d'"' -f2 | head -1)
          total=$((total + ${t:-0}))
          passed=$((passed + ${p:-0}))
          failed=$((failed + ${f:-0}))
        done

        echo "| Metric | Count |" >> "$GITHUB_STEP_SUMMARY"
        echo "|--------|-------|" >> "$GITHUB_STEP_SUMMARY"
        echo "| Total | $total |" >> "$GITHUB_STEP_SUMMARY"
        echo "| ✅ Passed | $passed |" >> "$GITHUB_STEP_SUMMARY"
        if [ "$failed" -gt 0 ]; then
          echo "| ❌ Failed | $failed |" >> "$GITHUB_STEP_SUMMARY"
        fi

# ──────────────────────────────────────────────────────────────
# Nightly E2E Tests (Scheduled)
# ──────────────────────────────────────────────────────────────
e2e-nightly:
  name: E2E Tests (Nightly)
  runs-on: ubuntu-latest
  if: github.event_name == 'schedule'  # Only on nightly schedule

  steps:
    - name: Checkout
      uses: actions/checkout@v6.0.3

    - name: Setup .NET
      uses: actions/setup-dotnet@v5.3.0
      with:
        dotnet-version: '10.x'

    - name: Cache NuGet packages
      uses: actions/cache@v5.0.5
      with:
        path: ~/.nuget/packages
        key: nuget-${{ runner.os }}-${{ hashFiles('**/*.csproj', 'Directory.Build.props', 'Directory.Packages.props') }}
        restore-keys: nuget-${{ runner.os }}-

    - name: Restore & Build
      run: |
        dotnet restore Rentier.slnx
        dotnet build Rentier.slnx --no-restore -c Release

    - name: Run E2E Tests
      shell: bash
      run: |
        dotnet test tests/ \
          --no-build -c Release \
          --filter "Category=E2E" \
          --logger "trx;LogFileName=e2e-results.trx" \
          --logger "console;verbosity=detailed"

    - name: Upload E2E results
      if: always()
      uses: actions/upload-artifact@v7.0.1
      with:
        name: e2e-test-results
        path: ./coverage/**/*.trx

    - name: Post summary
      if: always()
      shell: bash
      run: |
        echo "## 🌙 Nightly E2E Test Results" >> "$GITHUB_STEP_SUMMARY"
        # Parse and append summary
```

---

## 7. Local vs. CI Configuration

### Same Binaries, Different Execution

Use **environment-aware test configuration:**

```csharp
namespace Rentier.Tests.Common;

public class TestEnvironment
{
    public static bool IsCi => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI"));
    public static bool IsLocal => !IsCi;

    public static int MaxParallelThreads => IsCi ? 4 : Environment.ProcessorCount;

    public static string GetDatabasePath()
    {
        return IsCi 
            ? "Data Source=:memory:;Cache=Shared"  // In-memory in CI
            : $"Data Source={Path.GetTempFileName()}.db";  // SQLite file locally
    }
}
```

### GitHub Actions Environment Setup

```yaml
env:
  CI: 'true'
  DOTNET_NOLOGO: 'true'
  DOTNET_CLI_TELEMETRY_OPTOUT: 'true'
```

---

## 8. Implementation Checklist

### Phase 1: Infrastructure (Week 1)

- [ ] Create `CollectionDefinitions.cs` with 4 collection types
- [ ] Add `xunit.runner.json` to test projects
- [ ] Create `DiagnosticHelper.cs` for failure logging
- [ ] Update test attributes (add `[Collection(...)]` to all test classes)
- [ ] Categorize existing tests (add `[Trait("Category", "...")]`)

### Phase 2: CI Configuration (Week 2)

- [ ] Create scheduled E2E workflow
- [ ] Update main CI workflow with staged execution
- [ ] Test locally: `dotnet test --filter "Category=Unit"`
- [ ] Verify parallelization speedup on unit tests
- [ ] Validate integration tests run serially without conflicts

### Phase 3: Monitoring & Tuning (Week 3+)

- [ ] Monitor CI times per stage
- [ ] Track flaky tests (mark with `[Explicit]` if needed)
- [ ] Adjust `maxParallelThreads` based on runner capacity
- [ ] Collect failure diagnostic logs from failed runs
- [ ] Iterate on performance targets

---

## 9. Expected Outcomes

### CI Feedback Time

| Scenario | Current | Target | Speedup |
|----------|---------|--------|---------|
| Unit tests only | 4–5min | <2min | 2–2.5x |
| Full PR (unit + integration) | 6–8min | <5min | 1.2–1.6x |
| Full suite (add E2E) | 12–15min | <12min | 1–1.25x |

### Local Dev Loop

```bash
# Fast feedback (30s–1min locally)
dotnet test --filter "Category=Unit"

# Full validation (3–5min locally)
dotnet test --filter "Category!=E2E&Category!=Platform"

# Nightly (10–15min, not in main flow)
dotnet test
```

---

## 10. Maintenance & Escalation

### Monitor These Metrics

- **Test duration trends:** Nightly alerts if unit tests exceed 3min or integration >5min
- **Flakiness rate:** Flag any test with >5% failure rate on same commit
- **Parallelization speedup:** Expected 3–4x for unit tests, flag if <2x
- **Database contention:** Log any SQLite "database is locked" errors

### Escalation Decisions

| Issue | Action |
|-------|--------|
| Unit test slow (>100ms) | Profile and optimize |
| Integration test deadlock | Add to `[Explicit]`, investigate |
| E2E test flaky | Move to scheduled nightly, quarantine |
| CI runner OOM | Reduce `maxParallelThreads` |

---

## Appendix A: Example Test Attributes

```csharp
// ✅ Fast, pure logic
[Collection("Unit Tests")]
[Trait("Category", "Unit")]
public class FilingStatusTransitionTests { }

// ⚠️ Integration, in-memory DB
[Collection("Integration Tests (Shared DB)")]
[Trait("Category", "Integration")]
public class FilingRepositoryTests : IAsyncLifetime { }

// ❌ Slow, external I/O
[Collection("E2E Tests")]
[Trait("Category", "E2E")]
public class ImapSyncIntegrationTests : IAsyncLifetime { }

// 🚫 Platform-specific (skip in CI)
[Collection("Platform Tests")]
[Trait("Category", "Platform")]
[Explicit]  // Only when explicitly requested
public class WindowsCredentialStoreTests { }
```

---

## Appendix B: Test Filter Examples

```bash
# Run only unit tests (fast)
dotnet test --filter "Category=Unit"

# Run everything except E2E
dotnet test --filter "Category!=E2E&Category!=Platform"

# Run all integration tests (in-memory DB)
dotnet test --filter "Category=Integration"

# Run a specific test
dotnet test --filter "FullyQualifiedName~FilingRepositoryTests.AddAsync_ValidFiling_PersistedInDb"

# Run all except slow/platform
dotnet test --filter "Category!=E2E&Category!=Platform"

# For CI: strict failures, brief output
dotnet test --filter "Category!=E2E&Category!=Platform" \
  --logger "console;verbosity=minimal" \
  --no-build -c Release
```

---

## References

- **xUnit Parallel Execution:** https://xunit.net/docs/running-tests-in-parallel
- **EF Core Testing with SQLite:** https://learn.microsoft.com/en-us/ef/core/testing/testing-sqlite
- **GitHub Actions Concurrency:** https://docs.github.com/en/actions/using-jobs/using-concurrency
- **SonarCloud + xUnit Integration:** https://sonarcloud.io
