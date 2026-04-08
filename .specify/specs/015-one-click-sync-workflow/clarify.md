# Clarification Record — 015 One-Click Sync Workflow

**Date**: 2025-07-14
**Spec**: `.specify/specs/015-one-click-sync-workflow/spec.md`
**Questions asked**: 5 / 5
**Status**: Complete — proceed to `/speckit.plan`

---

## Taxonomy Coverage Map

| Category | Status | Disposition |
|---|---|---|
| Functional Scope & Behavior | Clear | All user goals, out-of-scope, and edge cases defined |
| Domain & Data Model | **Resolved** | Q1 & Q2: exact record shapes for `SyncAllCommand`, `SyncAllResult`, `SyncProgressEntry` |
| Interaction & UX Flow | Clear | Auto-navigation rules, cancel behaviour, empty-state handling all specified |
| Non-Functional Quality Attributes | Clear | SC-002 (2 s first entry), SC-004 (3 s cancel), SC-005 (1 s nav), async constraint defined |
| Integration & External Dependencies | **Resolved** | Q3 & Q4: `ISyncAllCommandHandler`, `SyncMailboxCommand` progress-via-constructor pattern |
| Edge Cases & Failure Handling | Clear | Network drop, single-report failure, zero-config case, app-close all addressed |
| Constraints & Tradeoffs | **Resolved** | Q5: no EF migration; Q3: dedicated interface not `ICommandHandler<TCmd,TResult>` |
| Terminology & Consistency | Clear | `SyncProgressEntry` vs `SyncProgress` distinction explicit; `SyncAllResult` vs `SyncResult` distinct |
| Completion Signals | Clear | FR-010/FR-011 measurable; SC-001…SC-008 testable |
| Misc / Placeholders | **Resolved** | No TODOs or vague adjectives remain after clarifications |

---

## Questions & Answers

### Q1 — `SyncAllCommand` and `SyncAllResult` exact shapes

**Question**: What are the exact C# record definitions for `SyncAllCommand` and the new `SyncAllResult` DTO?

**Answer**:
```csharp
// src/Rentier.Application/Commands/SyncAllCommand.cs
public sealed record SyncAllCommand();

// src/Rentier.Application/DTOs/SyncAllResult.cs
public sealed record SyncAllResult(
    int MailboxesSynced,
    int AttachmentsDownloaded,
    int ReportsProcessed,
    int FilingsCreated,
    IReadOnlyList<string> Errors);
```

**Why it matters**: Determines the boundary type between `SyncAllCommandHandler` and `SyncViewModel`. `MailboxesSynced` and `AttachmentsDownloaded` come from `SyncResult`; `ReportsProcessed` and `FilingsCreated` come from `ProcessReportsResult`. Without this, the aggregation logic is ambiguous.

**Spec sections updated**: `## Clarifications`, `### Key Entities`, `## Assumptions`

---

### Q2 — `SyncProgressEntry` DTO shape

**Question**: What is the exact shape of the new progress DTO, and how does it differ from the existing `SyncProgress`?

**Answer**:
```csharp
// src/Rentier.Application/DTOs/SyncProgressEntry.cs  (NEW)
public sealed record SyncProgressEntry(
    DateTimeOffset Timestamp,
    string Message,
    SyncProgressSeverity Severity);

public enum SyncProgressSeverity { Info, Warning, Error }

// src/Rentier.Application/DTOs/SyncProgress.cs  (UNCHANGED)
// public sealed record SyncProgress(int Total, int Processed, string? CurrentFile, bool IsComplete);
```

**Why it matters**: The existing `SyncProgress` is a counter-style DTO (Total/Processed/CurrentFile) not suitable as a log entry. A separate named type removes ambiguity, prevents accidental modification of the existing DTO, and directly maps to the severity-icon UI requirement in FR-004.

**Spec sections updated**: `## Clarifications`, `### Key Entities`

---

### Q3 — `ISyncAllCommandHandler` interface

**Question**: What interface does `SyncAllCommandHandler` implement, and how does it pass progress into the existing `SyncMailboxCommandHandler`?

**Answer**:
```csharp
// Dedicated interface — NOT ICommandHandler<TCmd, TResult>
public interface ISyncAllCommandHandler
{
    Task<Result<SyncAllResult, Error>> HandleAsync(
        SyncAllCommand command,
        IProgress<SyncProgressEntry> progress,
        CancellationToken ct = default);
}
```

Orchestration pattern inside `SyncAllCommandHandler.HandleAsync`:
```csharp
// Bridge SyncProgress → SyncProgressEntry
var internalProgress = new Progress<SyncProgress>(p =>
{
    var entry = new SyncProgressEntry(
        DateTimeOffset.Now,
        p.CurrentFile ?? $"Processing {p.Processed}/{p.Total}",
        SyncProgressSeverity.Info);
    progress.Report(entry);
});

var syncResult = await _syncMailboxHandler.HandleAsync(
    new SyncMailboxCommand(internalProgress), ct);

var processResult = await _processReportsHandler.HandleAsync(
    new ProcessReportsCommand(), ct);
```

**Why it matters**: The standard `ICommandHandler<TCmd, TResult>` interface signature is `HandleAsync(TCmd command, CancellationToken ct)`. It cannot carry `IProgress<SyncProgressEntry>`. Without a dedicated interface the handler signature is undefined, and DI registration in `CompositionRoot` is ambiguous. Also confirms `SyncMailboxCommand` receives progress via its constructor, not a method argument.

**Spec sections updated**: `## Clarifications`, `### Key Entities` (`ISyncAllCommandHandler`), `### Functional Requirements` (FR-014), `### Constitution Alignment` (CA-001)

---

### Q4 — Desktop navigation wiring

**Question**: How is `SyncViewModel` wired in `MainWindowViewModel`, and what delegate does it receive for auto-navigation?

**Answer**:
- `SyncViewModel` is created via `ActivatorUtilities.CreateInstance<SyncViewModel>(provider, navigateToFilings)` — same pattern as `ReportsViewModel`.
- `navigateToFilings` is `Action` (not `Action<Guid>` — no report-ID filter required for post-sync navigation).
- The delegate: sets `SelectedEntry` to the Filings `NavigationEntry`.
- `MainWindowViewModel` constructor gains a `SyncViewModel syncVm` parameter (or resolves via `ActivatorUtilities`) and inserts `new NavigationEntry(Strings.Nav_Sync, syncVm)` between Reports and Settings entries.

**Why it matters**: Determines the constructor shape of `SyncViewModel` and the changes required in `MainWindowViewModel`. Without this, the composition and DI registration in `CompositionRoot` are unknown, blocking both implementation and test design.

**Spec sections updated**: `## Clarifications`, `## Assumptions`

---

### Q5 — EF Core migration

**Question**: Does the feature require a new EF Core migration?

**Answer**: **No.** `SyncAllCommandHandler` is orchestration-only. All database writes (attachment storage, report status updates, filing creation) are performed by the existing `SyncMailboxCommandHandler` and `ProcessReportsCommandHandler`. No new tables, columns, index changes, or migration files are required.

**Why it matters**: Determines whether the implementation plan must include a `dotnet ef migrations add` step, a migration review, and a data-safety checkpoint. A wrong assumption here adds unnecessary plan steps or causes a runtime schema mismatch.

**Spec sections updated**: `## Clarifications`, `## Assumptions`

---

## Deferred Items

None. All five high-impact ambiguities were resolved within the question quota.

---

## Recommendation

All critical ambiguities are resolved. Proceed to:

```
/speckit.plan
```
