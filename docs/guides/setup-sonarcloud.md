# SonarCloud Setup Guide

> **Audience:** Project maintainer (one-time setup)
> **Time:** ~10 minutes
> **Cost:** Free for open-source projects — no infrastructure to maintain

The Rentier CI pipeline (`.github/workflows/ci.yml`) already contains the
SonarCloud scanning steps. This guide walks you through creating the SonarCloud
project and connecting it to GitHub so those steps can run.

---

## 1. Sign In to SonarCloud

1. Go to [sonarcloud.io](https://sonarcloud.io).
2. Click **Log in** → **Sign in with GitHub**.
3. Authorize SonarCloud to access your GitHub account.

## 2. Import the Repository

1. Click the **+** button (top-right) → **Analyze new project**.
2. Select the GitHub organization that hosts the `rentier` repository.
3. Find and select **rentier** from the repository list.
4. Click **Set Up**.

## 3. Note Organization and Project Keys

After import, SonarCloud assigns two identifiers you will need:

| Key | Where to Find |
|-----|---------------|
| **Organization key** | Organization Settings → top of the page (e.g. `your-org`) |
| **Project key** | Project Settings → General → bottom of the page (e.g. `your-org_rentier`) |

Write these down — you will add them as GitHub variables in the next steps.

## 4. Generate a SONAR_TOKEN

1. Click your avatar (top-right) → **My Account**.
2. Go to the **Security** tab.
3. Under *Generate Tokens*, enter a name (e.g. `rentier-ci`) and click **Generate**.
4. **Copy the token immediately** — it will not be shown again.

## 5. Add the Token as a GitHub Repository Secret

1. Go to the `rentier` repository on GitHub.
2. Navigate to **Settings → Secrets and variables → Actions**.
3. Click **New repository secret**.
4. Name: `SONAR_TOKEN`
5. Value: paste the token from step 4.
6. Click **Add secret**.

## 6. Add Organization and Project Key as GitHub Variables

In the same **Settings → Secrets and variables → Actions** page, switch to the
**Variables** tab.

| Variable name | Value |
|---------------|-------|
| `SONAR_ORGANIZATION` | Your organization key from step 3 |
| `SONAR_PROJECT_KEY` | Your project key from step 3 |

## 7. Verify the CI Pipeline

1. Push a commit or open a pull request to trigger the CI workflow.
2. Go to **Actions** → select the latest CI run.
3. Confirm the SonarCloud analysis step completes without errors.
4. Go back to [sonarcloud.io](https://sonarcloud.io) and verify results appear
   on the project dashboard.

## 8. Configure the Quality Gate

1. In SonarCloud, go to your project → **Project Settings → Quality Gate**.
2. Select **Sonar way** (the built-in default gate). This is recommended for
   most projects and enforces:
   - No new bugs
   - No new vulnerabilities
   - No new security hotspots
   - Code coverage on new code ≥ 80 %
   - Duplication on new code ≤ 3 %
3. Click **Save**.

> **Tip:** You can create a custom quality gate later, but start with
> "Sonar way" — it is well-tuned for .NET projects.

## 9. Enable PR Decoration

PR decoration adds inline comments to pull requests with SonarCloud findings.

1. In SonarCloud, go to **Administration → General Settings → Pull Requests**.
2. Ensure the **GitHub** provider is selected and the integration is active.
3. Verify that the next PR you open receives inline comments from the
   `sonarcloud[bot]` user.

---

## Troubleshooting

| Symptom | Fix |
|---------|-----|
| CI step fails with `Not authorized` | Verify `SONAR_TOKEN` secret is set and not expired |
| No results on SonarCloud dashboard | Check that `SONAR_ORGANIZATION` and `SONAR_PROJECT_KEY` variables match exactly |
| PR decoration not appearing | Ensure the SonarCloud GitHub App is installed on the repository |

---

## Summary

After completing this guide you will have:

- [x] SonarCloud project linked to the `rentier` GitHub repository
- [x] `SONAR_TOKEN` secret and organization/project variables configured
- [x] CI pipeline successfully reporting analysis results
- [x] Quality gate enforcing code quality standards
- [x] Inline PR comments for code review feedback
