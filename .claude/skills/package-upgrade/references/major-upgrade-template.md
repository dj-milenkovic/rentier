# Major Upgrade Presentation & Approval Flow

Use this template whenever one or more major-version upgrades are available. Present
**all** pending majors in one message so the user decides once, then wait for their
answer before executing anything.

## Research first

For each major upgrade, gather (WebFetch/WebSearch the package's GitHub releases and
official docs — don't rely on memory):

1. Changelog / release notes for the new major
2. The specific breaking changes
3. The official migration guide URL
4. Which files in this repo are affected (grep for the package's namespaces/APIs)
5. Notable new features worth having

## Presentation template

```markdown
## Major Upgrade: [Package] [old] → [new]

### Package information
- Current version: X.Y.Z
- Latest stable: A.B.C (released [date])

### What's new
- [features / performance improvements worth caring about]

### Breaking changes
1. [change] — impact: [which Rentier code is affected]
2. [change] — impact: …

### Migration effort
- Estimated files affected: [n] ([list the concrete files found by grep])
- Complexity: Low / Medium / High
- Code changes required: yes/no + one-line description

### Resources
- Release notes: [URL]
- Migration guide: [URL]

### Testing required
- [ ] Unit + Application tests (`dotnet test Rentier.slnx --filter "Category!=Integration"`)
- [ ] Integration tests (`dotnet test tests/Rentier.Infrastructure.Tests --filter "Category=Integration"`)
- [ ] Manual smoke of affected features

### Recommendation
[Upgrade now / delay because … / skip this version because …]
```

End with: **"Which major upgrades do you want to proceed with?"** and offer
`all` / specific package names / `skip`.

## Interpreting the answer

- `yes` / `proceed` / `all` → execute all presented majors
- package name(s) → execute only those
- `no` / `skip` → record as skipped; do not silently retry later in the session
- `more info` → deepen the research on the named package

## Execution after approval

1. Branch: `upgrade/<package>-to-<version>` (one family per branch keeps reverts cheap)
2. Edit `Directory.Packages.props` (move the whole version-locked family together)
3. `dotnet restore Rentier.slnx && dotnet build Rentier.slnx --no-restore -c Release`
4. Fix breaking changes per the migration guide — smallest possible diffs
5. Full verification (all four commands in SKILL.md §4)
6. Report exactly what changed and what the tests showed
