# Tasks: CodeQL Security Scanning

**Feature**: `039-codeql-security-scanning`  
**Input**: `.specify/specs/039-codeql-security-scanning/` (spec.md, plan.md, research.md, data-model.md, quickstart.md)  
**Target file**: `.github/workflows/codeql.yml` (single new file — no source code changes)

**Tests**: No automated tests. Verification is manual via GitHub Actions runs and the Security tab (per CA-006 in spec). Each story phase includes an Independent Test description only.

**Organization**: Tasks follow user story priority order. All stories deliver through the same workflow file, so Phase 2 creates the complete file; Phase 3+ tasks verify each story's trigger and acceptance criteria.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files or no dependencies on incomplete tasks)
- **[Story]**: Which user story this task belongs to (US1–US4)

---

## Phase 1: Setup

**Purpose**: Review and capture the existing patterns from `ci.yml` to ensure `codeql.yml` is consistent.

- [X] T001 Review `.github/workflows/ci.yml` and record: NuGet cache key pattern (`nuget-${{ runner.os }}-${{ hashFiles('**/*.csproj', 'Directory.Build.props') }}`), .NET SDK version (`10.x`), and env vars (`DOTNET_NOLOGO`, `DOTNET_CLI_TELEMETRY_OPTOUT`, `DOTNET_SKIP_FIRST_TIME_EXPERIENCE`) for reuse in `codeql.yml`

---

## Phase 2: Foundational (Blocking Prerequisite)

**Purpose**: Create the complete, self-contained CodeQL workflow file that all four user stories depend on. This single file is the entire deliverable for this feature.

**⚠️ CRITICAL**: All user story verification depends on this phase being complete.

- [X] T002 Create `.github/workflows/codeql.yml` with the following complete structure (all components required — see plan.md Design section):
  - **Name**: `CodeQL`
  - **Triggers**: `pull_request: branches: [develop]`, `push: branches: [main]`, `schedule: - cron: '0 6 * * 1'`
  - **Concurrency**: `group: codeql-${{ github.ref }}`, `cancel-in-progress: true`
  - **Env vars**: `DOTNET_NOLOGO: true`, `DOTNET_CLI_TELEMETRY_OPTOUT: true`, `DOTNET_SKIP_FIRST_TIME_EXPERIENCE: true`
  - **Job** `analyze` on `ubuntu-latest` with permissions: `security-events: write`, `contents: read`, `actions: read`
  - **Step 1** — Checkout: `actions/checkout@v4` (no `fetch-depth` override needed)
  - **Step 2** — Setup .NET: `actions/setup-dotnet@v4` with `dotnet-version: '10.x'`
  - **Step 3** — Cache NuGet: `actions/cache@v4`, `path: ~/.nuget/packages`, `key: nuget-${{ runner.os }}-${{ hashFiles('**/*.csproj', 'Directory.Build.props') }}`, `restore-keys: nuget-${{ runner.os }}-`
  - **Step 4** — Init CodeQL: `github/codeql-action/init@v3` with `languages: csharp`
  - **Step 5** — Restore: `run: dotnet restore Rentier.slnx`
  - **Step 6** — Build: `run: dotnet build Rentier.slnx -c Release --no-restore` (manual build — autobuild rejected for `.slnx` per R-001)
  - **Step 7** — Analyze: `github/codeql-action/analyze@v3` (uploads SARIF to GitHub Security tab)

**Checkpoint**: `codeql.yml` exists and is syntactically valid YAML. All 3 triggers, 7 steps, concurrency, permissions, and env vars are present.

---

## Phase 3: User Story 1 — Automated Scanning on Pull Requests (Priority: P1) 🎯 MVP

**Goal**: CodeQL runs automatically on every pull request targeting `develop`, reporting security vulnerabilities before merge.

**Independent Test**: Open a pull request targeting `develop` → verify the CodeQL "analyze" job appears in PR checks → confirm it completes successfully → confirm any findings appear in the GitHub Security tab as code scanning alerts.

### Verification for User Story 1

