# Feature Specification: CodeQL Security Scanning

**Feature Branch**: `039-codeql-security-scanning`  
**Created**: 2025-07-18  
**Status**: Draft  
**Input**: User description: "Add a CodeQL security analysis GitHub Actions workflow as recommended in the DevOps analysis. The workflow should run on pull requests to develop branch and on push to main, run on a weekly schedule (e.g., every Monday), analyze C# code for security vulnerabilities, use GitHub's CodeQL Action v3, follow the standard CodeQL setup for .NET projects, use .NET 10.x SDK (consistent with existing ci.yml), include proper caching of NuGet packages, and be placed at `.github/workflows/codeql.yml`. This is a secondary security scanning tool that complements SonarCloud."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Automated Security Scanning on Code Changes (Priority: P1)

As a developer, when I open a pull request targeting the develop branch, the CodeQL security analysis automatically runs against my changes and reports any security vulnerabilities found in the C# code, so that I can fix issues before they are merged.

**Why this priority**: This is the core value proposition — catching security vulnerabilities during code review before they reach the integration branch. Without this trigger, the entire feature has no purpose.

**Independent Test**: Can be fully tested by opening a pull request to the develop branch and verifying that the CodeQL analysis job runs, completes, and produces security findings visible in the GitHub Security tab.

**Acceptance Scenarios**:

1. **Given** a developer opens a pull request targeting the develop branch, **When** the PR is created or updated with new commits, **Then** the CodeQL security analysis workflow runs automatically against the C# codebase.
2. **Given** the CodeQL analysis completes on a pull request, **When** security vulnerabilities are detected, **Then** the findings appear as alerts in the GitHub Security tab and as annotations on the pull request.
3. **Given** the CodeQL analysis completes on a pull request, **When** no security vulnerabilities are detected, **Then** the workflow completes successfully with a green status check.

---

### User Story 2 - Security Scanning on Main Branch Pushes (Priority: P1)

As a maintainer, when code is pushed to the main branch (via merged pull requests), the CodeQL security analysis runs to ensure the releasable branch remains free of known security vulnerabilities, providing a safety net beyond the PR-level check.

**Why this priority**: The main branch is always releasable per the project's Git workflow. Security scanning on main ensures that any vulnerability missed during PR review is caught immediately after merge.

**Independent Test**: Can be fully tested by pushing a commit to the main branch and verifying the CodeQL workflow triggers, completes, and reports results in the GitHub Security tab.

**Acceptance Scenarios**:

1. **Given** a commit is pushed to the main branch, **When** the push event occurs, **Then** the CodeQL security analysis workflow runs automatically.
2. **Given** the CodeQL analysis runs on main, **When** it detects a new vulnerability not present in prior scans, **Then** a new security alert is created in the repository's Security tab.

---

### User Story 3 - Weekly Scheduled Security Scan (Priority: P2)

As a maintainer, the CodeQL analysis runs on a weekly schedule (every Monday) against the default branch to detect newly disclosed vulnerabilities in existing code, even when no code changes have been made recently.

**Why this priority**: Scheduled scans catch vulnerabilities from newly published security advisories against existing code patterns. This is lower priority than event-driven scans because it provides incremental rather than foundational value.

**Independent Test**: Can be tested by verifying the workflow schedule configuration is correct and confirming the workflow appears in the scheduled runs history after the next Monday occurrence.

**Acceptance Scenarios**:

1. **Given** it is Monday and the scheduled time has arrived, **When** the cron schedule triggers, **Then** the CodeQL analysis runs against the default branch.
2. **Given** a scheduled scan completes, **When** a previously undetected vulnerability is found due to an updated CodeQL rule, **Then** a new alert is created in the GitHub Security tab.

---

### User Story 4 - Complementary Coverage with SonarCloud (Priority: P3)

As a maintainer, the CodeQL security analysis operates independently from SonarCloud, providing a second layer of security scanning that covers different vulnerability categories and detection methodologies.

**Why this priority**: CodeQL and SonarCloud use different analysis engines and rule sets. Having both provides defense-in-depth, but this complementary relationship is a secondary benefit rather than a standalone user journey.

**Independent Test**: Can be verified by confirming that both the CodeQL workflow and the SonarCloud job (in ci.yml) can run independently on the same pull request without conflicts or duplicate reporting.

**Acceptance Scenarios**:

1. **Given** a pull request triggers both CI (with SonarCloud) and CodeQL workflows, **When** both analyses complete, **Then** results are reported independently without conflicts — CodeQL findings in the Security tab and SonarCloud findings in its own dashboard.
2. **Given** the CodeQL workflow is added to the repository, **When** the existing CI workflow runs, **Then** it is unaffected — no changes to build times, test execution, or SonarCloud analysis.

---

### Edge Cases

