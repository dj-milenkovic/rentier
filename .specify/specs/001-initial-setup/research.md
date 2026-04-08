# Research: Rentier Initial Project Setup

**Feature**: `001-initial-setup`  
**Phase**: 0 — Outline & Research  
**Date**: 2026-04-06  
**Status**: Complete — all unknowns resolved

---

## 1. UI Framework Selection: Avalonia UI vs .NET MAUI

### Decision
**Avalonia UI 11.x with FluentTheme** is the authoritative UI framework for Rentier.

### Rationale
The project constitution (v1.0.0) explicitly mandates Avalonia UI 11+ and is the highest-priority
engineering policy in the repository. The original feature description's reference to ".NET MAUI"
was a drafting error, confirmed and documented in `clarify.md`.

Beyond the constitutional mandate, Avalonia is the technically superior choice for this specific
problem domain:

| Dimension | .NET MAUI | Avalonia UI 11+ | Advantage |
|-----------|-----------|-----------------|-----------|
| Desktop-first rendering | Secondary concern | Core focus | **Avalonia** |
| Custom rendering engine | Platform-native | Skia-based (pixel-perfect) | **Avalonia** |
| Local SQLite/EF Core integration | MAUI Essentials wrapper | Direct EF Core + SQLite | **Avalonia** |
| ViewModel testability | Platform lifecycle complicates mocking | Pure reactive; easy NSubstitute mocking | **Avalonia** |
| ReactiveUI support | Limited | First-class (Avalonia.ReactiveUI package) | **Avalonia** |
| FluentTheme fidelity | Platform-dependent | Consistent cross-platform Fluent design | **Avalonia** |
| macOS support quality | Limited Mac Catalyst | Native macOS desktop | **Avalonia** |

### Alternatives Considered
- **.NET MAUI**: Optimised for mobile (iOS/Android) with desktop support as secondary. Requires
  MAUI Essentials for common desktop patterns. Tighter platform coupling makes ViewModel
  isolation harder. Rejected by constitution.
- **WPF**: Windows-only; incompatible with macOS CI matrix requirement.
- **Uno Platform**: More complex build chain; Avalonia is simpler for a local-first desktop tool.

---

## 2. ReactiveUI vs CommunityToolkit.Mvvm — Dual-Use Strategy

### Decision
Use **both** ReactiveUI and CommunityToolkit.Mvvm in `Rentier.Desktop`, each for its respective
strengths. This is the constitution-mandated approach.

### Rationale

| Concern | ReactiveUI | CommunityToolkit.Mvvm | Chosen For |
|---------|------------|----------------------|------------|
| Reactive command creation | `ReactiveCommand.CreateFromTask` | `[RelayCommand]` | ReactiveUI |
| Property change source generators | Not available | `[ObservableProperty]` | Toolkit |
| LINQ-style reactive pipelines | `WhenAnyValue`, `ObservableAsProperty` | Not available | ReactiveUI |
| Avalonia view binding base class | `ReactiveUserControl<TViewModel>` | Not applicable | ReactiveUI |
| Boilerplate reduction (INPC) | Requires manual `RaiseAndSetIfChanged` | Source-generated | Toolkit |

**Pattern**: ViewModels inherit from `ReactiveObject` (ReactiveUI) and use
`[ObservableProperty]` (CommunityToolkit) for simple bound properties. Commands that perform
async I/O use `ReactiveCommand.CreateFromTask`. Schedulers are resolved via
`RxApp.MainThreadScheduler` to avoid UI thread blocking.

### Alternatives Considered
- **ReactiveUI-only**: Verbose for simple properties; `RaiseAndSetIfChanged` boilerplate on every
  property. Rejected in favour of Toolkit source generators for simple properties.
- **CommunityToolkit-only**: No reactive pipeline support, no `ReactiveUserControl<T>` base.
  Avalonia integration would require custom view binding plumbing. Rejected.
- **Prism**: Heavier DI framework, conflicts with Microsoft.Extensions.DependencyInjection.
  Rejected; not in constitution.

