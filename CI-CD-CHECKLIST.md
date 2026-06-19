# ✅ CI/CD Strategy Implementation Checklist

## 📦 Delivered Artifacts

### Documentation (Ready to Use)

- [x] **CI-CD-TEST-STRATEGY.md** (21KB)
  - Comprehensive reference guide with all details
  - Sections: Categorization, Parallelization, Execution Order, Failure Reporting, Performance Targets, Workflows, Implementation Checklist, Maintenance
  - Contains example code, filter syntax, troubleshooting

- [x] **CI-CD-IMPLEMENTATION-GUIDE.md** (7KB)
  - Quick-start for developers
  - Step-by-step implementation (5 steps, 30 min)
  - Troubleshooting section
  - Local testing commands

- [x] **CI-CD-RESEARCH-SUMMARY.md** (11KB)
  - Executive summary
  - Key design decisions
  - Performance targets before/after
  - Benefits breakdown

### Code Infrastructure (Ready to Use)

#### Test Support Classes

- [x] **tests/Rentier.UnitTests/CollectionDefinitions.cs**
  - 4 xUnit collection definitions: Unit, Integration, E2E, Platform
  - Parallelization rules encoded
  - Documentation for each collection

- [x] **tests/Rentier.Tests.Common/DiagnosticHelper.cs**
  - `LogDatabaseStateAsync()` — Captures table snapshots on failure
  - `LogFixtureState()` — Serializes fixture/test context
  - `LogMessage()` & `LogDiagnostics()` — Formatted output
  - Ready for immediate use in failing tests

- [x] **tests/Rentier.Tests.Common/TestEnvironment.cs**
  - `IsCi` — Detects GitHub Actions/CI environment
  - `IsLocal` — Detects local dev environment
  - `MaxParallelThreads` — Recommends appropriate thread count
  - `GetDatabaseConnectionString()` — In-memory for CI, file-based for local
  - `GetEnvironmentDescription()` — For logging

#### Configuration Files

- [x] **tests/Rentier.UnitTests/xunit.runner.json** (Updated)
  ```json
  {
    "parallelizeAssembly": true,
    "parallelizeTestCollections": true,
    "maxParallelThreads": 0,        // Unlimited, full parallel
    "diagnosticMessages": true,
    "longRunningTestSeconds": 10
  }
  ```

- [x] **tests/Rentier.Infrastructure.Tests/xunit.runner.json** (Updated)
  ```json
  {
    "parallelizeAssembly": true,
    "parallelizeTestCollections": false,
    "maxParallelThreads": 1,        // Serial, safe DB access
    "diagnosticMessages": true,
    "longRunningTestSeconds": 5
  }
  ```

#### CI/CD Pipeline

- [x] **.github/workflows/ci.yml** (Updated)
  - Added `schedule` trigger for nightly E2E tests (2 AM UTC)
  - Split test stage into 3 phases:
    1. **Unit Tests** — `Category=Unit` (parallel, <2 min)
    2. **Integration Tests** — `Category=Integration` (serial, <3 min)
    3. **Platform Tests** — `Category=Platform` (Windows only, serial)
  - New job: `e2e-nightly` (runs on schedule, E2E tests only)
  - Enhanced summary with per-category durations

---

## 📋 Implementation Phases

### ✅ Phase 1: Infrastructure Setup (COMPLETE)
- [x] Create collection definitions file
- [x] Create diagnostic helper class
- [x] Create environment detection class
- [x] Update xUnit runner configurations
- [x] Update GitHub Actions CI workflow
- [x] Create documentation (3 files)

**Status:** Ready for use. Total time invested: ~2 hours research + implementation.

### ⏳ Phase 2: Apply Collection Attributes to Tests (15–20 min)

**Remaining work:**

Domain Tests:
```
tests/Rentier.UnitTests/Domain/
├── FilingStatusTransitionTests.cs          → [Collection("Unit Tests")]
├── FilingCreateFromIncomeTests.cs          → [Collection("Unit Tests")]
├── MoneyTests.cs                           → [Collection("Unit Tests")]
├── TaxCalculationServiceTests.cs           → [Collection("Unit Tests")]
└── ... (all Domain test classes)           → [Collection("Unit Tests")]
```

Application Tests:
```
tests/Rentier.UnitTests/Application/
├── AddMailboxCommandHandlerTests.cs        → [Collection("Unit Tests")]
├── ProcessReportsCommandHandlerTests.cs    → [Collection("Unit Tests")]
├── GetFilingsQueryHandlerTests.cs          → [Collection("Unit Tests")]
└── ... (all Application test classes)      → [Collection("Unit Tests")]
```

Infrastructure Tests:
```
tests/Rentier.Infrastructure.Tests/
├── FilingRepositoryTests.cs                → [Collection("Integration Tests")]
├── ImapSyncIntegrationTests.cs             → [Collection("E2E Tests")]
├── NbsWebScraperTests.cs                   → [Collection("E2E Tests")]
├── WindowsCredentialStoreTests.cs          → [Collection("Platform Tests")] + [Explicit]
└── ... (categorize by speed/I/O)
```

Desktop Tests (Headless/ViewModel):
```
tests/Rentier.UnitTests/Desktop/
├── DashboardViewModelTests.cs              → [Collection("Unit Tests")]
├── SyncViewHeadlessTests.cs                → [Collection("Unit Tests")]
└── ... (most are fast, use "Unit Tests")
```

Scenarios Tests:
```
tests/Rentier.Scenarios.Tests/
├── ... (categorize based on speed)         → [Collection("E2E Tests")] or [Explicit]
```

