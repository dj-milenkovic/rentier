# Research: CodeQL Security Scanning

**Feature**: 039-codeql-security-scanning  
**Date**: 2025-07-18  
**Status**: Complete

## Research Topics

### R-001: CodeQL Autobuild vs Manual Build for .NET with .slnx

**Decision**: Use manual build (explicit `dotnet restore` + `dotnet build`) instead of CodeQL autobuild.

**Rationale**:
- CodeQL's autobuild does not reliably recognize `.slnx` solution files. The Rentier project uses `Rentier.slnx` (the unified solution format introduced in .NET 9+).
- Manual build ensures all four source projects (Domain, Application, Infrastructure, Desktop) are compiled and analyzed by CodeQL.
- Manual build aligns with GitHub and Microsoft recommendations for non-trivial .NET solutions (2024–2025 best practices).
- The existing CI workflow (`ci.yml`) already uses explicit `dotnet build Rentier.slnx` commands, providing a proven pattern to reuse.

**Alternatives considered**:
- **Autobuild**: Rejected because `.slnx` support is unreliable in CodeQL autobuild. Would risk incomplete analysis with no clear error signal.
- **Individual project builds**: Rejected as unnecessary complexity — the `.slnx` file already includes all four source projects and `dotnet build` handles it correctly.

### R-002: CodeQL Action v3 for C# / .NET 10 Compatibility

**Decision**: Use `github/codeql-action/init@v3`, `github/codeql-action/analyze@v3` with language `csharp`.

**Rationale**:
- CodeQL Action v3 is the current stable release and supports C# analysis for .NET 10 projects.
- The `csharp` language identifier covers all C# source code within the compiled solution.
- No additional query suites or configurations are needed for the default security analysis — GitHub's default CodeQL query suite covers OWASP Top 10, CWE Top 25, and .NET-specific vulnerability patterns.

**Alternatives considered**:
- **CodeQL Action v2**: Deprecated; v3 is the supported version.
- **Custom query packs**: Not needed for initial setup. The default `security-extended` suite provides comprehensive coverage. Custom queries can be added later as needed.

### R-003: Runner OS Selection

**Decision**: Use `ubuntu-latest` as the runner OS for CodeQL analysis.

**Rationale**:
- CodeQL C# analysis works identically on all runner OS types — the analysis is based on the compiled IL, not platform-specific binaries.
- Ubuntu runners are the fastest to provision and cheapest in GitHub Actions minutes.
- The existing CI workflow's SonarCloud job also runs on `ubuntu-latest`, establishing precedent for analysis-only jobs running on Linux.
- The `dotnet build` command with `Rentier.slnx` works on Ubuntu (the CI workflow already builds all source projects on Ubuntu for SonarCloud).

**Alternatives considered**:
- **windows-latest**: Unnecessary — CodeQL C# analysis doesn't require Windows-specific APIs. Would be slower and use more Actions minutes.
- **Matrix build (multi-OS)**: Rejected as wasteful — CodeQL analyzes IL code which is platform-independent. Running on multiple OS would produce duplicate findings.

### R-004: Minimum Permissions for CodeQL

**Decision**: Use `security-events: write`, `contents: read`, `actions: read` as job-level permissions.

**Rationale**:
- `security-events: write`: Required for CodeQL to upload SARIF results to the GitHub Security tab.
- `contents: read`: Required by `actions/checkout@v4` to clone the repository.
- `actions: read`: Required by CodeQL Action v3 to read workflow metadata during analysis.
- All other permissions remain at their default (none) to follow the principle of least privilege.
- This matches FR-013 from the feature spec.

**Alternatives considered**:
- **`packages: read`**: Only needed if pulling dependencies from GitHub Packages. Rentier uses NuGet.org exclusively, so this permission is unnecessary.
- **Workflow-level permissions**: Rejected in favor of job-level permissions to be more explicit and restrictive.

### R-005: NuGet Caching Strategy

**Decision**: Reuse the exact caching pattern from `ci.yml` — hash-based on `**/*.csproj` and `Directory.Build.props`.

**Rationale**:
- The CI workflow has a proven, working NuGet cache configuration.
- Cache key: `nuget-${{ runner.os }}-${{ hashFiles('**/*.csproj', 'Directory.Build.props') }}`
- Restore key: `nuget-${{ runner.os }}-`
- Cache path: `~/.nuget/packages`
- This ensures the CodeQL workflow benefits from cached packages when they exist and creates/updates the cache when dependencies change.

**Alternatives considered**:
- **No caching**: Rejected — would add 30-60 seconds of NuGet restore time on every run.
- **Different cache key**: Rejected — consistency with `ci.yml` means cache hits are shared between workflows, improving overall CI efficiency.

### R-006: Concurrency Controls

**Decision**: Use `codeql-${{ github.ref }}` as the concurrency group with `cancel-in-progress: true`.

**Rationale**:
- Prevents redundant CodeQL runs when multiple pushes/commits happen in quick succession on the same branch or PR.
- Uses a `codeql-` prefix (not `ci-`) to ensure the CodeQL workflow's concurrency is independent from the CI workflow. Sharing the same group would risk cancelling CI builds when CodeQL runs or vice versa.
- Matches the pattern from `ci.yml` (`ci-${{ github.ref }}`) but with a distinct group name.

**Alternatives considered**:
- **Shared concurrency group with CI**: Rejected — would cause CI and CodeQL to cancel each other.
- **No concurrency controls**: Rejected — would waste Actions minutes on redundant analysis runs.

### R-007: Weekly Schedule Timing

**Decision**: Schedule weekly scan for Monday at 06:00 UTC (`cron: '0 6 * * 1'`).

**Rationale**:
- Monday aligns with the spec requirement (FR-004).
- 06:00 UTC avoids peak GitHub Actions usage periods (typically weekday business hours in US time zones).
- Early Monday morning provides fresh security scan results at the start of the work week.
- Scheduled scans run against the repository's default branch automatically.

**Alternatives considered**:
- **Sunday evening**: Rejected — Monday is more useful for actionable start-of-week results.
- **Monday midnight UTC**: Rejected — GitHub Actions can experience higher queue times at midnight boundaries.

### R-008: Interaction with Existing CI Workflow

**Decision**: The CodeQL workflow is completely independent — no shared jobs, no artifact dependencies, no workflow chaining.

**Rationale**:
- FR-011 explicitly requires no modification to or interference with `ci.yml`.
- The CodeQL workflow is a separate file (`.github/workflows/codeql.yml`) with its own triggers, concurrency group, and job definitions.
- The two workflows share NuGet cache (same cache keys) but this is a performance optimization, not a functional dependency.
- Both workflows can run in parallel on the same PR without conflicts — they produce different outputs (test results vs. security findings).

**Alternatives considered**:
- **Adding CodeQL as a job within `ci.yml`**: Rejected — would couple security scanning to the build pipeline, violating FR-011 and making the CI workflow harder to maintain.
- **Using `workflow_run` trigger**: Rejected — adds unnecessary complexity and latency. CodeQL benefits from running independently and immediately.