- [X] T003 [US1] Confirm `pull_request: branches: [develop]` trigger is present and correctly indented in `.github/workflows/codeql.yml` (FR-002)
- [X] T004 [US1] Confirm the `analyze` job produces SARIF output: verify `github/codeql-action/analyze@v3` step is the final step with no additional configuration needed for default security queries (FR-005, FR-010)
- [X] T005 [US1] Confirm the manual build approach (`dotnet restore Rentier.slnx` then `dotnet build Rentier.slnx -c Release --no-restore`) is consistent with the build commands used in `ci.yml` for Ubuntu (R-001, FR-008)

**Checkpoint**: US1 fully implementable — PR trigger and analysis steps are complete. Manual verification: create a draft PR to `develop` to trigger the workflow.

---

## Phase 4: User Story 2 — Scanning on Main Branch Pushes (Priority: P1)

**Goal**: CodeQL runs automatically after every merge to `main`, ensuring the releasable branch remains vulnerability-free.

**Independent Test**: Push or merge a commit to `main` → verify the CodeQL "analyze" job appears in the Actions tab → confirm it completes and results are visible in the GitHub Security tab.

### Verification for User Story 2

- [X] T006 [US2] Confirm `push: branches: [main]` trigger is present and correctly indented in `.github/workflows/codeql.yml` (FR-003)
- [X] T007 [US2] Confirm the concurrency group `codeql-${{ github.ref }}` ensures that a new push to `main` cancels any in-progress run for the same ref without affecting CI workflow runs (FR-012, R-006)

**Checkpoint**: US2 fully implementable — push trigger is verified. Manual verification: merge a PR to `main` to trigger the workflow.

---

## Phase 5: User Story 3 — Weekly Scheduled Security Scan (Priority: P2)

**Goal**: CodeQL runs automatically every Monday at 06:00 UTC against the default branch, detecting newly disclosed vulnerabilities even without code changes.

**Independent Test**: Confirm cron expression `0 6 * * 1` is present in `codeql.yml` and resolves to Monday 06:00 UTC. After the next Monday occurrence, confirm the workflow appears in the Actions "scheduled" run history.

### Verification for User Story 3

- [X] T008 [US3] Confirm `schedule: - cron: '0 6 * * 1'` is present and correctly formatted in `.github/workflows/codeql.yml` (FR-004, R-007)
- [X] T009 [US3] Validate the cron expression using a cron parser: `0 6 * * 1` = "At 06:00 on Monday" — confirm this is the intended Monday 06:00 UTC schedule and not an off-by-one in day-of-week indexing (0=Sunday, 1=Monday in GitHub Actions cron)

**Checkpoint**: US3 fully implementable — schedule trigger is verified syntactically. Functional verification requires waiting until next Monday 06:00 UTC.

---

## Phase 6: User Story 4 — Complementary Coverage with SonarCloud (Priority: P3)

**Goal**: CodeQL operates independently from SonarCloud and the existing `ci.yml` workflow — no conflicts, no shared state, no interference.

**Independent Test**: On a pull request, confirm that both the CI workflow (lint, build, coverage, sonar jobs) and the CodeQL workflow ("analyze" job) appear as separate check suites and complete independently.

### Verification for User Story 4

- [X] T010 [US4] Confirm `codeql.yml` uses concurrency group prefix `codeql-` (not `ci-`) so CodeQL and CI workflows cannot cancel each other (FR-011, FR-012, R-006)
- [X] T011 [US4] Confirm `codeql.yml` has no `needs:` dependencies, no `workflow_run:` triggers, no artifact downloads from `ci.yml`, and no shared job IDs with `ci.yml` — the two workflows are fully independent (FR-011, R-008)
- [X] T012 [US4] Confirm `codeql.yml` uses only `GITHUB_TOKEN` (built-in, no additional secrets required) and does not reference `SONAR_TOKEN` or any other secret defined in `ci.yml` (CA-003, FR-013)

**Checkpoint**: US4 verified — CodeQL and SonarCloud operate as independent, non-conflicting security scanning layers.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Final validation pass against all functional requirements and acceptance criteria.

