# Data Model: CodeQL Security Scanning

**Feature**: 039-codeql-security-scanning  
**Date**: 2025-07-18  
**Status**: Complete

## Overview

This feature introduces no new data entities, value objects, or domain models. It is a CI/CD infrastructure feature that adds a single GitHub Actions workflow file.

## Workflow Structure Model

The "data model" for this feature is the workflow file structure itself:

### Workflow: `codeql.yml`

| Element | Value | Source |
|---------|-------|--------|
| **Name** | `CodeQL` | Convention |
| **File** | `.github/workflows/codeql.yml` | FR-001 |
| **Runner** | `ubuntu-latest` | R-003 research |
| **Language** | `csharp` | FR-005 |

### Triggers

| Trigger | Configuration | Requirement |
|---------|--------------|-------------|
| `pull_request` | branches: `[develop]` | FR-002 |
| `push` | branches: `[main]` | FR-003 |
| `schedule` | cron: `0 6 * * 1` (Monday 06:00 UTC) | FR-004 |

### Permissions (Job-level)

| Permission | Level | Purpose |
|-----------|-------|---------|
| `security-events` | `write` | Upload SARIF results to Security tab |
| `contents` | `read` | Checkout repository |
| `actions` | `read` | CodeQL Action metadata |

### Environment Variables

| Variable | Value | Purpose |
|----------|-------|---------|
| `DOTNET_NOLOGO` | `true` | Suppress .NET logo output |
| `DOTNET_CLI_TELEMETRY_OPTOUT` | `true` | Disable telemetry |
| `DOTNET_SKIP_FIRST_TIME_EXPERIENCE` | `true` | Skip first-run experience |

### Job Steps (Sequential)

| Step | Action/Command | Notes |
|------|---------------|-------|
| 1. Checkout | `actions/checkout@v4` | Standard checkout |
| 2. Setup .NET | `actions/setup-dotnet@v4` | Version: `10.x` |
| 3. Cache NuGet | `actions/cache@v4` | Shared cache with CI |
| 4. Init CodeQL | `github/codeql-action/init@v3` | Language: `csharp` |
| 5. Restore | `dotnet restore Rentier.slnx` | Full solution restore |
| 6. Build | `dotnet build Rentier.slnx -c Release --no-restore` | Manual build (not autobuild) |
| 7. Analyze | `github/codeql-action/analyze@v3` | Upload SARIF results |

## Entity Impact

- **Domain entities**: None affected
- **Application DTOs/Commands**: None affected
- **Infrastructure schemas**: None affected
- **Database migrations**: None required

## State Transitions

Not applicable — this feature has no stateful entities. The workflow is stateless and runs to completion on each trigger.
