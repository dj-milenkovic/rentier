# Quickstart: CodeQL Security Scanning

**Feature**: 039-codeql-security-scanning  
**Date**: 2025-07-18

## What This Feature Does

Adds a GitHub Actions workflow (`.github/workflows/codeql.yml`) that runs GitHub's CodeQL security analysis on the Rentier C# codebase. It detects security vulnerabilities automatically on pull requests, main branch pushes, and on a weekly schedule.

## Implementation Scope

**Single file to create**: `.github/workflows/codeql.yml`

No source code changes are required. No existing files are modified.

## Key Decisions

| Decision | Choice | Why |
|----------|--------|-----|
| Build mode | Manual (`dotnet build`) | `.slnx` not reliably supported by autobuild |
| Runner OS | `ubuntu-latest` | Fastest, cheapest; CodeQL analyzes IL (platform-independent) |
| Concurrency | `codeql-${{ github.ref }}` | Independent from CI workflow's concurrency group |
| Schedule | Monday 06:00 UTC | Avoids peak hours; fresh results for start of week |

## Prerequisites

- Repository must have GitHub Advanced Security enabled (free for public repos; license required for private repos)
- The `.NET 10.x` SDK must be available via `actions/setup-dotnet@v4`

## Verification Steps

After merging the workflow file:

1. **PR trigger**: Open a PR targeting `develop` → verify CodeQL job appears in PR checks
2. **Push trigger**: Merge to `main` → verify CodeQL job runs in Actions tab
3. **Schedule trigger**: Wait until Monday 06:00 UTC → verify scheduled run in Actions tab
4. **Results**: Check repository Security tab → Code scanning alerts should show CodeQL results
5. **CI independence**: Verify `ci.yml` jobs (lint, build, coverage, sonar) are unaffected

## Files

| File | Action | Description |
|------|--------|-------------|
| `.github/workflows/codeql.yml` | **Create** | CodeQL security analysis workflow |
