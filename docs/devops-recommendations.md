# Rentier — DevOps, Code Quality & Release Strategy Recommendations

> **Date:** 2026-04-24  
> **Scope:** Code analysis, test review, static analysis tooling, CI/CD pipelines,  
> cross-platform installers, cross-platform testing, and auto-update strategy.

---

## Table of Contents

1. [Code Analysis & Improvement Recommendations](#1-code-analysis--improvement-recommendations)
2. [Test Cases & Test Types Review](#2-test-cases--test-types-review)
3. [Static Code Analysis Tooling](#3-static-code-analysis-tooling)
4. [CI/CD Pipeline Strategy](#4-cicd-pipeline-strategy)
5. [Cross-Platform Installer Generation](#5-cross-platform-installer-generation)
6. [Cross-Platform Testing in Pipeline](#6-cross-platform-testing-in-pipeline)
7. [Auto-Update Strategy](#7-auto-update-strategy)
8. [Implementation Roadmap](#8-implementation-roadmap)

---

## 1. Code Analysis & Improvement Recommendations

### 1.1 Architecture Assessment — Grade: B+ (→ A after fixes)

The Rentier codebase demonstrates **strong Clean Architecture principles** with well-defined
layers, consistent use of the Result pattern, and excellent domain modeling. Two critical
issues need immediate attention.

#### 🔴 Critical Issues

**Issue 1: Desktop → Infrastructure Layer Violation**

| File | Problem |
|------|---------|
| `src/Rentier.Desktop/Rentier.Desktop.csproj:11` | Direct `ProjectReference` to Infrastructure |
| `src/Rentier.Desktop/App.axaml.cs:13-14` | `using Rentier.Infrastructure` imports |
| `src/Rentier.Desktop/Composition/CompositionRoot.cs:14` | References Infrastructure repositories |

The Desktop layer should reference **only** Application + Domain. Infrastructure should be
wired up via an `AddInfrastructureServices()` extension method called from the composition
root, but the Desktop project should not have a compile-time dependency on Infrastructure.

**Recommendation:** Create an `IServiceCollectionExtensions` in Application that accepts
registrations via `Action<IServiceCollection>` delegates, allowing Infrastructure to register
itself without Desktop knowing about it directly. Or use a host builder pattern where
Infrastructure registers itself.

**Issue 2: .Result Anti-Pattern in MacOsCredentialStore**

| File | Line | Problem |
|------|------|---------|
| `src/Rentier.Infrastructure/Security/MacOsCredentialStore.cs` | 107 | Uses `.Result` on completed tasks |

```csharp
// Current (problematic):
return (process.ExitCode, stdoutTask.Result, stderrTask.Result);

// Fixed:
var stdout = await process.StandardOutput.ReadToEndAsync();
var stderr = await process.StandardError.ReadToEndAsync();
await process.WaitForExitAsync();
return (process.ExitCode, stdout, stderr);
```

#### 🟠 High Priority

**Handler Error Handling Duplication**

17 CQRS handlers repeat 3 variations of the same try-catch pattern. Extract to a helper:

```csharp
public static class HandlerExtensions
{
    public static async Task<Result<T, Error>> ExecuteAsync<T>(
        Func<CancellationToken, Task<Result<T, Error>>> operation,
        CancellationToken ct = default)
    {
        try { return await operation(ct); }
        catch (OperationCanceledException) { throw; }
        catch (DomainException ex) { return Result<T, Error>.Failure(Error.Domain(ex.Message)); }
        catch (Exception ex) { return Result<T, Error>.Failure(Error.Infrastructure(ex.Message)); }
    }
}
```

**Inconsistent Error Codes**

Some handlers use descriptive codes (`"GET_REPORTS_FAILED"`), others use generic
(`"VALIDATION_ERROR"`). Standardize with an error code enum or constants class.

#### 🟡 Medium Priority

| Issue | Recommendation |
|-------|---------------|
| Pagination validation duplicated in `GetFilingsQueryHandler` and `GetReportsQueryHandler` | Extract to `IPaginatedQuery` interface with shared validation |
| Wildcard NuGet versions (`11.*`, `20.*`) | Pin exact versions or use Central Package Management (`Directory.Packages.props`) |
| Money amounts as raw `decimal` properties | Consider using `Money` value object instead of `decimal GrossIncomeRsd` |

#### ✅ Strengths

| Aspect | Grade | Notes |
|--------|-------|-------|
| Domain invariants (Filing, Report, TaxpayerProfile) | A+ | Factory methods, private setters, state machine |
| Value objects (Money, ExchangeRate, HolidayConf) | A+ | Immutable records, proper validation |
| Result pattern | A+ | Type-safe, no exception-as-flow-control |
| Async patterns | A | CancellationToken threaded, no blocking (1 minor exception) |
| DateOnly / decimal usage | A+ | Consistent throughout all layers |
| ReactiveUI ViewModel patterns | A | Proper WhenActivated, scheduler injection, disposal |
| Cross-platform credential stores | A | Factory pattern with platform detection |
| Repository abstractions | A | AsNoTracking, proper upsert patterns |

---

## 2. Test Cases & Test Types Review

### 2.1 Overall Assessment — Score: 7/10

| Metric | Count | Status |
|--------|-------|--------|
| Test Projects | 5 | ✅ Complete |
| Estimated Test Methods | 500+ | ✅ Comprehensive |
| Handler Coverage | 33/33 (100%) | ✅ Excellent |
| Domain Entity Coverage | 8/8 (100%) | ✅ Excellent |
| ViewModel Coverage | 15/18 (85%) | ⚠️ 3 VMs missing |
| Infrastructure Coverage | ~95% | ✅ Good |
| Property-Based Tests | 4 | ⚠️ Underutilized |
| Snapshot Tests | 1 | ⚠️ Minimal |
| E2E Tests | 0 implemented | 🔴 Skipped |

### 2.2 Test Types Present

| Category | Project | Count | Framework | Status |
|----------|---------|-------|-----------|--------|
| Domain unit | `Rentier.UnitTests/Domain/` | ~80 | xUnit + FluentAssertions | ✅ Excellent |
| Application unit | `Rentier.UnitTests/Application/` | ~150 | xUnit + FluentAssertions + NSubstitute | ✅ Excellent |
| ViewModel unit | `Rentier.UnitTests/Desktop/` | ~150 | xUnit + FluentAssertions + NSubstitute | ✅ Good |
| Infrastructure integration | `Rentier.Infrastructure.Tests/` | ~130 | xUnit + EF Core InMemory/SQLite | ✅ Good |
| Property-based | `Rentier.UnitTests/Domain/Properties/` | 4 | FsCheck 3.x | ⚠️ Limited |
| Snapshot | `Infrastructure.Tests/Serialization/` | 1 | Verify 28.x | ⚠️ Minimal |
| Avalonia headless UI | `Rentier.UnitTests/Desktop/Views/` | ~60 | Avalonia.Headless.XUnit | ✅ Good |
| Scenario/functional | `Rentier.Scenarios.Tests/` | 9 | xUnit + EF Core SQLite | ✅ Present |
| Smoke/wiring | `DiRegistrationSmokeTests` | 1 | xUnit | ⚠️ Minimal |
| E2E (FlaUI) | `Rentier.E2E.Tests/` | 2 skipped | FlaUI (Windows-only) | 🔴 Not implemented |

### 2.3 Test Naming — 85% Compliant

Tests follow `MethodName_StateUnderTest_ExpectedBehavior` convention well:
```
✅ MarkFiled_FromInitStatus_TransitionsToFiled
✅ HandleAsync_ValidCommand_ReturnsSuccessWithGuid
✅ LoadCommand_OnSuccess_PopulatesFilings
✅ CreateFromIncome_WhtExceedsGrossTax_ClampsToZero
```

Minor violations in constructor tests: `Constructor_InitializesWithDefaults` should be
`Constructor_NoArgs_InitializesWithDefaults`.

### 2.4 Critical Coverage Gaps

#### 🔴 Property-Based Tests (FsCheck) — Only 4, Should Be 10+

**Current properties:**
- `TaxPayable_IsNeverNegative`
- `TaxPayable_NeverExceedsGrossTax`
- `GrossIncome_RoundedToTwoDecimalPlaces`
- `Deadline_IsAlwaysAtLeast30DaysAfterPaymentDate`

**Missing high-value properties:**
1. Exchange rate conversion rounding invariant
2. Filing status state machine valid transitions
3. Deadline never falls on weekend/public holiday
4. Report aggregation sum consistency
5. Pagination no-loss/no-duplication invariant
6. Payment reference format consistency

#### 🔴 Snapshot Tests (Verify) — Only 1, Should Be 5+

Only `Serialize_RepresentativeDividendFiling_MatchesSnapshot` exists.

**Missing snapshots:**
1. XML with Interest income type
2. XML with payment references
3. CSV export format
4. Filing details export
5. Report summary export

#### 🔴 E2E Tests — Not Implemented

Two test methods exist but are `[Skip]`-ped and one throws `NotImplementedException`.
The project targets `net10.0-windows`, making it unbuildable on macOS/Linux CI runners.

**Recommendation:** See Section 6 for cross-platform testing alternatives.

#### ⚠️ Missing ViewModel Tests (3 ViewModels)

- `HolidayEntryViewModel` — used in HolidaySettingsView
- `SyncProgressEntryViewModel` — progress tracking
- `ImporterItemViewModel` — used in ImporterSettings

### 2.5 Test Quality Issues

| Issue | Severity | Description |
|-------|----------|-------------|
| Over-specification | Medium | Some handler tests verify exact call counts (`Received(1)` vs `Received()`) |
| State transition gaps | Medium | `IsLoading`/`ErrorMessage` lifecycle not fully tested in all VMs |
| Infrastructure edge cases | Medium | DB constraint violations and NULL handling undertested |
| Error → recovery paths | Medium | No tests for error → subsequent success scenarios |

---

## 3. Static Code Analysis Tooling

### 3.1 Recommendation: SonarCloud (Free for Open Source)

| Criteria | SonarCloud | SonarQube (Self-Hosted) | CodeQL |
|----------|-----------|------------------------|--------|
| **Cost** | Free (open source) | Free Community Edition | Free |
| **Hosting** | Cloud (sonarcloud.io) | Self-hosted (Docker/VM) | GitHub-hosted |
| **Setup effort** | Low (GitHub App) | High (Docker, DB, maintenance) | Low (GitHub native) |
| **C# support** | ✅ Full | ✅ Full | ✅ Good |
| **PR decoration** | ✅ Inline comments | ❌ Only with paid editions | ✅ PR alerts |
| **Quality gates** | ✅ Yes | ✅ Yes | ❌ No |
| **Code coverage** | ✅ Imports from CI | ✅ Imports from CI | ❌ No |
| **Duplication detection** | ✅ Yes | ✅ Yes | ❌ No |
| **Security scanning** | ✅ SAST | ✅ SAST | ✅ SAST (security focus) |
| **Technical debt tracking** | ✅ Yes | ✅ Yes | ❌ No |
| **Maintenance** | None | Significant | None |

### 3.2 Verdict: Use SonarCloud + CodeQL Together

**Primary: SonarCloud** (run in pipeline)
- Zero infrastructure cost
- PR quality gates block merging if quality drops
- Tracks code smells, bugs, vulnerabilities, duplication, and coverage over time
- Beautiful dashboard at sonarcloud.io
- **Do NOT run locally** — the pipeline integration is the main value

**Secondary: CodeQL** (run in pipeline)
- GitHub-native security analysis
- Catches security vulnerabilities SonarCloud might miss
- Zero configuration — GitHub enables it automatically
- Run weekly + on PRs

**Local development (optional):**
- Install **SonarLint** IDE extension (VS/Rider) for real-time feedback
- It connects to your SonarCloud project to use the same rules
- This is free and requires no infrastructure

### 3.3 Why NOT Self-Hosted SonarQube

| Factor | Self-Hosted | Cloud |
|--------|-------------|-------|
| Server costs | Docker host + PostgreSQL + maintenance | $0 |
| Updates | Manual patching | Automatic |
| Availability | Depends on your infra | 99.9% SLA |
| Backup | Your responsibility | Handled |
| PR decoration | Community Edition: ❌ | ✅ |

**Bottom line:** For a desktop app team without dedicated DevOps, SonarCloud is the clear
winner. You get better features for free and no maintenance burden.

### 3.4 Setup Instructions

1. Go to [sonarcloud.io](https://sonarcloud.io) → Sign in with GitHub
2. Import your `rentier` repository
3. Create a `SONAR_TOKEN` secret in GitHub repository settings
4. Note your organization key and project key
5. Add the SonarCloud scan step to CI pipeline (see `ci.yml`)
6. Install SonarLint in your IDE and connect to SonarCloud

---

## 4. CI/CD Pipeline Strategy

### 4.1 Pipeline Overview

We recommend **two pipelines**:

| Pipeline | Trigger | Purpose |
|----------|---------|---------|
| **CI** (`ci.yml`) | Push to `develop`/`main`, PRs | Build, test, analyze, quality gate |
| **Release** (`release.yml`) | Version tag `v*.*.*` or manual | Build installers, create GitHub Release |

### 4.2 CI Pipeline — What's Included

```
┌─────────────────────────────────────────────────────┐
│                    CI Pipeline                       │
├─────────────────────────────────────────────────────┤
│                                                     │
│  ┌──────────┐  ┌──────────┐  ┌──────────────────┐  │
│  │ Windows  │  │  macOS   │  │     Ubuntu       │  │
│  │  Build   │  │  Build   │  │  Build + Sonar   │  │
│  │  Test    │  │  Test    │  │  Test            │  │
│  │  E2E     │  │          │  │  Format Check    │  │
│  │          │  │          │  │  Vuln Check      │  │
│  │          │  │          │  │  CodeQL          │  │
│  └──────────┘  └──────────┘  └──────────────────┘  │
│       │              │               │              │
│       └──────────────┼───────────────┘              │
│                      ▼                              │
│            ┌──────────────────┐                     │
│            │ Coverage Merge   │                     │
│            │ Quality Gate     │                     │
│            │ PR Comment       │                     │
│            └──────────────────┘                     │
└─────────────────────────────────────────────────────┘
```

**Standard practices included:**
1. ✅ NuGet dependency caching
2. ✅ `dotnet format --verify-no-changes` (enforce code style)
3. ✅ `dotnet list package --vulnerable` (security audit)
4. ✅ Build with `-warnaserror` (zero-warning policy)
5. ✅ Unit tests on all 3 platforms
6. ✅ E2E tests on Windows only (net10.0-windows constraint)
7. ✅ Code coverage collection and merging
8. ✅ SonarCloud analysis with quality gate
9. ✅ CodeQL security scanning
10. ✅ Concurrency control (cancel previous runs on same PR)
11. ✅ Job step summaries with test counts

### 4.3 Release Pipeline — What's Included

```
┌──────────────────────────────────────────────────────┐
│                  Release Pipeline                     │
├──────────────────────────────────────────────────────┤
│  Trigger: tag v*.*.* or manual dispatch              │
│                                                      │
│  ┌────────────┐ ┌────────────┐ ┌────────────────┐   │
│  │  Windows   │ │   macOS    │ │    Linux       │   │
│  │  Publish   │ │  Publish   │ │   Publish      │   │
│  │  win-x64   │ │  osx-x64   │ │  linux-x64     │   │
│  │            │ │  osx-arm64 │ │                │   │
│  │  InnoSetup │ │  .dmg      │ │  .tar.gz       │   │
│  │  installer │ │  bundle    │ │  .deb package  │   │
│  └────────────┘ └────────────┘ └────────────────┘   │
│       │              │               │               │
│       └──────────────┼───────────────┘               │
│                      ▼                               │
│            ┌──────────────────┐                      │
│            │ GitHub Release   │                      │
│            │ + SHA256 sums    │                      │
│            │ + Release Notes  │                      │
│            └──────────────────┘                      │
└──────────────────────────────────────────────────────┘
```

**Artifacts per release:**
| Platform | Artifact | Format |
|----------|----------|--------|
| Windows | `Rentier-x.x.x-win-x64-setup.exe` | InnoSetup installer |
| Windows | `Rentier-x.x.x-win-x64.zip` | Portable ZIP |
| macOS Intel | `Rentier-x.x.x-osx-x64.dmg` | Disk image |
| macOS ARM | `Rentier-x.x.x-osx-arm64.dmg` | Disk image |
| Linux | `Rentier-x.x.x-linux-x64.tar.gz` | Tarball |
| Linux | `rentier_x.x.x_amd64.deb` | Debian package |
| All | `SHA256SUMS.txt` | Checksum file |

### 4.4 Branching & Release Flow

```
feature/xyz ──PR──▶ develop ──PR──▶ main ──tag v1.2.0──▶ Release
     │                  │              │
     │                  │              └── Release pipeline triggers
     │                  └── CI pipeline (full)
     └── CI pipeline (full)
```

**1 feature = 1 PR flow:**
1. Developer creates `feature/my-feature` branch
2. PR opened against `develop` → CI runs (build + test + analysis)
3. SonarCloud quality gate must pass
4. PR merged to `develop`
5. When ready to release: PR from `develop` → `main`
6. Tag `main` with `v1.2.0` → Release pipeline builds installers

---

## 5. Cross-Platform Installer Generation

### 5.1 Publishing Configuration

Add to `src/Rentier.Desktop/Rentier.Desktop.csproj`:

```xml
<PropertyGroup Condition="'$(Configuration)' == 'Release'">
  <SelfContained>true</SelfContained>
  <PublishSingleFile>true</PublishSingleFile>
  <PublishReadyToRun Condition="$([MSBuild]::IsOSPlatform('Windows'))">true</PublishReadyToRun>
  <IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
  <DebugType>embedded</DebugType>
  <EnableCompressionInSingleFile>true</EnableCompressionInSingleFile>
</PropertyGroup>
```

### 5.2 Build Commands per Platform

```bash
# Windows
dotnet publish src/Rentier.Desktop -c Release -r win-x64 --self-contained -o publish/win-x64

# macOS Intel
dotnet publish src/Rentier.Desktop -c Release -r osx-x64 --self-contained -o publish/osx-x64

# macOS ARM (Apple Silicon)
dotnet publish src/Rentier.Desktop -c Release -r osx-arm64 --self-contained -o publish/osx-arm64

# Linux
dotnet publish src/Rentier.Desktop -c Release -r linux-x64 --self-contained -o publish/linux-x64
```

### 5.3 Installer Tooling

| Platform | Tool | Cost | Notes |
|----------|------|------|-------|
| Windows | InnoSetup | Free | Industry standard, scriptable, well-documented |
| macOS | `create-dmg` | Free | Creates `.dmg` with drag-to-Applications |
| Linux | `dpkg-deb` | Free | Creates `.deb` packages for Debian/Ubuntu |
| Linux | AppImage | Free | Alternative: single-file, no install needed |

### 5.4 InnoSetup Script

A template InnoSetup script is provided at `.github/installers/rentier-setup.iss`.
It handles:
- Application installation to `Program Files`
- Start Menu shortcuts
- Desktop shortcut (optional)
- Uninstaller
- Application icon
- Version from CI parameter (`/DAppVersion=1.2.0`)

---

## 6. Cross-Platform Testing in Pipeline

### 6.1 What You Can Test for Free

| Test Type | Windows | macOS | Linux | Free? |
|-----------|---------|-------|-------|-------|
| Unit tests | ✅ | ✅ | ✅ | ✅ GitHub Actions free tier |
| Infrastructure tests (EF Core) | ✅ | ✅ | ✅ | ✅ In-memory SQLite |
| Scenario tests | ✅ | ✅ | ✅ | ✅ Cross-platform |
| Avalonia headless UI tests | ✅ | ✅ | ✅ | ✅ No display needed |
| E2E (FlaUI) | ✅ | ❌ | ❌ | ✅ Windows runner only |
| Property-based tests | ✅ | ✅ | ✅ | ✅ Cross-platform |
| Snapshot tests | ✅ | ✅ | ✅ | ✅ Cross-platform |

### 6.2 GitHub Actions Free Tier Limits

| Runner | Minutes/month (Free) | Cost after free tier |
|--------|---------------------|---------------------|
| Ubuntu | 2,000 min | $0.008/min |
| Windows | 2,000 min (2x multiplier = 1,000 actual) | $0.016/min |
| macOS | 2,000 min (10x multiplier = 200 actual) | $0.08/min |

**⚠️ macOS is expensive!** Each macOS minute costs 10x the free tier. Strategy:
- Run full test suite on Windows + Ubuntu
- On macOS: run only unit tests + build verification (no integration tests)
- This maximizes free tier usage

### 6.3 Testing Without Physical Hardware

**You do NOT need macOS/Linux machines.** GitHub Actions runners provide them for free:

```yaml
strategy:
  matrix:
    include:
      - os: windows-latest    # Windows Server 2022
      - os: ubuntu-latest     # Ubuntu 24.04
      - os: macos-latest      # macOS 14 (ARM64)
```

**Avalonia headless tests work on all platforms** because they don't need a real display.
The `Avalonia.Headless` package renders controls in memory.

### 6.4 E2E Testing Strategy

The current FlaUI-based E2E tests are Windows-only (`net10.0-windows`). Options:

| Approach | Platforms | Effort | Recommendation |
|----------|-----------|--------|---------------|
| FlaUI (current) | Windows only | Low (exists) | Keep for Windows |
| Avalonia.Headless (functional) | All | Medium | **Recommended** for cross-platform |
| Playwright/Selenium | Requires Electron wrapper | High | Not applicable |

**Recommended approach:**
1. **Keep FlaUI E2E tests** for Windows-only smoke testing (launch app, verify window)
2. **Expand Avalonia headless tests** for cross-platform UI verification
3. Run FlaUI tests only on Windows runners in CI
4. **Filter E2E project** out of macOS/Linux builds:
   ```yaml
   # On non-Windows:
   dotnet test --filter "FullyQualifiedName!~E2E"
   ```

### 6.5 Platform-Specific Behavior to Test

Even without physical machines, your pipeline tests can catch:

| Concern | How to Test in CI |
|---------|-------------------|
| File path separators | Unit tests with `Path.Combine` (runs on each OS) |
| Credential store | Mock-based tests + integration on each OS |
| Line endings | Snapshot tests may differ (normalize in Verify settings) |
| SQLite behavior | EF Core tests run on each OS with real SQLite |
| Locale/encoding | Set `LANG` env var in CI |
| Binary publishing | Verify `dotnet publish` succeeds on each RID |

---

## 7. Auto-Update Strategy

### 7.1 Recommendation: Velopack

| Feature | Velopack | Squirrel.Windows | Sparkle | Custom (GitHub API) |
|---------|----------|-----------------|---------|-------------------|
| **Platforms** | Windows, macOS, Linux | Windows only | macOS only | All |
| **Cost** | Free (OSS) | Free | Free | Free |
| **Delta updates** | ✅ | ✅ | ✅ | ❌ |
| **Maintenance** | Active | Abandoned | Active (macOS) | You maintain it |
| **Setup effort** | Low | Medium | N/A | High |
| **Signing** | ✅ | ✅ | ✅ | Manual |
| **.NET support** | ✅ Native | ✅ | ❌ (Obj-C) | ✅ |
| **NuGet package** | `Velopack` | N/A | N/A | N/A |
| **Size overhead** | ~2 MB | ~5 MB | N/A | ~0 |

### 7.2 Why Velopack

1. **Cross-platform** — Works on Windows, macOS, and Linux with the same API
2. **Free & open-source** — MIT license, actively maintained
3. **Delta updates** — Downloads only changed bytes, not the full app
4. **Seamless UX** — Background check, prompt user, restart app
5. **.NET-native** — First-class C# SDK, designed for .NET apps
6. **GitHub Releases integration** — Reads release assets directly from your repo
7. **No server required** — Updates served from GitHub Releases (free)

### 7.3 Implementation Plan

#### Step 1: Install Velopack

```bash
dotnet add src/Rentier.Desktop package Velopack
```

#### Step 2: Configure in Program.cs

```csharp
using Velopack;

public static class Program
{
    public static void Main(string[] args)
    {
        // Velopack MUST be first — handles install/update/uninstall hooks
        VelopackApp.Build().Run();

        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }
}
```

#### Step 3: Create an Update Service

```csharp
// src/Rentier.Application/Interfaces/IUpdateService.cs
public interface IUpdateService
{
    Task<UpdateInfo?> CheckForUpdateAsync(CancellationToken ct = default);
    Task DownloadAndApplyAsync(UpdateInfo update, Action<int>? progress = null, CancellationToken ct = default);
}

// src/Rentier.Infrastructure/Services/VelopackUpdateService.cs
public class VelopackUpdateService : IUpdateService
{
    private readonly UpdateManager _updateManager;

    public VelopackUpdateService()
    {
        _updateManager = new UpdateManager(
            new GithubSource("https://github.com/YourOrg/rentier", null, false));
    }

    public async Task<UpdateInfo?> CheckForUpdateAsync(CancellationToken ct = default)
    {
        return await _updateManager.CheckForUpdatesAsync();
    }

    public async Task DownloadAndApplyAsync(
        UpdateInfo update, Action<int>? progress = null, CancellationToken ct = default)
    {
        await _updateManager.DownloadUpdatesAsync(update, progress);
        _updateManager.ApplyUpdatesAndRestart(update);
    }
}
```

#### Step 4: Add Update Check to ViewModel

```csharp
// In MainWindowViewModel or a dedicated UpdateViewModel:
public ReactiveCommand<Unit, Unit> CheckForUpdatesCommand { get; }

private async Task CheckForUpdatesAsync()
{
    var update = await _updateService.CheckForUpdateAsync();
    if (update != null)
    {
        // Show notification: "Update v{update.TargetFullRelease.Version} available"
        UpdateAvailable = true;
        UpdateVersion = update.TargetFullRelease.Version.ToString();
    }
}
```

#### Step 5: Update Release Pipeline

Velopack has a CLI tool (`vpk`) that creates the update packages:

```bash
# Install Velopack CLI
dotnet tool install -g vpk

# After dotnet publish, create Velopack release
vpk pack \
  --packId Rentier \
  --packVersion 1.2.0 \
  --packDir publish/win-x64 \
  --mainExe Rentier.Desktop.exe \
  --outputDir releases/win-x64

# Upload releases/ contents as GitHub Release assets
```

### 7.4 Update Flow (User Experience)

```
┌─────────────────────────────────────────────┐
│         App starts                          │
│              │                              │
│              ▼                              │
│     Check GitHub Releases                   │
│     (background, non-blocking)              │
│              │                              │
│    ┌─────────┼─────────┐                    │
│    │ No update         │ Update available   │
│    │ (silent)          ▼                    │
│    │         Show notification bar:         │
│    │         "Update v1.3.0 available"      │
│    │         [Update Now] [Later]           │
│    │                    │                   │
│    │                    ▼                   │
│    │         Download in background         │
│    │         (progress bar)                 │
│    │                    │                   │
│    │                    ▼                   │
│    │         "Restart to apply update?"     │
│    │         [Restart] [After close]        │
│    │                    │                   │
│    │                    ▼                   │
│    │         Apply update + restart app     │
│    └────────────────────────────────────────│
└─────────────────────────────────────────────┘
```

### 7.5 Alternative: Simple GitHub Release Checker (No Dependencies)

If you want minimal overhead without Velopack, implement a simple checker:

```csharp
public class GitHubUpdateChecker
{
    private const string ReleasesUrl =
        "https://api.github.com/repos/YourOrg/rentier/releases/latest";

    public async Task<(bool Available, string Version, string DownloadUrl)?> CheckAsync()
    {
        using var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Rentier/1.0");

        var json = await http.GetStringAsync(ReleasesUrl);
        var release = JsonSerializer.Deserialize<GitHubRelease>(json);

        var currentVersion = Assembly.GetExecutingAssembly().GetName().Version;
        var latestVersion = Version.Parse(release.TagName.TrimStart('v'));

        if (latestVersion > currentVersion)
            return (true, release.TagName, release.Assets[0].BrowserDownloadUrl);

        return null;
    }
}
```

This approach requires the user to manually download and install — no auto-apply.
**Velopack is strongly recommended** for a professional UX.

---

## 8. Implementation Roadmap

### Phase 1: Pipeline Foundation

| Task | Priority | Effort |
|------|----------|--------|
| Set up SonarCloud account & token | 🔴 High | 30 min |
| Deploy updated `ci.yml` pipeline | 🔴 High | 30 min |
| Enable CodeQL in GitHub settings | 🔴 High | 10 min |
| Install SonarLint in IDE | 🟡 Medium | 10 min |
| Pin NuGet package versions | 🟡 Medium | 1 hour |

### Phase 2: Release Infrastructure

| Task | Priority | Effort |
|------|----------|--------|
| Add publish configuration to `.csproj` | 🔴 High | 30 min |
| Deploy `release.yml` pipeline | 🔴 High | 30 min |
| Create InnoSetup script | 🔴 High | 1 hour |
| Test release on all 3 platforms | 🔴 High | 2 hours |
| Create first tagged release | 🔴 High | 30 min |

### Phase 3: Auto-Update

| Task | Priority | Effort |
|------|----------|--------|
| Install Velopack NuGet package | 🟠 High | 10 min |
| Configure `VelopackApp.Build().Run()` in Program.cs | 🟠 High | 30 min |
| Create `IUpdateService` interface | 🟠 High | 30 min |
| Implement `VelopackUpdateService` | 🟠 High | 2 hours |
| Add update notification UI | 🟠 High | 2 hours |
| Update release pipeline for Velopack packaging | 🟠 High | 1 hour |
| Test end-to-end update flow | 🟠 High | 2 hours |

### Phase 4: Test Gaps

| Task | Priority | Effort |
|------|----------|--------|
| Add 6+ FsCheck property-based tests | 🟡 Medium | 3 hours |
| Add 4+ Verify snapshot tests | 🟡 Medium | 2 hours |
| Test 3 missing ViewModels | 🟡 Medium | 3 hours |
| Expand smoke tests | 🟡 Medium | 1 hour |
| Implement or fix E2E tests | 🟡 Medium | 4 hours |

### Phase 5: Code Quality

| Task | Priority | Effort |
|------|----------|--------|
| Remove Desktop → Infrastructure dependency | 🔴 High | 4 hours |
| Fix MacOsCredentialStore `.Result` | 🔴 High | 15 min |
| Extract handler exception helper | 🟡 Medium | 2 hours |
| Standardize error codes | 🟡 Medium | 2 hours |
| Extract pagination validation | 🟢 Low | 1 hour |

---

## Appendix A: Tool Reference

| Tool | Purpose | Cost | URL |
|------|---------|------|-----|
| SonarCloud | Static analysis, quality gates | Free (OSS) | sonarcloud.io |
| CodeQL | Security scanning | Free | github.com/features/security |
| SonarLint | IDE real-time analysis | Free | sonarlint.org |
| Velopack | Auto-update framework | Free (MIT) | velopack.io |
| InnoSetup | Windows installer | Free | jrsoftware.org/isinfo.php |
| create-dmg | macOS disk image | Free | github.com/create-dmg/create-dmg |
| dpkg-deb | Debian packages | Free | Built into Ubuntu |
| coverlet | Code coverage | Free | github.com/coverlet-coverage |
| ReportGenerator | Coverage reports | Free | github.com/danielpalme/ReportGenerator |

## Appendix B: GitHub Secrets Required

| Secret | Purpose | Where to Get |
|--------|---------|-------------|
| `SONAR_TOKEN` | SonarCloud authentication | sonarcloud.io → My Account → Security |
| `SONAR_ORGANIZATION` | SonarCloud org key | sonarcloud.io → Organization settings |
| `SONAR_PROJECT_KEY` | SonarCloud project key | sonarcloud.io → Project settings |

No other secrets are needed — everything uses GitHub's built-in `GITHUB_TOKEN`.
