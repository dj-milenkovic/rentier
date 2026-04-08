# Clarifications: 001 — Initial Project Setup

**Feature**: Rentier Initial Project Setup  
**Created**: 2026-04-06  
**Status**: Clarified (non-interactive — all answers encoded as reasoned assumptions)  
**Feeds into**: `/speckit.specify` → `spec.md`, then `/speckit.plan`

---

## Ambiguity & Coverage Scan

The following taxonomy was applied to the feature description before generating
clarification questions. Each category is rated: **Clear**, **Partial**, or **Missing**.

| # | Category | Status | Notes |
|---|----------|--------|-------|
| 1 | Functional Scope & Behavior | Partial | "Basic shell navigation" undefined; scaffold completeness boundary unclear |
| 2 | Domain & Data Model | Partial | Constitution defines all entities; unclear which receive stub implementations vs. empty stubs |
| 3 | Interaction & UX Flow | Partial | Main window navigation pattern unspecified |
| 4 | Non-Functional Quality Attributes | Partial | Target .NET TFM not stated explicitly in feature description |
| 5 | Integration & External Dependencies | Clear | No live external integrations in scaffolding phase |
| 6 | Edge Cases & Failure Handling | Clear (N/A) | No business logic; not applicable to scaffold |
| 7 | Constraints & Tradeoffs | Missing | **Critical conflict**: description says ".NET MAUI", constitution mandates Avalonia UI 11+ |
| 8 | Terminology & Consistency | Partial | "Basic shell navigation" is an ambiguous adjective |
| 9 | Completion Signals | Partial | CI trigger events and coverage enforcement timing not defined |
| 10 | Misc / Placeholders | Partial | Windows-only vs. cross-platform target not stated in description |

**Prioritized question queue** (by Impact × Uncertainty, max 5):

1. UI framework conflict (Critical impact, High uncertainty)
2. Scaffold completeness level (High impact, High uncertainty)
3. Target .NET version (High impact, Medium uncertainty)
4. Main shell navigation pattern (Medium impact, High uncertainty)
5. CI workflow scope and triggers (Medium impact, Medium uncertainty)

---

## Clarifications

### Session 2026-04-06

- **Q1**: The feature description references ".NET MAUI" but the project constitution
  mandates **Avalonia UI 11+** as the authoritative UI framework. Which framework governs
  this project?
  → **A: Avalonia UI 11+ with FluentTheme** — the constitution is the highest-priority
  engineering policy in the repository. The ".NET MAUI" mention in the original description
  was an error. All subsequent implementation MUST use Avalonia UI 11+.

- **Q2**: What level of implementation completeness is expected for the initial scaffold —
  specifically: (A) empty project stubs only, (B) empty projects + DI wiring + DbContext
  stub, (C) full layer interface stubs + DI composition root + empty EF Core migration,
  or (D) all of the above plus a sample passing test per layer?
  → **A: Option C — full layer interface stubs + DI composition root + empty initial EF Core
  migration** (no business tables yet). Each project (`Domain`, `Application`,
  `Infrastructure`, `Desktop`) must exist as a buildable `.csproj` with the correct
  package references, the right inward-only project references, the interface skeletons
  called out in the constitution (repository interfaces, use-case handler interfaces), and
  an `AppDbContext` stub. A passing sample xUnit test confirming project wiring is also
  required (see Q5 outcome).

- **Q3**: Which .NET target framework moniker (TFM) should the solution target?
  → **A: `net8.0`** — .NET 8 is the current LTS release. The constitution's CI matrix
  explicitly references ".NET 8". `net9.0` is out of scope until an explicit upgrade
  decision is made.

- **Q4**: What navigation pattern should the main Avalonia shell window use for the
  initial setup?
  → **A: Single-window sidebar navigation** — a left-side `NavigationView`-style panel
  with named top-level destinations (e.g., Filings, Reports, Settings). This is the
  canonical Avalonia FluentTheme desktop pattern and matches the expected filing lifecycle
  workflow (import → calculate → file → pay). Each destination is a separate
  `ReactiveUserControl<TViewModel>`. The shell itself does not implement any business
  logic; all panes render "Coming soon" placeholder content until later features land.