---

## 3. Single-Window Sidebar Navigation Pattern (Avalonia)

### Decision
Implement a **single `MainWindow` with a left-side `SplitView`-based sidebar** using Avalonia's
built-in layout primitives. Navigation state is held in `MainWindowViewModel`; each destination
is a separate `ReactiveUserControl<TViewModel>` loaded into a `ContentControl` in the main
content area.

### Rationale
Avalonia 11 ships a `SplitView` control and the Fluent theme provides styling consistent with
Windows 11 NavigationView aesthetics. A `ContentControl` bound to a ViewModel property is the
standard Avalonia MVVM navigation pattern. This avoids third-party navigation libraries and
keeps DI integration simple.

**Navigation flow**:
1. `MainWindowViewModel` exposes `SelectedDestination` (an enum or page ViewModel).
2. Sidebar buttons trigger `ReactiveCommand` to update `SelectedDestination`.
3. A `DataTemplate`-based `ContentControl` in `MainWindow.axaml` renders the correct
   `ReactiveUserControl<TViewModel>` based on the current selection.
4. Destination ViewModels (`FilingsViewModel`, `ReportsViewModel`, `SettingsViewModel`) are
   resolved from the DI container at navigation time.

### Alternatives Considered
- **Routing via ReactiveUI `RoutingState`**: Adds complexity for a three-destination shell.
  Deferred until navigation requirements justify the overhead.
- **Multiple windows**: Incompatible with single-window desktop UX requirements from `clarify.md`.
- **Avalonia.Controls.NavigationView (preview)**: In preview as of Avalonia 11.x; too unstable
  for a scaffold baseline.

---

## 4. EF Core 8 + SQLite Patterns for Desktop Apps

### Decision
Use **EF Core 8 with `Microsoft.EntityFrameworkCore.Sqlite`** provider. The `AppDbContext` is
registered in the DI container with a connection string pointing to a local file in the user's
app data directory (`%APPDATA%\Rentier\rentier.db` on Windows; `~/Library/Application Support/Rentier/rentier.db` on macOS).

### Rationale
EF Core 8 + SQLite is the constitution-mandated ORM/storage stack. Key desktop-specific patterns:

| Pattern | Details |
|---------|---------|
| **Connection string** | Derived at startup from `Environment.GetFolderPath(SpecialFolder.ApplicationData)` — never hard-coded |
| **Migration strategy** | `context.Database.MigrateAsync()` called at app startup; ensures schema is up to date on first run |
| **Concurrency** | Single-user desktop app; default SQLite WAL mode; no concurrent access concerns at this stage |
| **In-memory testing** | Infrastructure tests use `UseInMemoryDatabase` (or SQLite `:memory:`) for isolation |
| **DbContext lifetime** | Registered as `Scoped` within a DI scope created per operation; avoids long-lived context issues |

The initial migration (`0001_InitialCreate`) creates an empty schema. No `DbSet<>` properties or
entity type configurations are added until a future feature specifies persistence for a domain entity.

### Alternatives Considered
- **LiteDB**: Schemaless; doesn't benefit from EF Core query patterns. Rejected; not in constitution.
- **SQLite + Dapper**: No migration tooling; more manual. Rejected in favour of EF Core migrations.
- **Local JSON files**: No querying; not suitable for relational tax filing data. Rejected.

---

## 5. OS Credential Store Abstraction

### Decision
Define `ICredentialStore` in `Rentier.Application` with two methods: `SaveCredential(string key, string secret)` and `GetCredential(string key) → string?`. Provide an `OsCredentialStore` stub in `Rentier.Infrastructure` with platform-conditional compilation (`#if WINDOWS` / `#if MACOS`) delegating to:
- **Windows**: `Windows.Security.Credentials.PasswordVault` (Windows.winmd; available via `Microsoft.Windows.SDK.NET` or WinRT interop)
- **macOS**: `Security.SecKeychain` / `Security.SecRecord` from `Xamarin.Mac` bindings or `CoreFoundation` interop

