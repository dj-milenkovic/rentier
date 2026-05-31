# Project Review

**Project:** Rentier — Serbian PP-OPO passive-income tax filing desktop app
**Stack:** C# 14 / .NET 10, Avalonia (MVVM/ReactiveUI), EF Core + SQLite, Clean Architecture + CQRS
**Reviewer perspective:** Technical Lead / Architect
**Date:** 2026-05-31
**Basis:** Direct inspection of source, tests, CI/CD, and packaging. ~18.4k LOC src, ~1,071 test methods, 4 source projects, 5 test projects, 11 views.

> Scope note: This is a **static** review of the repository. No runtime profiling, real device testing, or live external-integration testing was performed — those gaps are themselves a finding (see §5).

---

## Executive Summary

- **Architecturally strong and disciplined.** Clean Architecture layering is real, not cosmetic: Domain has no EF/HTTP deps, Application defines interfaces, Desktop never touches Infrastructure (enforced by a CI architecture test). This is well above typical solo/early-stage maturity.
- **Financial-correctness primitives are right.** `decimal` for money, `DateOnly` for dates, a `Money` value object, an explicit `Filing` state machine (`Init→Filed→Paid`), correctly **clamped** tax credit (`Math.Max(tax - wht, 0)`), and holiday/weekend-aware deadline calc.
- **Test breadth is excellent (~1,071 tests)** spanning Domain, Application, Infrastructure, headless UI, scenarios, and architecture — but **depth is uneven**: the E2E project is empty, live IMAP tests are skipped, and resilience/perf/soak testing is absent.
- **DevOps maturity is high for a solo project:** multi-OS build/test matrix, format gate, vulnerable-package gate, SonarCloud quality gate, coverage merge, and a fully automated semver release producing Windows/macOS/Linux installers with checksums.
- **Biggest production risk: unsigned binaries.** No Windows Authenticode signing, no macOS notarization. For a finance app this triggers SmartScreen/Gatekeeper warnings and undermines trust.
- **Resilience is thin at the I/O edges.** NBS rate fetch and IMAP sync have no retry/backoff/circuit-breaker. External-service flakiness will surface as user-facing failures.
- **UX is consistent but keyboard/accessibility support is partial.** Standard state model (`IsLoading`/`ErrorMessage`/empty states) is applied uniformly, but **zero keyboard accelerators** and only sparse `AutomationProperties`.
- **Localization is half-built.** A `Localizer`/`Strings.resx` pipeline exists and is used in XAML, but only **one** locale file ships — no Serbian/English split despite a Serbian-taxpayer audience.
- **Single-maintainer bus factor.** One CODEOWNER, one author, no Dependabot/Renovate. Process is mature; the team is not.
- **Verdict: a genuinely well-engineered pre-1.0 product** with a small number of high-leverage gaps (signing, resilience, real E2E) standing between it and production-grade confidence.

---

## 1. UI / UX Design Review

### Strengths
- **Strict UI/logic separation.** Code-behind files are 7–44 lines (e.g. `DashboardView.axaml.cs` = 7, `FilingsView.axaml.cs` = 44). All views are `ReactiveUserControl<TViewModel>` binding to observables — no business logic in the view layer.
- **Consistent state vocabulary.** Every list view follows the same pattern: loading `ProgressBar`, dismissible error banner, empty-state text, pagination. Verified in `ReportsView.axaml` and mirrored across views. This consistency is a real usability asset.
- **Theming via dynamic resources.** Colors use `{DynamicResource RentierDangerForegroundBrush}` etc. (`Assets/Styles/Theme.axaml`, `Controls.axaml`) rather than hardcoded values — supports dark/light and central restyling.
- **Logical UX flow** matching the domain: Import → calculate → review report → generate filing → mark Filed → Paid. The journey mirrors the README pipeline and the filing state machine.