- **Q5**: What should the initial GitHub Actions CI pipeline cover and on which platforms?
  → **A: Build + unit tests on Windows and macOS matrix** — matching the constitution CI
  requirement (`Windows + macOS`). Triggers: `push` to `develop` or `main`, and `pull_request`
  targeting `develop`. Coverage reporting is wired (Coverlet + GitHub summary) but no
  hard coverage gate is enforced until at least one Application layer test exists (first
  real feature). The pipeline MUST pass with zero warnings (`-warnaserror`).

---

## Resolved Assumptions Applied to Specification

The following assumptions were derived from the five clarifications above and are
encoded here for direct use by `/speckit.specify`:

### FR (Functional Requirements) — Initial Setup Scope

- **FR-001**: The solution MUST be named `Rentier` and structured as a single `.sln`
  containing exactly four C# projects: `Rentier.Domain`, `Rentier.Application`,
  `Rentier.Infrastructure`, and `Rentier.Desktop`.
- **FR-002**: A fifth project, `Rentier.Tests` (or a `tests/` folder containing
  `Rentier.Domain.Tests`, `Rentier.Application.Tests`), MUST be included in the
  solution and must contain at least one passing smoke-test per layer verifying
  project compilation and DI registration.
- **FR-003**: Project references MUST enforce Clean Architecture inward-only dependency
  rules:
  - `Rentier.Domain` → no project references
  - `Rentier.Application` → `Rentier.Domain` only
  - `Rentier.Infrastructure` → `Rentier.Domain` + `Rentier.Application`
  - `Rentier.Desktop` → `Rentier.Application` + `Rentier.Domain`
- **FR-004**: All four main projects MUST target `net8.0`.
- **FR-005**: `Rentier.Domain` MUST contain stub `record` types for all nine domain
  entities/value objects listed in the constitution
  (`TaxpayerProfile`, `Mailbox`, `MailboxCursor`, `Importer`, `Report`, `Filing`,
  `Money`, `ExchangeRate`, `HolidayConf`), with empty or minimal constructors.
  No persistence logic is included.
- **FR-006**: `Rentier.Application` MUST contain the six repository interfaces
  (`IFilingRepository`, `IReportRepository`, `IMailboxRepository`,
  `IImporterRepository`, `ITaxpayerProfileRepository`,
  `IExchangeRateCacheRepository`) as empty `interface` declarations, plus skeleton
  `ICommandHandler<TCommand, TResult>` and `IQueryHandler<TQuery, TResult>`
  generic interfaces.
- **FR-007**: `Rentier.Infrastructure` MUST contain an `AppDbContext` stub inheriting
  `DbContext` (EF Core 8 + SQLite provider) with no `DbSet<>` properties yet, plus
  an empty initial migration (`0001_InitialCreate`) generated via
  `dotnet ef migrations add`.
- **FR-008**: `Rentier.Desktop` MUST contain:
  - `App.axaml` / `App.axaml.cs` with Avalonia app entry point using `FluentTheme`.
  - `MainWindow.axaml` with a sidebar navigation shell layout.
  - Placeholder `ReactiveUserControl<TViewModel>` views for at least three top-level
    navigation destinations: **Filings**, **Reports**, **Settings**.
  - A DI composition root wiring all Application and Infrastructure registrations via
    `Microsoft.Extensions.DependencyInjection`.
- **FR-009**: `Rentier.Desktop` MUST use `ReactiveUI` for MVVM and
  `CommunityToolkit.Mvvm` source generators for `[ObservableProperty]` attributes
  on ViewModels.
- **FR-010**: All user-visible strings (navigation labels, window title) MUST be
  declared in `Resources/Strings.resx` inside `Rentier.Desktop`.
- **FR-011**: IMAP passwords MUST NOT appear anywhere in the scaffold; the
  `ICredentialStore` interface stub MUST be placed in `Rentier.Application` and
  marked for OS credential store implementation in `Rentier.Infrastructure`.

### CI/CD

