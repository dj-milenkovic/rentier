# Task 1: CI — Integration-Tests Job + Stryker Workflow

## Context
Rentier is a C#/Avalonia desktop app for Serbian PP-OPO tax filing. It uses GitHub Actions CI.
The plan identified that ~30 integration test files never run in CI — the single worst defect.
This task fixes that by adding a dedicated integration-tests job, and adds weekly mutation testing.

## Files to modify
- `.github/workflows/ci.yml` — add `integration-tests` job; wire its coverage; add Domain+Application coverage ratchet
- `.github/workflows/stryker.yml` — NEW: weekly cron + `workflow_dispatch`
- `stryker-config.json` — NEW: target Rentier.Domain, break threshold 85

## Requirements (verbatim from plan §Part 3 + §2.5 + §Part 4 step 1)

### ci.yml changes

**Add `integration-tests` job** (parallel to `build`, both need `lint`):
```yaml
integration-tests:
  name: Integration & Scenario Tests
  needs: lint
  runs-on: ubuntu-latest
  steps:
    - Checkout, Setup .NET, Cache NuGet, Restore (all same as other jobs)
    - Build: dotnet build tests/Rentier.Infrastructure.Tests tests/Rentier.Scenarios.Tests --no-restore -c Release
    - Run: dotnet test tests/Rentier.Infrastructure.Tests tests/Rentier.Scenarios.Tests
        --no-build -c Release
        --filter "Category=Integration"
        --collect:"XPlat Code Coverage"
        --settings coverlet.runsettings
        --results-directory ./coverage-integration
        --logger "trx;LogFileName=integration-results.trx"
        --logger "console;verbosity=normal"
    - Upload coverage artifact: name=coverage-integration, path=./coverage-integration/**/coverage.opencover.xml
    - Upload test results artifact: name=integration-test-results
```

**Delete** the existing `migration-tests` job entirely (it is folded into `integration-tests` as the first subset — the filter `Category=Integration` already covers all migrations plus everything else).

**Update `coverage` job**:
- Change `needs: build` → `needs: [build, integration-tests]`
- The download step already uses `pattern: coverage-*` which will automatically pick up `coverage-integration`

**Add coverage ratchet step** to the `coverage` job (after the merge & generate report step):
```yaml
- name: Coverage ratchet — Domain + Application
  shell: bash
  run: |
    # Extract line coverage for Domain and Application assemblies only
    COV=$(grep -A2 'assembly.*Rentier\.\(Domain\|Application\)' ./coverage-report/Summary.txt \
          | grep -oP 'Line coverage: \K[0-9.]+' | awk '{sum+=$1; n++} END{if(n>0) print sum/n; else print 0}')
    echo "Domain+Application line coverage: ${COV}%"
    # Ratchet: must be >= (last known - 2pt floor). Currently store baseline in env.
    # If COVERAGE_FLOOR env not set, just report (first run establishes baseline).
    if [ -n "$COVERAGE_FLOOR" ]; then
      python3 -c "import sys; cov=float('${COV}' or 0); floor=float('${COVERAGE_FLOOR}'); sys.exit(1 if cov < floor else 0)"
    fi
```
  (This is a best-effort ratchet; exact implementation may vary — the key is it reports and can gate on Domain+Application coverage specifically.)

### stryker-config.json (repo root)
```json
{
  "stryker-config": {
    "project": "src/Rentier.Domain/Rentier.Domain.csproj",
    "test-projects": ["tests/Rentier.UnitTests/Rentier.UnitTests.csproj"],
    "target-framework": "net10.0",
    "reporters": ["html", "progress", "dashboard"],
    "mutation-level": "Standard",
    "thresholds": {
      "high": 90,
      "low": 85,
      "break": 85
    },
    "output-path": "mutation-report",
    "since": {
      "enabled": false
    }
  }
}
```

### .github/workflows/stryker.yml (new file)
```yaml
name: Mutation Testing (Stryker.NET)

on:
  schedule:
    - cron: '0 3 * * 1'   # Monday 03:00 UTC
  workflow_dispatch:

concurrency:
  group: stryker
  cancel-in-progress: true

env:
  DOTNET_NOLOGO: true
  DOTNET_CLI_TELEMETRY_OPTOUT: true
  DOTNET_SKIP_FIRST_TIME_EXPERIENCE: true

jobs:
  mutation:
    name: Stryker.NET — Rentier.Domain
    runs-on: ubuntu-latest
    steps:
      - name: Checkout
        uses: actions/checkout@v6.0.3

      - name: Setup .NET
        uses: actions/setup-dotnet@v5.3.0
        with:
          dotnet-version: '10.x'

      - name: Cache NuGet packages
        uses: actions/cache@v5.0.5
        with:
          path: ~/.nuget/packages
          key: nuget-${{ runner.os }}-${{ hashFiles('**/*.csproj', 'Directory.Build.props', 'Directory.Packages.props') }}
          restore-keys: nuget-${{ runner.os }}-

      - name: Restore
        run: dotnet restore Rentier.slnx

      - name: Install dotnet-stryker
        run: dotnet tool install -g dotnet-stryker

      - name: Run Stryker
        run: dotnet stryker --config-file stryker-config.json

      - name: Upload mutation report
        if: always()
        uses: actions/upload-artifact@v7.0.1
        with:
          name: mutation-report
          path: mutation-report/
```

## Constraints
- Use the same action versions already in ci.yml (`actions/checkout@v6.0.3`, `actions/setup-dotnet@v5.3.0`, `actions/cache@v5.0.5`, `actions/upload-artifact@v7.0.1`, `actions/download-artifact@v8.0.1`)
- Keep all existing jobs untouched except: delete `migration-tests`, update `coverage` needs + add ratchet step
- No tests to write for this task — it is pure config changes
- Run `dotnet format Rentier.slnx --no-restore --verify-no-changes` to confirm nothing breaks (these are yaml/json changes, not C# — format won't complain, but run it anyway to confirm)
- YAML must be valid — check indentation carefully (2-space throughout)

## Verification
After making changes:
1. `cat .github/workflows/ci.yml` — confirm `migration-tests` job is gone, `integration-tests` is present, `coverage` needs both `build` and `integration-tests`
2. `cat .github/workflows/stryker.yml` — confirm weekly cron
3. `cat stryker-config.json` — confirm break threshold 85
4. `python3 -c "import yaml; yaml.safe_load(open('.github/workflows/ci.yml'))"` — validate YAML

## Commit
Commit with message: `ci: add integration-tests job, stryker weekly workflow, coverage ratchet`