### Gaps / Anti-patterns
- **No keyboard accelerators.** `KeyBinding`/`HotKey` count across all views = **0**. A data-heavy desktop finance tool with no shortcuts (e.g. import, next page, mark filed) is a power-user efficiency gap.
- **Partial accessibility.** Only **8** `AutomationProperties` and **13** `ToolTip` usages across 11 views. Screen-reader labelling and explicit tab order are largely absent — contrast/keyboard-nav cannot be assumed compliant.
- **`x:CompileBindings="False"`** in `ReportsView.axaml` disables compile-time binding checks — binding typos become silent runtime failures. Compiled bindings should be the default.
- **Responsiveness unverified.** Layouts use `DockPanel`/`StackPanel` (good), but there is no evidence of testing at small window sizes or high-DPI scaling beyond macOS `NSHighResolutionCapable`.

### Concrete improvements
1. Add `KeyBinding`s for primary actions and a global navigation shortcut scheme.
2. Add `AutomationProperties.Name`/`LabeledBy` to all interactive controls; define explicit `TabIndex` on forms.
3. Re-enable compiled bindings (`x:CompileBindings="True"`) and fix any surfaced binding errors.
4. Add a headless test asserting key views render at a minimum window size without clipping.

---

## 2. Product / Feature Completeness

### What feels complete
- **Core tax pipeline is end-to-end:** IBKR CSV parse → per-date NBS rate → 15% tax with WHT credit → PP-OPO XML export → filing lifecycle. This is the product's reason to exist and it is implemented.
- **Operational niceties present:** multi-year support, holiday-aware deadlines, IMAP auto-import, OS credential store, column filters/sorting, bulk delete, pagination.
- **Domain edge correctness:** weekend/holiday deadline shifting and credit clamping are exactly the subtle rules a naive implementation gets wrong.

### Missing / underdeveloped
- **Single broker.** Only IBKR is supported; Revolut/Wise are roadmap-only. Statement-format coupling is a product risk if IBKR changes layout (mitigated partially by snapshot tests).
- **No bulk filing export.** Roadmap item — but a real friction point: users file one XML per income event with no "export all for tax year" action.
- **No in-app submission feedback loop.** The app generates XML; the user manually uploads to ePorezi and manually marks Filed/Paid. No reconciliation/validation against what was actually submitted.
- **Single locale shipped** (see §6/i18n) despite a Serbian audience — a product-facing gap, not just technical.
- **No backup/export of the local DB.** `rentier.db` holds the user's entire filing history with no visible export/restore path — a data-loss risk for a records-of-record finance app.

### Product risks
- **Regulatory drift:** the 15% rate and PP-OPO XML schema are hardcoded domain knowledge; a Serbian tax-law or ePorezi-schema change requires a code release. No external config for rate/schema.
- **Correctness liability:** the disclaimer is appropriate, but a wrong rate/credit calculation has real financial consequences — raising the bar for test depth (§4) and traceability (§6).

---

## 3. Code Quality & Architecture

### Strengths
- **Layering is genuine and enforced.** Domain is dependency-free; Application depends only on Domain and defines `IFilingRepository`, `IExchangeRateFetcher`, `IHolidayRepository`; Infrastructure implements them; Desktop calls Application. `tests/Rentier.UnitTests/Architecture/LayerDependencyTests.cs` asserts Desktop ⇏ Infrastructure in CI.
- **Domain services isolate the hard logic** — `TaxCalculationService`, `FilingDeadlineCalculator`, `BusinessDayResolver` — keeping handlers thinner and the math testable in isolation.
- **Result pattern for expected failures.** `Result<T,Error>` is used across handlers and infra (`UpdateFilingStatusCommandHandler` translates `DomainException` → `Error.Domain`), avoiding exception-as-control-flow at the application boundary.
- **Central package management** (`Directory.Packages.props`, `ManagePackageVersionsCentrally=true`) and **`TreatWarningsAsErrors=true` + `Deterministic=true`** in `Directory.Build.props` — strong baseline hygiene.

