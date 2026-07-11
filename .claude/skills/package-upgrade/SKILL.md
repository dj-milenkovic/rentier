---
name: package-upgrade
description: >
  Upgrade NuGet packages safely with semantic-versioning awareness (patch/minor/major
  risk tiers) in a repo using Central Package Management. Make sure to use this skill
  whenever upgrading dependencies, addressing a security vulnerability or CVE in a
  package, responding to the CI vulnerable-package gate failing, modernizing package
  versions, or resolving NuGet version conflicts — even if the user just says "update
  packages" or "fix the Dependabot warning".
---

# NuGet Package Upgrade

Guides safe, strategic NuGet upgrades. The core idea: **risk scales with the SemVer
position that changed**, so patch upgrades are routine, minor upgrades need a
changelog glance, and major upgrades need the user's explicit go-ahead because they
can break code and consume real migration time.

## Critical rules

1. **Never upgrade to pre-release versions** (`-alpha`, `-beta`, `-rc`, `-preview`)
   without explicit approval — they can regress and churn APIs between builds.
2. **Never execute a major-version upgrade without user confirmation.** Present
   breaking changes and effort first (template in
   `references/major-upgrade-template.md`).
3. **Categorize every available upgrade by risk tier before touching anything**, so
   the user sees the whole picture once instead of piecemeal.

| Version change | Risk | Action |
|---|---|---|
| Patch `1.2.3 → 1.2.4` | Low | Upgrade, run unit tests |
| Minor `1.2.3 → 1.3.0` | Medium | Review changelog, upgrade, run full tests |
| Major `1.2.3 → 2.0.0` | High | Present breaking changes, **wait for confirmation** |
| Pre-release | Avoid | Explicit approval only |

## Rentier specifics (read before upgrading anything here)

- **Central Package Management**: every version lives in `Directory.Packages.props`
  as a `<PackageVersion>` entry; `.csproj` files hold only unversioned
  `<PackageReference>`s. Upgrade by editing `Directory.Packages.props` — one place,
  one diff.
- **Version-locked families must move together**: `Avalonia*` packages,
  `Microsoft.EntityFrameworkCore.*`, and `xunit.v3`/`xunit.runner.visualstudio`.
  Upgrading one member alone produces runtime or restore breakage.
- **CI has a vulnerable-package gate** (fails the lint job). Reproduce locally:
  ```bash
  dotnet list Rentier.slnx package --vulnerable --include-transitive
  ```
- A transitive-only vulnerability is fixed by pinning the transitive package as a
  direct `PackageVersion` at the patched version.

## Workflow

### 1. Discover

```bash
dotnet restore Rentier.slnx
dotnet list Rentier.slnx package --outdated
dotnet list Rentier.slnx package --vulnerable --include-transitive
```

### 2. Categorize

Sort every available upgrade into patch / minor / major / pre-release. Present the
full table to the user with your plan: which ones you'll do now, which need their
confirmation, which you'll skip.

### 3. Execute (per tier)

- **Patch + minor**: edit `Directory.Packages.props`, then
  `dotnet restore Rentier.slnx && dotnet build Rentier.slnx --no-restore -c Release`
  and `dotnet test Rentier.slnx --filter "Category!=Integration"`. For minors, skim
  the release notes first and mention anything notable to the user.
- **Major**: present with `references/major-upgrade-template.md`, wait for
  confirmation, then upgrade one package (family) at a time on a branch, fixing
  breaking changes per the package's migration guide. Run integration tests too:
  `dotnet test tests/Rentier.Infrastructure.Tests --filter "Category=Integration"`.
- **Security fixes**: see `references/upgrade-scenarios.md` — patch-tier fixes go
  immediately; a fix only available in a major version still needs confirmation,
  presented as an emergency.

### 4. Verify before claiming done

```bash
dotnet build Rentier.slnx --no-restore -c Release
dotnet test Rentier.slnx --filter "Category!=Integration"
dotnet format Rentier.slnx --no-restore --verify-no-changes
dotnet list Rentier.slnx package --vulnerable --include-transitive
```

## References

- `references/major-upgrade-template.md` — the exact presentation format for major
  upgrades and the approval flow. Read it whenever a major upgrade is on the table.
- `references/upgrade-scenarios.md` — worked playbooks: security CVE response,
  feature-driven upgrade, routine maintenance sweep, and handling breaking changes
  after a major bump.