- [X] T013 [P] Validate `codeql.yml` YAML syntax using a linter or `gh workflow list` to confirm GitHub Actions accepts the file without parse errors
- [X] T014 [P] Perform a requirements traceability check: confirm every FR (FR-001 through FR-013) is addressed by an element in `codeql.yml` per the traceability table in plan.md
- [X] T015 Run quickstart.md verification checklist: confirm the 5 verification steps (PR trigger, push trigger, schedule trigger, Security tab results, CI independence) are all achievable with the created file — document any deviations
- [X] T016 Confirm `ci.yml` is unmodified: `git diff HEAD -- .github/workflows/ci.yml` shows no changes (FR-011, SC-004)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: No dependencies — can start immediately
- **Phase 2 (Foundational)**: Depends on Phase 1 completion — **BLOCKS all verification phases**
- **Phases 3–6 (User Stories)**: All depend on Phase 2 completion; verification tasks within each phase are independent of other story phases
- **Phase 7 (Polish)**: Depends on Phases 2–6 completion

### User Story Dependencies

| Story | Priority | Depends On | Can Start After |
|-------|----------|------------|-----------------|
| US1 — PR trigger | P1 | Phase 2 only | T002 complete |
| US2 — Main push trigger | P1 | Phase 2 only | T002 complete |
| US3 — Weekly schedule | P2 | Phase 2 only | T002 complete |
| US4 — SonarCloud independence | P3 | Phase 2 only | T002 complete |

### Parallel Opportunities

All Phase 3–6 verification tasks can run in parallel once Phase 2 is complete (different concerns, same file but read-only verification):

```
# After T002 completes, launch all story verifications simultaneously:
Task: T003-T005  (US1 PR trigger verification)
Task: T006-T007  (US2 push trigger verification)
Task: T008-T009  (US3 schedule verification)
Task: T010-T012  (US4 independence verification)

# Phase 7 polish tasks T013 and T014 can run in parallel:
Task: T013  (YAML syntax validation)
Task: T014  (requirements traceability check)
```

---

## Implementation Strategy

### MVP First (User Stories 1 + 2 Only — Both P1)

1. Complete Phase 1: Review `ci.yml` patterns (T001)
2. Complete Phase 2: Create `codeql.yml` (T002) — this single task delivers all P1 and P2 stories
3. Complete Phase 3: Verify US1 PR trigger (T003–T005)
4. Complete Phase 4: Verify US2 push trigger (T006–T007)
5. **STOP and VALIDATE**: Open a draft PR to `develop` to trigger US1 manually
6. Proceed with US3 (Phase 5) and US4 (Phase 6) once P1 stories are confirmed working

### Incremental Delivery

Since all stories are delivered by one file (T002), incremental delivery means:
1. Create `codeql.yml` → all triggers activate on merge
2. US1 (PR scanning) — verified on first PR to `develop` after merge
3. US2 (main scanning) — verified on next push/merge to `main`
4. US3 (weekly schedule) — verified on next Monday 06:00 UTC
5. US4 (independence) — verified by observing both CI and CodeQL checks on any PR

### Note on Single-File Nature

This feature's entire implementation is **one file** (`codeql.yml`). T002 is the only implementation task — all subsequent tasks are verification. The primary risk is YAML syntax errors; validate with `gh workflow list` or a YAML linter immediately after creating the file.

---

## Task Summary

| Phase | Tasks | User Story | Priority |
|-------|-------|-----------|----------|
| Phase 1: Setup | T001 | — | — |
| Phase 2: Foundational | T002 | All | — |
| Phase 3: US1 | T003–T005 | US1 | P1 |
| Phase 4: US2 | T006–T007 | US2 | P1 |
| Phase 5: US3 | T008–T009 | US3 | P2 |
| Phase 6: US4 | T010–T012 | US4 | P3 |
| Phase 7: Polish | T013–T016 | — | — |
| **Total** | **16 tasks** | 4 stories | — |

**Suggested MVP scope**: T001 + T002 (create `codeql.yml`) — delivers all 4 user stories in a single commit.

---

## Notes

- [P] tasks = different files or read-only verification; no write conflicts
- This is a pure CI/CD infrastructure feature — no Domain, Application, Infrastructure, or Desktop source code is touched
- No automated tests exist for this feature; all verification is manual via GitHub Actions
- Reverting this feature is trivial: delete `.github/workflows/codeql.yml`
- GitHub Advanced Security must be enabled on the repository for CodeQL findings to appear in the Security tab (free for public repos)
