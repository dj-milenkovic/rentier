# Implementation Plan: CodeQL Security Scanning

**Branch**: `039-codeql-security-scanning` | **Date**: 2025-07-18 | **Spec**: [spec.md](spec.md)  
**Input**: Feature specification from `.specify/specs/039-codeql-security-scanning/spec.md`

## Summary

Add a GitHub Actions workflow at `.github/workflows/codeql.yml` that performs CodeQL security analysis on the Rentier C# codebase. The workflow triggers on pull requests to `develop`, pushes to `main`, and weekly on Mondays. It uses a manual build approach (not autobuild) because the project uses a `.slnx` solution file, and follows the same .NET 10.x SDK, NuGet caching, and environment variable patterns established in the existing `ci.yml` workflow. This complements the existing SonarCloud analysis as an independent, parallel security scanning layer.

## Technical Context

**Language/Version**: C# / .NET 10.x (consistent with `ci.yml`)  
**Primary Dependencies**: `github/codeql-action@v3`, `actions/checkout@v4`, `actions/setup-dotnet@v4`, `actions/cache@v4`  
**Storage**: N/A (workflow file only; results stored in GitHub Security tab)  
**Testing**: Manual verification via GitHub Actions runs and Security tab inspection  
**Target Platform**: GitHub Actions (`ubuntu-latest` runner)  
**Project Type**: CI/CD workflow (GitHub Actions YAML)  
**Performance Goals**: Analysis completes within 15 minutes for PRs, 20 minutes for full scans (per SC-001, SC-002)  
**Constraints**: Must not modify or interfere with existing `ci.yml` workflow (FR-011)  
**Scale/Scope**: Single workflow file, single job, 7 steps; analyzes 4 source projects (~Rentier.slnx)

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- [x] Clean Architecture boundary is preserved (`Desktop -> Application -> Domain`; Infrastructure implements Application contracts only).
  - **N/A**: No source code changes. This feature adds only a CI/CD workflow file. All Clean Architecture layers remain untouched.
- [x] All monetary/rate/percentage values are modeled as `decimal`.
  - **N/A**: No monetary values, rates, or percentages are introduced or modified.
- [x] All business dates are modeled as `DateOnly`; boundary conversions are identified.
  - **N/A**: No business dates are introduced or modified.
- [x] Security/privacy constraints hold: local-first data, OS credential store for secrets, no telemetry.
  - **PASS**: The workflow runs entirely within GitHub Actions infrastructure. It uses only the built-in `GITHUB_TOKEN` (no additional secrets). The `DOTNET_CLI_TELEMETRY_OPTOUT: true` environment variable is set. No user data is accessed or transmitted.
- [x] External network usage is limited to approved endpoints (IMAP and NBS) or explicitly justified as a constitution amendment.
  - **PASS with justification**: The workflow makes outbound calls to GitHub's own services (CodeQL action downloads) and NuGet.org (package restore). These are standard CI/CD infrastructure calls, not application-level network access. This aligns with CA-004 in the spec. No constitution amendment required — CI/CD infrastructure network access is outside the scope of the application-level network restriction.
- [x] All I/O paths are async; UI work avoids blocking calls and uses reactive async command flow.
  - **N/A**: No application I/O paths or UI interactions are introduced.
- [x] Tests and coverage impact are defined for Domain (rule/state coverage) and Application (>=90%).
  - **N/A**: No Domain or Application code changes. No test impact. Verification is via GitHub Actions execution, not unit tests (per CA-006 in spec).
- [x] Feature work is mapped to an approved spec task under `.specify/tasks/`.
  - **PENDING**: Will be created by `/speckit.tasks` command after plan approval.

## Project Structure

### Documentation (this feature)

```text
.specify/specs/039-codeql-security-scanning/
├── spec.md              # Feature specification (complete)
├── plan.md              # This file
├── research.md          # Phase 0 output — CodeQL v3 research findings
├── data-model.md        # Phase 1 output — workflow structure model
├── quickstart.md        # Phase 1 output — developer quickstart guide
├── checklists/
│   └── requirements.md  # Specification quality checklist (complete)
└── tasks.md             # Phase 2 output (created by /speckit.tasks)
```

### Source Code (repository root)

```text
.github/
└── workflows/
    ├── ci.yml           # Existing — NOT MODIFIED
    ├── release.yml      # Existing — NOT MODIFIED
    └── codeql.yml       # NEW — CodeQL security analysis workflow
```

**Structure Decision**: Single new file added to the existing `.github/workflows/` directory. No new directories, no source code changes, no test changes. The `codeql.yml` workflow is fully self-contained and independent.

## Design: CodeQL Workflow

### Workflow File: `.github/workflows/codeql.yml`

#### Triggers

| Event | Configuration | Requirement |
|-------|--------------|-------------|
| `pull_request` | branches: `[develop]` | FR-002 |
| `push` | branches: `[main]` | FR-003 |
| `schedule` | cron: `0 6 * * 1` (Monday 06:00 UTC) | FR-004 |