- What happens when the C# build fails during CodeQL analysis (e.g., due to a transient NuGet restore failure)? The workflow should fail with a clear error message indicating the build step that failed, without producing partial or misleading security results.
- What happens when the CodeQL analysis times out on a large changeset? GitHub has default timeout limits for CodeQL; the workflow should respect these and fail gracefully rather than hang indefinitely.
- What happens when a scheduled run occurs while the repository has no recent changes? The scan should still execute against the current state of the default branch and complete normally.
- What happens when the NuGet package cache is stale or corrupted? The workflow should fall back to a full restore without failing the entire analysis.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The workflow MUST be defined in a single file at `.github/workflows/codeql.yml`.
- **FR-002**: The workflow MUST trigger on pull requests targeting the `develop` branch.
- **FR-003**: The workflow MUST trigger on push events to the `main` branch.
- **FR-004**: The workflow MUST trigger on a weekly cron schedule, running every Monday.
- **FR-005**: The workflow MUST analyze C# code using GitHub's CodeQL Action version 3.
- **FR-006**: The workflow MUST set up .NET 10.x SDK, consistent with the existing CI workflow.
- **FR-007**: The workflow MUST cache NuGet packages to reduce analysis time on subsequent runs.
- **FR-008**: The workflow MUST follow the standard CodeQL initialization, analysis, and autobuild (or manual build) flow for .NET projects.
- **FR-009**: The workflow MUST suppress .NET CLI telemetry, first-time experience, and logo output (consistent with the existing CI workflow environment variables).
- **FR-010**: The workflow MUST produce security findings that appear in the repository's GitHub Security tab (Code scanning alerts).
- **FR-011**: The workflow MUST NOT modify, depend on, or interfere with the existing CI workflow (`ci.yml`).
- **FR-012**: The workflow MUST use concurrency controls to cancel redundant in-progress runs for the same branch/PR.
- **FR-013**: The workflow MUST request only the minimum permissions required for CodeQL analysis (security-events write, contents read, actions read).

### Constitution Alignment *(mandatory)*

- **CA-001 (Architecture)**: No application layers are impacted. This feature adds a CI/CD workflow file only (`.github/workflows/codeql.yml`). Clean Architecture boundaries are not affected as no source code changes to Domain, Application, Infrastructure, or Desktop projects are required.
- **CA-002 (Money and Dates)**: Not applicable. This feature does not introduce or modify any monetary values, rates, or date fields in the codebase.
- **CA-003 (Privacy and Security)**: The CodeQL workflow runs entirely within GitHub Actions infrastructure. No user data, credentials, or secrets beyond GitHub's built-in `GITHUB_TOKEN` are required. The analysis results are stored in GitHub's Security tab, which respects repository access permissions.
- **CA-004 (Network Scope)**: The workflow makes outbound calls only to GitHub's own services (CodeQL action downloads, NuGet package registry for restore). No calls to user mailboxes, NBS endpoints, or any external services outside the standard CI/CD pipeline.
- **CA-005 (Async and UI)**: Not applicable. This feature does not involve any application I/O operations or UI interactions.
- **CA-006 (Testing Impact)**: No Domain, Application, Infrastructure, or Desktop test updates are required. The workflow itself is validated through successful execution in GitHub Actions. Manual verification involves confirming the workflow triggers correctly and produces security alerts.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: The CodeQL workflow runs successfully to completion on every pull request targeting the develop branch, within 15 minutes of being triggered.
- **SC-002**: The CodeQL workflow runs successfully on every push to the main branch, with security findings visible in the repository's Security tab within 20 minutes.
- **SC-003**: The weekly scheduled scan executes every Monday without manual intervention and produces an up-to-date security analysis of the codebase.
- **SC-004**: The existing CI workflow (build, test, SonarCloud) continues to function identically — no increase in CI duration, no test failures, no configuration conflicts introduced by the new workflow.
- **SC-005**: 100% of CodeQL-detected security vulnerabilities in C# code are surfaced as actionable alerts in the GitHub Security tab with clear descriptions and remediation guidance.

## Assumptions

- The repository has GitHub Advanced Security features available (CodeQL is free for public repositories; private repositories require GitHub Advanced Security license).
- The `.NET 10.x` SDK is available in the GitHub Actions runner environment via `actions/setup-dotnet@v4`.
- The existing solution file (`Rentier.slnx`) is the correct build target for CodeQL to analyze all C# source code.
- GitHub's CodeQL Action v3 supports C# analysis for .NET 10 projects without additional configuration.
- The NuGet package caching strategy (keyed on `*.csproj` and `Directory.Build.props` file hashes) from the existing CI workflow is reusable for the CodeQL workflow.
- The weekly schedule (Monday) uses a reasonable time that avoids peak GitHub Actions usage (assumed early morning UTC).
- CodeQL's autobuild feature may not work reliably with `.slnx` solution files; a manual build step (matching the existing CI build approach) may be needed instead.