- **CI-001**: A GitHub Actions workflow file (`.github/workflows/ci.yml`) MUST be
  created that:
  - Triggers on `push` to `develop` and `main`, and `pull_request` targeting `develop`.
  - Runs on `windows-latest` and `macos-latest` in a matrix strategy.
  - Steps: checkout → setup .NET 8 → restore → build (with `-warnaserror`) → test
    (with Coverlet XML output) → upload coverage summary as a job step summary.
  - All steps MUST pass with zero warnings and zero test failures.
- **CI-002**: A `.editorconfig` MUST be committed at repository root enforcing
  Conventional Commits-friendly settings and C# 12 style rules consistent with the
  constitution coding standards.

### Out of Scope for This Feature

- No business logic implementation (CSV import, NBS scraping, XML generation, tax
  calculation).
- No EF Core `DbSet<>` properties or data seeding.
- No IMAP credential storage implementation (interface stub only).
- No Avalonia UI automation tests.
- No installer/packaging setup.
- No `net9.0` multi-targeting.

### Technology Stack (Authoritative)

| Concern | Choice | Source |
|---------|--------|--------|
| UI | **Avalonia UI 11+** with `FluentTheme` | Constitution (overrides ".NET MAUI" in description) |
| MVVM | ReactiveUI + CommunityToolkit.Mvvm | Constitution |
| ORM | EF Core 8 + SQLite | Constitution |
| Testing | xUnit + FluentAssertions + NSubstitute | Constitution |
| DI | Microsoft.Extensions.DependencyInjection | Constitution |
| .NET TFM | `net8.0` | Constitution CI matrix |
| CI | GitHub Actions (Windows + macOS) | Constitution |

### Resolved Tradeoff: .NET MAUI vs Avalonia UI

| Dimension | .NET MAUI | Avalonia UI 11+ | Decision |
|-----------|-----------|-----------------|----------|
| Authority | Original description | Constitution v1.0.0 | **Avalonia** |
| Cross-platform | iOS/Android/macOS/Windows | macOS/Linux/Windows/Browser | Avalonia (desktop focus) |
| SQLite local-first | Requires MAUI Essentials | Native EF Core + SQLite | Avalonia |
| Testability | Limited ViewModel isolation | ReactiveUI + easy mocking | Avalonia |
| Community | Microsoft-backed | Active open source | Tie |

**Rationale**: The constitution is explicitly the highest-priority engineering policy.
It mandates Avalonia UI 11+ with ReactiveUI. The description's reference to .NET MAUI
was a drafting error. No further evaluation of .NET MAUI is required.

---

## Coverage Summary

| Category | Status |
|----------|--------|
| Functional Scope & Behavior | ✅ Resolved (Q2 → scaffold completeness defined) |
| Domain & Data Model | ✅ Resolved (Q2 → stub entities listed; no persistence) |
| Interaction & UX Flow | ✅ Resolved (Q4 → sidebar navigation pattern confirmed) |
| Non-Functional Quality Attributes | ✅ Resolved (Q3 → net8.0 TFM; Q5 → CI zero-warning gate) |
| Integration & External Dependencies | ✅ Clear (no live integrations in scaffold) |
| Edge Cases & Failure Handling | ✅ Clear/N/A (no business logic in scope) |
| Constraints & Tradeoffs | ✅ Resolved (Q1 → Avalonia authoritative; MAUI discrepancy documented) |
| Terminology & Consistency | ✅ Resolved ("basic shell navigation" = sidebar ReactiveUserControl pattern) |
| Completion Signals | ✅ Resolved (Q5 → CI passes, zero warnings, sample tests green) |
| Misc / Placeholders | ✅ Resolved (cross-platform: Windows + macOS per constitution) |

**All 10 categories resolved. No outstanding or deferred items.**

---

## Recommended Next Step

All critical ambiguities are resolved. Proceed to:

```
/speckit.specify
```

Use this clarify.md as the primary input to generate `spec.md` for feature
`001-initial-setup`. The spec should incorporate all FRs, CI requirements, and
out-of-scope declarations above.