#### Concurrency

- **Group**: `codeql-${{ github.ref }}` (distinct from CI's `ci-${{ github.ref }}`)
- **Cancel in progress**: `true`
- **Rationale**: Prevents redundant runs without interfering with CI workflow (FR-012)

#### Environment Variables

Consistent with `ci.yml` (FR-009):

```yaml
env:
  DOTNET_NOLOGO: true
  DOTNET_CLI_TELEMETRY_OPTOUT: true
  DOTNET_SKIP_FIRST_TIME_EXPERIENCE: true
```

#### Job: `analyze`

**Runs on**: `ubuntu-latest`

**Permissions** (FR-013):

| Permission | Level | Required by |
|-----------|-------|-------------|
| `security-events` | `write` | CodeQL SARIF upload |
| `contents` | `read` | Repository checkout |
| `actions` | `read` | CodeQL action metadata |

#### Steps (sequential)

| # | Step | Action/Command | Config |
|---|------|---------------|--------|
| 1 | Checkout | `actions/checkout@v4` | Default (shallow clone sufficient for CodeQL) |
| 2 | Setup .NET | `actions/setup-dotnet@v4` | `dotnet-version: '10.x'` |
| 3 | Cache NuGet | `actions/cache@v4` | key: `nuget-${{ runner.os }}-${{ hashFiles('**/*.csproj', 'Directory.Build.props') }}` |
| 4 | Initialize CodeQL | `github/codeql-action/init@v3` | `languages: csharp` |
| 5 | Restore | `dotnet restore Rentier.slnx` | Full solution restore |
| 6 | Build | `dotnet build Rentier.slnx -c Release --no-restore` | Manual build (not autobuild) per R-001 |
| 7 | Perform Analysis | `github/codeql-action/analyze@v3` | Uploads SARIF to Security tab |

### Key Design Decisions

| Decision | Choice | Research Reference |
|----------|--------|-------------------|
| Manual build over autobuild | `.slnx` not reliably supported by CodeQL autobuild | R-001 |
| Ubuntu runner | Fastest/cheapest; CodeQL analyzes IL (platform-independent) | R-003 |
| Separate concurrency group | Prevents CI/CodeQL mutual cancellation | R-006 |
| Shared NuGet cache keys | Cross-workflow cache reuse for faster restores | R-005 |
| Job-level permissions | Principle of least privilege | R-004 |
| No `fetch-depth: 0` | CodeQL doesn't need full git history (unlike SonarCloud) | Simplified from CI |

### Requirement Traceability

| Requirement | How Addressed |
|-------------|--------------|
| FR-001 | File created at `.github/workflows/codeql.yml` |
| FR-002 | `pull_request: branches: [develop]` trigger |
| FR-003 | `push: branches: [main]` trigger |
| FR-004 | `schedule: - cron: '0 6 * * 1'` (Monday 06:00 UTC) |
| FR-005 | `github/codeql-action/init@v3` + `github/codeql-action/analyze@v3` with `languages: csharp` |
| FR-006 | `actions/setup-dotnet@v4` with `dotnet-version: '10.x'` |
| FR-007 | `actions/cache@v4` with NuGet cache pattern from `ci.yml` |
| FR-008 | Init → manual build → analyze flow |
| FR-009 | `DOTNET_NOLOGO`, `DOTNET_CLI_TELEMETRY_OPTOUT`, `DOTNET_SKIP_FIRST_TIME_EXPERIENCE` env vars |
| FR-010 | CodeQL `analyze` step uploads SARIF → GitHub Security tab |
| FR-011 | Separate workflow file, separate concurrency group, no shared state |
| FR-012 | `concurrency: group: codeql-${{ github.ref }}, cancel-in-progress: true` |
| FR-013 | `permissions: security-events: write, contents: read, actions: read` |

### Edge Case Handling

| Edge Case | Handling |
|-----------|---------|
| C# build failure during analysis | Build step (`dotnet build`) will fail with standard .NET error output. CodeQL `analyze` step is never reached, preventing partial results. |
| Analysis timeout on large changeset | GitHub Actions default job timeout (6 hours) applies. Practical CodeQL runs for this solution size should complete in 10-15 minutes. |
| Scheduled run with no recent changes | Runs against current default branch HEAD. CodeQL re-analyzes and updates findings (may detect newly published vulnerability patterns). |
| Stale/corrupted NuGet cache | Cache `restore-keys` provides fallback. On cache miss, `dotnet restore` performs full package download. Workflow continues normally. |

## Complexity Tracking

> No constitution violations detected. This feature is purely additive CI/CD infrastructure.

| Aspect | Assessment |
|--------|-----------|
| Architecture impact | None — no source code changes |
| Risk level | Low — self-contained workflow file |
| Reversibility | Full — delete single file to revert |
| Dependencies | None on application code; depends on GitHub Actions infrastructure |