Both method bodies initially throw `NotImplementedException` and are implemented in the IMAP mailbox configuration feature.

### Rationale
The constitution (Principle II) prohibits storing IMAP passwords in SQLite, plaintext files, or
environment variables. Delegating to OS-managed secure enclaves is the only constitution-compliant
approach. Abstracting behind `ICredentialStore` preserves testability (NSubstitute can mock the
interface in Application and Desktop tests) and allows the infrastructure implementation to vary
by platform without leaking platform concerns into Application or Domain.

### Alternatives Considered
- **Environment variables**: Explicitly prohibited by constitution.
- **SQLite encrypted column**: SQLite encryption is an extension; adds complexity; still fails
  "OS credential store" requirement.
- **`Microsoft.AspNetCore.DataProtection`**: Designed for server-side use; overly complex for
  single-user desktop. Rejected.
- **DPAPI (Windows-only)**: Not cross-platform; abstraction layer still required anyway.

---

## 6. GitHub Actions CI/CD Matrix for .NET 8

### Decision
Single workflow file `.github/workflows/ci.yml` with a matrix strategy across `windows-latest`
and `macos-latest` runners. Triggers: `push` to `develop` and `main`; `pull_request` targeting
`develop`.

### Workflow Steps

```text
1. actions/checkout@v4
2. actions/setup-dotnet@v4  (dotnet-version: '8.0.x')
3. dotnet restore Rentier.sln
4. dotnet build Rentier.sln --no-restore -c Release /p:TreatWarningsAsErrors=true
5. dotnet test Rentier.sln --no-build -c Release --collect:"XPlat Code Coverage" --results-directory ./coverage
6. Post coverage summary (dorny/test-reporter or direct step summary from lcov/cobertura)
```

### Key Design Choices

| Choice | Rationale |
|--------|-----------|
| `TreatWarningsAsErrors=true` | Constitution's zero-warning gate; enforced at CI rather than locally only |
| `--no-restore` on build | Avoids double restore; explicit restore step allows caching |
| `actions/cache` for NuGet | Reduces cold build time on cached runs |
| Coverlet XML (`XPlat Code Coverage`) | Standard .NET coverage collector; no Coverlet NuGet package required in test projects |
| No coverage gate | Constitution defers hard coverage enforcement until first Application use case lands |
| `macos-latest` (ARM64) | Avalonia's SkiaSharp renders on Apple Silicon; validates M-series Mac support |

### Alternatives Considered
- **Self-hosted runners**: Overkill for a developer tool at this stage.
- **Azure DevOps Pipelines**: Not in constitution; GitHub Actions is mandated.
- **ubuntu-latest matrix job**: Explicitly deferred to a future cross-platform specification task (A-009).
- **dotnet-coverage (Microsoft)**: Equivalent to XPlat Code Coverage but requires extra tooling. Rejected in favour of standard collector.

---

## Resolution Summary

| Unknown / Decision | Status | Outcome |
|--------------------|--------|---------|
| UI framework (MAUI vs Avalonia) | ✅ Resolved | Avalonia UI 11+ (constitution mandate + technical fit) |
| ReactiveUI vs Toolkit.Mvvm | ✅ Resolved | Both; complementary roles |
| Navigation pattern | ✅ Resolved | Single-window SplitView sidebar |
| EF Core startup + migration | ✅ Resolved | MigrateAsync at startup; empty initial migration |
| OS credential store | ✅ Resolved | ICredentialStore abstraction; OS-specific stubs in Infrastructure |
| CI matrix + triggers | ✅ Resolved | windows-latest + macos-latest; push + PR triggers |
| TFM | ✅ Resolved | net8.0 exclusively |
| Linux support | ✅ Resolved (deferred) | Builds on Linux but no CI job until formal requirement |

**All NEEDS CLARIFICATION items from Technical Context resolved. Proceed to Phase 1.**