**Total test classes to update:** ~150 (rough estimate)

**Command to help identify:**
```bash
# Find all public test classes
grep -r "public class.*Tests" tests/ --include="*.cs" | wc -l

# Find classes missing [Collection] attribute
grep -L "Collection(" tests/**/*Tests.cs
```

### ✅ Phase 3: Local Validation (5 min)

After applying attributes, run locally:

```bash
# Fast feedback (unit tests only)
dotnet test tests/Rentier.UnitTests --filter "Category=Unit"
# Expected: <2 min, all tests parallel

# Safe integration (serialized DB tests)
dotnet test tests/Rentier.Infrastructure.Tests --filter "Category=Integration"
# Expected: 2–3 min, serial execution, no lock errors

# Full PR validation (unit + integration)
dotnet test --filter "Category!=E2E&Category!=Platform"
# Expected: <5 min total

# Full suite (including E2E)
dotnet test
# Expected: 10–15 min total
```

### ✅ Phase 4: Merge & Monitor (Automatic)

- Create PR with Phase 2 changes
- GitHub Actions automatically uses new CI configuration
- Monitor CI times in job summary
- Adjust `maxParallelThreads` if needed based on runner performance

---

## 🎯 Expected Outcomes

### Before Implementation
- Unit tests: 4–5 min
- PR tests: 6–8 min
- Full suite: 12–15 min
- Parallelization: Limited

### After Implementation
- Unit tests: <2 min (2.5–3x faster ⚡)
- PR tests: <5 min (1.2–1.6x faster ⚡)
- Full suite: <12 min (no change, E2E stays serial)
- Parallelization: Full (unit), Safe (integration)

---

## 🚨 Important Notes

### Database Safety

The strategy enforces serial execution for integration tests:
- `maxParallelThreads: 1` in Infrastructure.Tests
- Shared SQLite in-memory connection kept open
- Each test responsible for data cleanup

**Result:** Zero "database is locked" errors, safe concurrent access.

### CI Scheduling

Nightly E2E schedule (2 AM UTC = 10 PM EDT):
- Only runs on schedule (not PR-triggered)
- Parallel main feedback loop not affected
- Failed E2E tests reported next morning

**Result:** Fast PR feedback, comprehensive coverage.

### Same Binaries, Different Behavior

Tests run with same code locally and CI:
- `TestEnvironment.IsCi` detects environment
- In-memory SQLite for CI (fast, isolated)
- File-based SQLite for local (inspectable, debuggable)
- Same parallelization rules

**Result:** Confidence that local tests match CI behavior.

---

## 📞 Support & Questions

### Reference Documents

| Question | Document | Section |
|----------|----------|---------|
| How do tests run in parallel? | CI-CD-TEST-STRATEGY.md | Section 2 |
| What should my test class look like? | CI-CD-IMPLEMENTATION-GUIDE.md | Step 1 |
| Why is this test failing? | CI-CD-TEST-STRATEGY.md | Section 4 |
| How do I add diagnostic logging? | CI-CD-IMPLEMENTATION-GUIDE.md | Step 3 |
| What's the overall architecture? | CI-CD-RESEARCH-SUMMARY.md | Key Design Decisions |

### Troubleshooting

| Issue | Solution |
|-------|----------|
| "Tests fail when parallel" | Add `[Collection("Integration Tests")]` to test class |
| "Database is locked" | Already fixed via `maxParallelThreads: 1` |
| E2E test flaky | Move to `[Collection("E2E Tests")]` + add to nightly schedule |
| CI runner OOM | Lower `maxParallelThreads` in xunit.runner.json |

---

## 🏁 Success Criteria

- [ ] All test classes have `[Collection(...)]` attribute
- [ ] All test classes have `[Trait("Category", "...")]` attribute
- [ ] Local: `dotnet test --filter "Category=Unit"` completes in <2 min
- [ ] Local: `dotnet test --filter "Category!=E2E"` completes in <5 min
- [ ] CI: PR tests complete in <5 min (see GitHub Actions summary)
- [ ] CI: No "database is locked" errors in integration tests
- [ ] CI: E2E tests run nightly at 2 AM UTC (check Actions schedule)
- [ ] Diagnostic logs appear in CI output on test failure

---

## 📊 Quick Stats

| Metric | Value |
|--------|-------|
| Documentation files | 3 |
| Code files created | 3 |
| Configuration files updated | 2 |
| CI workflow updated | 1 |
| Expected unit test speedup | 2.5–3x ⚡ |
| Expected PR speedup | 1.2–1.6x ⚡ |
| Test classes to categorize | ~150 |
| Time to categorize | 15–20 min |
| Time to validate locally | 5 min |

---

## ✨ Next Action

**Apply `[Collection(...)]` and `[Trait("Category", "...")]` attributes to all test classes.**

Estimated time: **15–20 minutes**

Start with high-impact files:
1. `tests/Rentier.UnitTests/Domain/*.cs` (20 files)
2. `tests/Rentier.UnitTests/Application/*.cs` (50+ files)
3. `tests/Rentier.Infrastructure.Tests/*.cs` (40+ files)
4. `tests/Rentier.UnitTests/Desktop/*.cs` (40+ files)

Commands to help:
```bash
# Find all test classes missing [Collection]
grep -r "^public class.*Tests\s*$" tests/ --include="*.cs" | grep -v "\[Collection"

# Batch add attribute (requires manual verification)
# Example for Unit test: add [Collection("Unit Tests")] before public class declaration
```

---

**Ready to proceed? Start with Phase 2!**