### Critical issues / red flags
- **`FilingStatus` is a plain public enum.** The state machine is enforced only by discipline inside `AdvanceStatus`; invalid states remain *representable*. A type-driven design (or guarding all mutation through the aggregate) would make illegal states unrepresentable.
- **`Filing.TaxPeriod = incomeDate`** (`Filing.cs:~85`) is a modeling smell — tax period set to the income date rather than a derived period. Worth verifying against PP-OPO semantics.
- **Primitive obsession / partly anemic aggregate.** `Money` exists but `Filing` still carries multiple raw `decimal` fields; the aggregate is largely data + one transition method.
- **Application contracts leak infrastructure knowledge.** `IExchangeRateFetcher` XML-doc says "Checks the local SQLite cache… falls back to the NBS XML web service," and `ExchangeRateResolver` hardcodes infra error codes (`"UNSUPPORTED_CURRENCY"`, `"NBS_PARSE_ERROR"`). Abstractions should not know about SQLite/NBS.
- **`CreateManualFilingCommandHandler` orchestrates too much** (calculation + duplicate check + entity creation + persistence) — push more into domain/services.
- **No mediator abstraction** — custom `ICommandHandler`/`IQueryHandler` interfaces are fine, but DI wiring of handlers is manual; verify there's no service-locator creep in `Composition/`.

### Refactoring recommendations (prioritized)
1. **(High)** Make illegal `Filing` states unrepresentable — guard all state changes through the aggregate; consider a discriminated state type.
2. **(High)** Scrub infra leakage from Application contracts (rename/relocate error codes to a shared Application enum; remove SQLite/NBS references from interface docs).
3. **(Med)** Resolve `TaxPeriod` semantics and document the rule with a test.
4. **(Med)** Thin out `CreateManualFilingCommandHandler`; move duplicate-detection into a domain service.
5. **(Low)** Expand `Money` usage to replace raw `decimal` fields in `Filing`.

---

## 4. Test Coverage & Test Quality

### Coverage (by inspection)
- **~1,071 test methods.** Files: `Rentier.UnitTests` 114, `Rentier.Infrastructure.Tests` 47, `Rentier.Scenarios.Tests` 15, `Rentier.Tests.Common` 13, **`Rentier.E2E.Tests`: no source tests (empty)**.
- **Domain tests are mock-free** and include **property-based tests** (`FsCheck.Xunit`, `Domain/Properties/*`) — strong for math-heavy invariants.
- **Application tests use NSubstitute** correctly (e.g. `SyncMailboxCommandHandlerTests`).
- **Infrastructure tests have real edge cases:** malformed/empty HTML, HTTP 500, request exceptions in `NbsWebScraperTests`; XML/CSV **snapshot tests** for serializer stability.
- **Headless UI tests** exist (`Avalonia.Headless`, many `Desktop/*HeadlessTests.cs`) — above-average for a desktop app.

### Coverage gaps
- **E2E project is empty** — no full-pipeline automation despite the project existing.
- **Live IMAP integration is skipped** (`ImapSyncIntegrationTests` are placeholder/skip), so real mailbox behavior is untested.
- **NBS network path is only mock-tested** — no contract test against a recorded real response corpus; a real NBS HTML/XML change would pass CI but break production.

### Weak patterns
- **Nondeterminism in production code under test:** `DateTimeOffset.Now`/`DateTime.UtcNow` in `ImapMailboxSyncService` (cursor/progress) is not injected via a clock abstraction — risks flaky/time-dependent tests.
- **Single-fixture scraper tests** rely on hand-written HTML only; no regression corpus from real statements/NBS pages.
- Some infra tests are smoke/placeholder rather than assertions.

### Recommendations
1. Inject an `IClock` (or `TimeProvider`) and ban `DateTime.Now`/`UtcNow` outside it — deterministic time everywhere.
2. Populate `Rentier.E2E.Tests` with at least one headless full-pipeline test (CSV in → XML out) using golden files.
3. Add **recorded-response contract tests** for NBS (HTML + XML) and a CSV regression corpus from anonymized real IBKR statements.
4. Add coverage **quality gates per layer** in Sonar (e.g. Domain ≥ 90%), not just an overall number.

