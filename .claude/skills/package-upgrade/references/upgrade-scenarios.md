# Upgrade Scenario Playbooks

## Scenario 1: Security vulnerability (CVE / CI gate failure)

The CI lint job fails on `dotnet list … --vulnerable`, or the user reports a CVE.

1. Identify the vulnerable package and the **lowest patched version**:
   ```bash
   dotnet list Rentier.slnx package --vulnerable --include-transitive
   ```
2. **Fix is a patch/minor away** → upgrade immediately in `Directory.Packages.props`,
   run tests, report. No confirmation needed — shipping a known CVE is worse than a
   low-risk bump.
3. **Vulnerable package is transitive-only** → add it as a direct `<PackageVersion>`
   pin at the patched version (this overrides the transitive resolution).
4. **Fix requires a major version** → still requires user confirmation, but present
   it as an emergency:

   ```markdown
   SECURITY: [CVE-id] in [package] [version]
   Severity: [CVSS]
   Fixed in: [version] (MAJOR upgrade)
   Breaking changes: [list]
   Recommend upgrading despite the major bump. Proceed? (yes/no)
   ```

## Scenario 2: Feature-driven upgrade

The user wants a capability that ships in a newer package version.

- Feature in a **minor** → upgrade, cite the release notes, test.
- Feature in a **major** → present via `major-upgrade-template.md`, and offer the
  alternative of implementing the need without the upgrade if that's cheap.

## Scenario 3: Routine maintenance sweep

"Update the packages" with no specific driver:

1. `dotnet list Rentier.slnx package --outdated`
2. Apply all **patch** upgrades in one `Directory.Packages.props` edit; build + unit tests.
3. Apply **minor** upgrades next (family-by-family for Avalonia/EF Core/xUnit), skim
   release notes, full tests.
4. Present all **majors** at once via the template; execute only what's approved.
5. Skip pre-releases silently unless one is the only fix for something.

Report at the end with a table: package, old → new, tier, test outcome.

## Handling breaking changes after a major bump

1. `dotnet build Rentier.slnx --no-restore -c Release` — compilation errors and
   obsolete-API warnings are the map of what the migration touched.
2. Read the official migration guide before "fixing" errors by intuition — renamed
   APIs often changed semantics too, and this codebase treats financial behavior
   changes as safety-critical.
3. Fix one project at a time following the dependency order: Domain → Application →
   Infrastructure → Desktop → tests.
4. If tests fail after compilation is clean, diff behavior against the old package's
   documented semantics — do not adjust test expectations to make failures go away
   without understanding why the value changed (especially anything `decimal`).
5. If the migration turns out much larger than presented, stop and re-present the
   real effort to the user instead of pushing through.
