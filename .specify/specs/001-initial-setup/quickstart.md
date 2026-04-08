# Developer Quickstart: Rentier

**Feature**: `001-initial-setup`  
**Applies to**: All developers onboarding to the Rentier project  
**Date**: 2026-04-06

---

## Prerequisites

Before you begin, ensure the following are installed on your machine:

| Tool | Minimum Version | Install |
|------|-----------------|---------|
| .NET SDK | 8.0.x (LTS) | https://dotnet.microsoft.com/download/dotnet/8 |
| Git | 2.40+ | https://git-scm.com |
| IDE | Visual Studio 2022 17.8+ **or** JetBrains Rider 2023.3+ **or** VS Code with C# Dev Kit | See links below |

**IDE Notes**:
- **Visual Studio 2022**: Install the **ASP.NET and web development** workload (for EF Core tooling) and the **.NET desktop development** workload.
- **JetBrains Rider**: No additional plugins required; Rider has built-in Avalonia support.
- **VS Code**: Install the [C# Dev Kit extension](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csdevkit). Install the [Avalonia for VS Code extension](https://marketplace.visualstudio.com/items?itemName=AvaloniaTeam.vscode-avalonia) for XAML previews.

**Optional — EF Core CLI tools** (required if you add new migrations):

```bash
dotnet tool install --global dotnet-ef
```

---

## 1. Clone the Repository

```bash
git clone https://github.com/<your-org>/rentier.git
cd rentier
```

Confirm you are on the `develop` branch (the integration branch):

```bash
git status
# On branch develop
```

---

## 2. Restore Dependencies

From the repository root, restore all NuGet packages:

```bash
dotnet restore Rentier.sln
```

Expected output: `Restore succeeded.` with no error or warning messages.

---

## 3. Build the Solution

Build all projects with warnings treated as errors (matching the CI gate):

```bash
dotnet build Rentier.sln -c Release /p:TreatWarningsAsErrors=true
```

Expected output:

```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

If you see any warnings or errors, stop and resolve them before continuing. The CI pipeline
enforces the same zero-warning policy.

---

## 4. Run the Tests

Run all unit tests across the four test projects:

```bash
dotnet test Rentier.sln --no-build -c Release
```

Expected output:

```
Passed!  - Failed: 0, Passed: N, Skipped: 0, Total: N, Duration: ...
```

Where `N ≥ 4` (at least one smoke test per layer).

To run tests with coverage output (matches CI behaviour):

```bash
dotnet test Rentier.sln --no-build -c Release \
  --collect:"XPlat Code Coverage" \
  --results-directory ./coverage
```

Coverage XML files are written under `./coverage/`. Open `coverage.cobertura.xml` in a coverage
viewer, or use a VS Code extension such as Coverage Gutters.

---

## 5. Launch the Desktop Application

Run the Avalonia desktop application:

```bash
dotnet run --project src/Rentier.Desktop -c Debug
```

You should see:
- The Rentier main window open within ~3 seconds.
- A left-side sidebar with three navigation entries: **Filings**, **Reports**, and **Settings**.
- Clicking each entry loads the corresponding placeholder pane ("Coming soon" content).

**Troubleshooting**:
- If the window does not open, check that your display server (X11/Wayland on Linux, or desktop
  session on Windows/macOS) is active.
- On macOS, if you receive a Gatekeeper warning, run `xattr -d com.apple.quarantine` on the
  output binary or build from source as above (no signing required for dev).

---

## 6. Verify CI Locally

Reproduce the full CI pipeline locally before pushing:

```bash
# 1. Restore
dotnet restore Rentier.sln

# 2. Build (zero warnings)
dotnet build Rentier.sln -c Release /p:TreatWarningsAsErrors=true

# 3. Test with coverage
dotnet test Rentier.sln --no-build -c Release \
  --collect:"XPlat Code Coverage" \
  --results-directory ./coverage
```

All three commands must exit with code `0`. If any fails, the CI pipeline will also fail.

---

## 7. Working with EF Core Migrations

The initial empty migration (`0001_InitialCreate`) is already committed. When a future feature
adds a new entity to `AppDbContext`, create a new migration from the solution root:

```bash
dotnet ef migrations add <MigrationName> \
  --project src/Rentier.Infrastructure \
  --startup-project src/Rentier.Desktop
```

Apply migrations to your local development database:

```bash
dotnet ef database update \
  --project src/Rentier.Infrastructure \
  --startup-project src/Rentier.Desktop
```

The local database file is located at:
- **Windows**: `%APPDATA%\Rentier\rentier.db`
- **macOS**: `~/Library/Application Support/Rentier/rentier.db`

---

## 8. Git Workflow Summary

| Branch | Purpose |
|--------|---------|
| `main` | Always-releasable; direct commits prohibited |
| `develop` | Integration branch; all feature PRs target this |
| `feature/TASK-XXX-short-name` | Feature work; branched from `develop` |

**Create a feature branch**:

```bash
git checkout develop
git pull origin develop
git checkout -b feature/TASK-001-initial-setup
```

**Commit conventions** (Conventional Commits):

```
feat: add TaxpayerProfile entity stub
fix: correct FilingStatus transition guard
chore: add .editorconfig C# 12 rules
docs: update quickstart with EF migration steps
```

**Push and open a PR** targeting `develop`. CI must be green before merging.

---

## 9. Project Structure at a Glance

```
Rentier.sln
├── src/
│   ├── Rentier.Domain/          # Entities, Value Objects, DomainException
│   ├── Rentier.Application/     # Repository interfaces, CQRS interfaces, ICredentialStore
│   ├── Rentier.Infrastructure/  # AppDbContext, OsCredentialStore stub, EF migrations
│   └── Rentier.Desktop/         # Avalonia app, Views, ViewModels, DI composition root
└── tests/
    ├── Rentier.Domain.Tests/
    ├── Rentier.Application.Tests/
    ├── Rentier.Infrastructure.Tests/
    └── Rentier.Desktop.Tests/
```

---

## 10. Useful Commands Reference

| Task | Command |
|------|---------|
| Restore packages | `dotnet restore Rentier.sln` |
| Build (zero-warning mode) | `dotnet build Rentier.sln /p:TreatWarningsAsErrors=true` |
| Run all tests | `dotnet test Rentier.sln` |
| Run desktop app | `dotnet run --project src/Rentier.Desktop` |
| Add EF migration | `dotnet ef migrations add <Name> --project src/Rentier.Infrastructure --startup-project src/Rentier.Desktop` |
| Apply migrations | `dotnet ef database update --project src/Rentier.Infrastructure --startup-project src/Rentier.Desktop` |
| Check dependency graph | `dotnet list src/Rentier.Application/Rentier.Application.csproj reference` |