---

## 5. Desktop Application Testing Standards

| Dimension | Status | Risk |
|---|---|---|
| Unit / Application / Domain | ✅ Strong | Low |
| Headless UI (ViewModel/view) | ✅ Present | Low–Med |
| Full E2E UI automation | ❌ Empty project | **High** |
| Performance / memory profiling | ❌ None | Med |
| Long-running / soak stability | ❌ None | Med (IMAP polling, long sessions) |
| Install / upgrade testing | ❌ None | **High** (DB migration on upgrade) |
| Packaging / artifact validation | ⚠️ Built, not smoke-tested | **High** |
| Recovery / resilience | ❌ No retry/backoff/circuit-breaker | **High** |
| Cross-platform runtime | ⚠️ Built on 3 OS, behavior not asserted | Med |
| Error handling / logging validation | ⚠️ Result pattern good; logging coverage unverified | Med |

### Risk assessment
- **Upgrade/migration is the scariest untested path.** EF Core migrations exist, but there is **no test that an old `rentier.db` upgrades cleanly** to a new schema. For a records-of-record finance app, a botched migration = lost/garbled filing history.
- **Artifacts are produced but never launched in CI.** The release pipeline builds ZIP/EXE/DMG/DEB but never installs or smoke-runs them — a broken single-file/trimmed build would ship undetected.
- **External-edge fragility:** no retry on transient NBS/IMAP failures means normal internet hiccups become user-visible errors.

### What to add next (practical priority)
1. **Migration test:** seed a DB from the previous released schema, run migrations, assert data integrity.
2. **Artifact smoke test:** after publish, launch the binary headless on each OS and assert it starts (`--version`/health flag).
3. **Resilience:** wrap NBS/IMAP calls in Polly (retry + jittered backoff + circuit breaker) and test it.
4. **Soak test:** run the IMAP poller for an extended loop and watch for handle/memory growth.

---

## 6. DevOps & Engineering Maturity

### Strengths
- **CI (`ci.yml`) is genuinely good:** fast-fail `lint` job (`dotnet format --verify-no-changes` + `dotnet list package --vulnerable` gate), then a **3-OS build/test matrix** with coverage collection, TRX summaries, merged ReportGenerator coverage posted to PRs, and a **SonarCloud quality gate**.
- **Release automation is excellent:** `auto-release.yml` derives semver from Conventional-Commit PR titles, tags, and calls a reusable `release.yml` that cross-builds Windows (ZIP + InnoSetup), macOS (.app + .dmg, x64 & arm64), Linux (tar.gz + .deb), generates **SHA256 checksums** and release notes. `[skip release]` escape hatch and shell-injection guard on version input show care.
- **Build reproducibility:** `Deterministic=true`, `EmbedUntrackedSources=true`, central package versions, `TreatWarningsAsErrors=true`.
- **Code review structure:** CODEOWNERS, PR-title lint (`pr-title.yml`), CodeQL workflow.

