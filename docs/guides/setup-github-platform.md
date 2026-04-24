# GitHub Platform Setup Guide

> **Audience:** Project maintainer (one-time setup)
> **Time:** ~15 minutes
> **Covers:** Secrets & variables, CodeQL code scanning, branch protection rules

---

## A. GitHub Secrets & Variables

Navigate to **Settings → Secrets and variables → Actions** in the `rentier`
repository.

### Required Secrets

| Secret | Purpose | Where to Get |
|--------|---------|--------------|
| `SONAR_TOKEN` | SonarCloud authentication | sonarcloud.io → My Account → Security → Generate Token |

### Required Variables

Switch to the **Variables** tab to add these:

| Variable | Purpose | Where to Get |
|----------|---------|--------------|
| `SONAR_ORGANIZATION` | SonarCloud organization key | sonarcloud.io → Organization Settings |
| `SONAR_PROJECT_KEY` | SonarCloud project key | sonarcloud.io → Project Settings → General |

### Built-in Token

No other secrets are needed. All GitHub Actions workflows use the built-in
`GITHUB_TOKEN` which is automatically available to every workflow run.

> **See also:** [setup-sonarcloud.md](setup-sonarcloud.md) for the full
> SonarCloud onboarding walkthrough.

---

## B. Enable CodeQL Code Scanning

CodeQL performs semantic code analysis to find security vulnerabilities and
coding errors.

> **Note:** A dedicated CodeQL workflow file (`.github/workflows/codeql.yml`)
> will be added as part of the DevOps code changes. The steps below enable the
> GitHub platform feature that displays CodeQL results.

### Steps

1. Go to repository **Settings → Code security and analysis** (under the
   *Security* section in the sidebar).
2. Scroll to **Code scanning**.
3. Click **Set up** next to *CodeQL analysis*.
4. If prompted to choose a setup method, select **Advanced** (we supply our own
   workflow file).
5. If GitHub auto-creates a workflow file PR, you may close it — the project
   already includes its own `codeql.yml`.
6. Verify that the **Code scanning alerts** section appears under the
   repository's **Security** tab.

### What CodeQL Scans

For the Rentier .NET project, CodeQL will analyze:

- C# source code for security vulnerabilities (SQL injection, path traversal,
  insecure deserialization, etc.)
- Common coding errors and quality issues

Results surface directly in pull requests and under **Security → Code scanning
alerts**.

---

## C. Branch Protection Rules

Rentier follows a structured branching model:

```
feature/* ──→ develop ──→ main ──→ tag v*.*.* ──→ GitHub Release
```

Configure protection rules for both `main` and `develop`.

### Protect the `main` Branch

1. Go to **Settings → Branches**.
2. Click **Add branch protection rule** (or **Add classic branch protection
   rule** if prompted).
3. **Branch name pattern:** `main`
4. Enable the following:

| Setting | Value |
|---------|-------|
| **Require a pull request before merging** | ✅ Enabled |
| — Require approvals | 1 (minimum) |
| — Dismiss stale pull request approvals when new commits are pushed | ✅ |
| **Require status checks to pass before merging** | ✅ Enabled |
| — Require branches to be up to date before merging | ✅ |
| — Status checks: | `build`, `sonarcloud` (add once CI has run at least once) |
| **Require conversation resolution before merging** | ✅ (recommended) |
| **Do not allow force pushes** | ✅ Enabled |
| **Do not allow deletions** | ✅ Enabled |

5. Click **Create** (or **Save changes**).

### Protect the `develop` Branch

1. Click **Add branch protection rule** again.
2. **Branch name pattern:** `develop`
3. Enable the same settings as `main` with these differences:

| Setting | Difference from `main` |
|---------|----------------------|
| Require approvals | 1 (same) |
| Do not allow deletions | Optional — may leave unchecked for flexibility |

4. Click **Create**.

### Add SonarCloud as a Required Status Check

The SonarCloud quality gate status check name is typically:

```
SonarCloud Code Analysis
```

If the exact name differs, you can find it by:

1. Opening a completed PR that ran the CI pipeline.
2. Scrolling to the status checks section at the bottom of the PR.
3. Copying the exact check name shown there.

Add this name under the required status checks for both `main` and `develop`.

---

## Branching & Release Flow

```
feature/ibkr-import ─┐
feature/nbs-rates ────┤
hotfix/deadline-calc ─┤
                      ▼
                   develop  ← integration branch, CI runs on every push/PR
                      │
                      ▼
                    main    ← stable, release-ready code
                      │
                      ▼
                  tag v1.0.0 ──→ GitHub Release (with build artifacts)
```

| Action | Branch | Merge Target |
|--------|--------|-------------|
| New feature | `feature/*` | `develop` |
| Bug fix | `fix/*` | `develop` |
| Hotfix | `hotfix/*` | `main` and `develop` |
| Release | `develop` | `main` (tag after merge) |

---

## Verification Checklist

After completing all sections:

- [ ] `SONAR_TOKEN` secret is set in repository settings
- [ ] `SONAR_ORGANIZATION` and `SONAR_PROJECT_KEY` variables are set
- [ ] CodeQL is enabled under Code security and analysis
- [ ] `main` branch protection rule is active with required checks
- [ ] `develop` branch protection rule is active with required checks
- [ ] Force pushes are disabled on `main`
- [ ] Branch deletion is disabled on `main`
- [ ] A test PR triggers CI and all status checks appear