### Gaps / red flags
- **No binary signing/notarization.** `release.yml` has **zero** signing/notarization steps. Windows SmartScreen and macOS Gatekeeper will warn/block users — unacceptable for a finance app at GA.
- **No automated dependency updates.** No Dependabot/Renovate config (`.github/dependabot.yml` absent, `renovate.json` absent). The vulnerable-package CI gate is reactive only.
- **Bus factor = 1.** Single CODEOWNER (`@dj-milenkovic`), single author across 120 commits. CODEOWNERS-based review is theater with one person.
- **Releases fire on every merge to main.** Auto-release on each push is powerful but risky without a staging/manual-approval gate — every merge ships a public GA release.
- **No traceability of correctness to releases.** No changelog category linking tax-logic changes to versions; for a tax tool, calculation changes deserve explicit release annotation.
- **Quality gate config not fully verified** — confirm Sonar gate actually fails the PR (it's wired via `sonarqube-quality-gate-action`, good) and that coverage thresholds are enforced, not just reported.

### Recommendations
1. Add Authenticode signing (Windows) and codesign + notarization (macOS) to `release.yml`.
2. Add Dependabot (NuGet + GitHub Actions ecosystems).
3. Introduce a release gate (e.g. require a `release:` label or manual approval) instead of auto-shipping every merge — or auto-publish as **draft/prerelease** and promote manually.
4. Pin GitHub Actions to commit SHAs (supply-chain hardening) — currently pinned to version tags, which are mutable.

---

## Top 5 Prioritized Improvements

1. **Sign & notarize release binaries** *(High impact / Med effort)* — removes the single biggest barrier to user trust and install success for a finance app.
2. **Add resilience (Polly) to NBS + IMAP I/O** *(High / Low–Med)* — converts the most common real-world failure (network blips) from user-facing errors into transparent retries.
3. **Add a DB migration/upgrade test + artifact smoke test** *(High / Med)* — protects the user's records-of-record across upgrades and prevents shipping a broken installer.
4. **Make illegal `Filing` states unrepresentable & scrub infra leakage from Application contracts** *(Med / Med)* — hardens the correctness-critical core and the architecture you've already invested in.
5. **Inject `TimeProvider`/`IClock` and populate the empty E2E project with one golden-file pipeline test** *(Med / Low–Med)* — kills nondeterminism and gives a real safety net over the CSV→XML path.

---

## 30–60 Day Technical Lead Action Plan

### Quick wins (first 2 weeks)
- Add **Dependabot** (NuGet + Actions).
- Re-enable **compiled bindings** in views; fix surfaced errors.
- Introduce **`TimeProvider`** and remove `DateTime.Now`/`UtcNow` from `ImapMailboxSyncService`.
- Switch auto-release to publish **draft/prerelease**, promote manually (de-risk every-merge GA).
- Add `AutomationProperties.Name` + `TabIndex` to the highest-traffic views (Reports, Filings, ManualFiling).

### Strategic (weeks 3–8)
- **Code signing + macOS notarization** in the release pipeline (needs certs/secrets — start procurement now).
- **Resilience layer** (Polly) around NBS/IMAP, with tests using recorded responses.
- **Migration test harness** (seed prior schema → migrate → assert) + **artifact launch smoke test** per OS.
- **NBS/IBKR contract corpus**: anonymized real statements + recorded NBS responses as golden regression tests.
- **Populate `Rentier.E2E.Tests`** with a headless full-pipeline test.
- **Architecture tests**: expand beyond the single Desktop⇏Infrastructure check to full layer-rule enforcement (e.g. NetArchTest) and a "no `DateTime.Now`" analyzer rule.
- **DB export/backup feature** (product + data-safety).

### Team/process
- Document a **release runbook** and a **tax-logic change protocol** (any change to rate/credit/schema requires a property test + explicit changelog entry).
- Recruit/assign a **second reviewer** to make CODEOWNERS meaningful.

---

## Overall Verdict

**Maturity level: Advanced pre-1.0 / "production-capable core, not yet production-hardened."**

Rentier is, frankly, better engineered than most solo or early-team desktop products: the Clean Architecture is real and CI-enforced, the financial primitives are correct, the test breadth (~1,071 tests, property-based + snapshot + headless UI) is impressive, and the CI/CD + cross-platform release automation is genuinely senior-level work.

What separates it from "production-grade" is a short, specific list: **unsigned binaries, no I/O resilience, no upgrade/artifact validation, and an empty E2E project** — plus a single-maintainer bus factor. None require re-architecture; they're additive hardening on a sound foundation. Close the Top 5 and this moves confidently to a trustworthy 1.0.

*Where evidence was unavailable (runtime performance, real DPI/resize behavior, actual Sonar coverage thresholds, live external integrations), this review says so explicitly rather than assuming — and several of those absences are themselves the recommended next tests.*
