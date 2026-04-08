# Specification Analysis Report — 010 IMAP Email Sync

**Generated**: 2026-04-07  
**Scope**: `clarify.md`, `spec.md`, `plan.md`, `data-model.md`, `tasks.md`, `contracts/sync-service.md`  
**Constitution**: `.specify/memory/constitution.md` v1.0.0  
**Source state verified**: master branch — zero feature code implemented  

---

## Findings Table

| ID | Category | Severity | Location(s) | Summary | Recommendation |
|----|----------|----------|-------------|---------|----------------|
| C1 | Inconsistency | **CRITICAL** | `plan.md` §Implementation Status | plan.md marks all Domain, Application, and Infrastructure components as ✅ complete (e.g., `ImapMailboxSyncService.cs ✅`, `ReportConfiguration.cs ✅`), but actual master-branch source has zero feature implementation — no `ReportStatus`, no `SyncMailboxCommand`, no `ImapMailboxSyncService`, no migration 0007, no `ReportRepository`, etc. tasks.md correctly states "nothing implemented yet." | Correct plan.md Implementation Status table — change all ✅ to ⬜ for the components still to be built. The plan was written speculatively. tasks.md is the authoritative work-order and is accurate. |
| H1 | Inconsistency | **HIGH** | `spec.md:L47` vs. `plan.md`, `tasks.md:T017`, `contracts/sync-service.md` | spec.md defines `IMailboxSyncService.SyncAsync` returning `Task<Result<MailboxSyncResult, Error>>`, but every other artifact uses `SyncResult` (not `MailboxSyncResult`). T015 creates `SyncResult`, T017/T018/T023 all reference `SyncResult`. If an implementor follows spec.md literally they produce a type `MailboxSyncResult` that won't match the rest of the codebase. | Update spec.md line 47 to read `Task<Result<SyncResult, Error>>`. `MailboxSyncResult` is a stale name. |
| H2 | Coverage Gap | **HIGH** | `tasks.md` Phase 1 (T001–T005) | No task removes or makes `private` the existing public `Report(Guid id, DateOnly importDate, Guid importerId)` constructor. After T005 adds `Report.Create(...)`, this old constructor remains as a public escape hatch that bypasses factory validation (empty-name guard, 500-char guard, Status=Init, DateOnly.UtcNow). Any caller using `new Report(id, date, importerId)` gets an object with null `ReportName`, null `Status`. | Add to T002 (or as T002a): "Make the existing public `Report(Guid, DateOnly, Guid)` constructor private (EF parameterless constructor already added). All external instantiation MUST use `Report.Create()`." |
| H3 | Coverage Gap | **HIGH** | `tasks.md:T029`, `tests/Rentier.Application.Tests/DiRegistrationSmokeTests.cs` | T029 says "Confirm DiRegistrationSmokeTests still green after new DI registrations." The existing test only asserts resolution of the 7 *pre-existing* interfaces. It does **not** assert `IMailboxSyncService` or `ICommandHandler<SyncMailboxCommand, Result<SyncResult, Error>>` — both newly registered by T011/T022. T029 will pass trivially (old bindings unchanged) but the new bindings will be untested by this gate. | Expand T029 to include: "Add assertions to `DiRegistrationSmokeTests` for `IReportRepository` (re-confirm), `IMailboxSyncService`, and `ICommandHandler<SyncMailboxCommand, Result<SyncResult, Error>>`." |
| M1 | Underspecification | **MEDIUM** | `spec.md` (missing), `plan.md` §ImapMailboxSyncServiceTests | `AttachmentRegex = ""` (empty string) behavior — "skip all attachments" — is defined *only* in plan.md test names (`SyncAsync_EmptyAttachmentRegex_NoAttachmentsReturned`) and T021 task prose. spec.md says "download attachments matching `Importer.AttachmentRegex`" but does not define empty-regex semantics. Empty regex matches everything in .NET `Regex`; the intended behavior (skip) must be explicitly stated. | Add to spec.md §ImapMailboxSyncService: "If `AttachmentRegex` is empty or whitespace, no attachments are extracted for that importer." |
| M2 | Ambiguity | **MEDIUM** | `contracts/sync-service.md` §Progress Reporting, `tasks.md:T025–T026` | `SyncProgress.IsComplete` and `Total` are defined per-importer (contract: "total UIDs matching *this importer*"). In a mailbox with N importers, `IsComplete=true` fires N times. The Desktop task (T025) wires progress to `[Reactive] public int ProgressValue` but does not specify how to handle multiple completion events. The progress bar would reset and re-complete N times per sync. | Clarify in contracts/sync-service.md and T025 whether progress is per-importer (current) or aggregated across all importers for the mailbox. Consider a single aggregated `Total` = sum of all importer UIDs. |
| M3 | Constitution Alignment | **MEDIUM** | `plan.md:L3`, `tasks.md:L4` | Branch name `feature/010-011-sync-pipeline` does not follow constitution Principle (Git Workflow): `feature/TASK-XXX-short-name`. The branch name combines two feature numbers and uses a descriptive suffix but not the mandated `TASK-XXX` format. | Rename branch to `feature/010-imap-email-sync` (or per team convention). Update plan.md and tasks.md headers accordingly. |
| M4 | Architecture | **MEDIUM** | `tasks.md:T022`, `contracts/sync-service.md` §DI Registration | T022 registers `SyncMailboxCommandHandler` (an Application-layer class) from `InfrastructureServiceExtensions` (Infrastructure-layer method). While technically legal (Infrastructure → Application is an allowed reference), the pattern is inconsistent with how other Application handlers appear to be registered (likely from `Desktop/CompositionRoot.cs`). This leaks Application handler registration into the Infrastructure project. | Evaluate registering `SyncMailboxCommandHandler` from `CompositionRoot.cs` (Desktop) alongside other handlers. Only infrastructure-specific bindings (`ImapMailboxSyncService`, `ReportRepository`) belong in `InfrastructureServiceExtensions`. |
| M5 | Underspecification | **MEDIUM** | `spec.md` §ReportName format, `clarify.md` item 8 | ReportName truncation to 500 chars is specified in clarify.md and carried into tasks.md, but neither document states *which end* is truncated. `"{subject}_{filename}".Substring(0, 500)` (truncate tail) vs. `TakeLast(500)` are both plausible interpretations. A non-unique truncation result (two different attachments sharing same 500-char prefix) could still cause UNIQUE index collisions. | Specify in spec.md: "Truncate from the **end** (take first 500 chars). If truncation would produce a duplicate `ReportName` for the same `ImporterId`, log and skip the attachment." |
| L1 | Inconsistency | **LOW** | `plan.md` §Documentation file tree (L63) | plan.md lists `research.md` in the spec directory file tree but immediately notes "No `research.md` stub required." The file does not exist. | Remove `research.md` from the plan.md file tree entry to avoid confusion. |
| L2 | Inconsistency | **LOW** | `contracts/sync-service.md` §Pre-conditions, row 3 | Pre-condition 3 states "`importers` must be non-null and non-empty" as a valid precondition, but the "Violation behaviour" column says "Implementation silently returns `Success(0, [])` on empty list." These contradict each other: non-empty is listed as a precondition but empty is gracefully handled. | Either remove the pre-condition (empty list is supported) or define the violation as `ArgumentException`. Current behavior (silent success on empty) is fine — just document it consistently. |
| L3 | Redundancy | **LOW** | `contracts/sync-service.md` §IMAP Search Strategy, `spec.md:L66` | The search query uses `mailbox.Cursor.LastSyncDate ?? mailbox.InitialSyncDate`. However, `Mailbox.Create()` always initializes `Cursor = new MailboxCursor(LastSyncDate: initialSyncDate, LastUid: null)` — so `LastSyncDate` is never null for a newly created mailbox. The `??` fallback is unreachable dead code in the implementation. | Simplify to `mailbox.Cursor.LastSyncDate` (non-null guaranteed by `Mailbox.Create`). Add a comment noting the invariant. |

---

## Coverage Summary Table

| Requirement (spec.md section) | Has Task(s)? | Task IDs | Notes |
|-------------------------------|:------------:|----------|-------|
| `ReportStatus` enum | ✅ | T001 | — |
| `Report` entity enrichment (Status, ReportName, AttachmentContent, MailboxMessageId) | ✅ | T002–T005 | **Gap**: existing public constructor not addressed — see H2 |
| EF Migration 0007 (`Reports` table + UNIQUE index + FK) | ✅ | T006–T008 | — |
| `SyncProgress` record | ✅ | T014 | — |
| `SyncResult` record | ✅ | T015 | spec.md erroneously calls it `MailboxSyncResult` — see H1 |
| `SyncMailboxCommand` record | ✅ | T016 | — |
| `IMailboxSyncService` interface | ✅ | T017 | — |
| `SyncMailboxCommandHandler` | ✅ | T018 | — |
| `IReportRepository` additions (GetByStatus, ExistsByName, UpdateAsync) | ✅ | T009 | — |
| `ReportRepository` (full CRUD + new methods) | ✅ | T010 | — |
| `ReportRepository` DI registration | ✅ | T011 | — |
| `ImapMailboxSyncService` (connect, search, dedup, cursor, progress) | ✅ | T021 | — |
| `IMailboxSyncService` + `SyncMailboxCommandHandler` DI registration | ✅ | T022 | Architecture concern — see M4 |
| `ReportsViewModel` sync trigger + progress UX | ✅ | T025 | — |
| Unit tests: `ReportTests` | ✅ | T012 | — |
| Unit tests: `SyncMailboxCommandHandlerTests` | ✅ | T019 | — |
| Integration tests: `ReportRepositoryTests` | ✅ | T013 | — |
| Unit tests: `ImapMailboxSyncServiceTests` | ✅ | T023 | — |
| Placeholder: `ImapSyncIntegrationTests` | ✅ | T024 | — |
| Unit tests: `ReportsViewModelTests` | ✅ | T026 | — |
| Build/CI validation | ✅ | T027–T030 | T029 gap — see H3 |
| `AttachmentRegex = ""` → skip all attachments (business rule) | ⚠️ | T021 (implicit) | **Not in spec.md** — see M1 |
| ReportName truncation direction | ⚠️ | T021 (implicit) | **Not specified** — see M5 |

---

## Constitution Alignment Issues

| Principle | Status | Finding |
|-----------|--------|---------|
| I. Clean Architecture | ⚠️ Minor | `SyncMailboxCommandHandler` (Application) registered from `InfrastructureServiceExtensions` — see M4. Not a hard violation (Infrastructure→Application is allowed), but inconsistent with project patterns. |
| II. Local-First Security | ✅ | Passwords exclusively via `ICredentialStore`. Key format matches convention. No SQLite storage. |
| III. Financial/Temporal Correctness | ✅ | `ImportDate` uses `DateOnly`. No monetary values. Datetime boundary conversion in Infrastructure only (`ToDateTime(TimeOnly.MinValue)`). |
| IV. Async/UI Responsiveness | ✅ | All I/O is `async Task`. `CancellationToken` threaded throughout. `ReactiveCommand.CreateFromTask` mandated for Desktop (T025). `RxApp.MainThreadScheduler` specified. |
| V. Quality Gates | ⚠️ Minor | T029 does not validate new DI registrations — see H3. Domain 100% rule coverage target is met by T012 test plan. Application ≥90% coverage target addressed by T018/T019. |
| Git Workflow | ⚠️ | Branch name `feature/010-011-sync-pipeline` deviates from `feature/TASK-XXX-short-name` convention — see M3. |

**No MUST-level constitution violations detected.**

---

## Unmapped Tasks

All 30 tasks (T001–T030) map to at least one spec requirement, user story, or quality gate. No orphan tasks detected.

---

## Architecture Rule Spot-Check

| Rule | Spec/Tasks Compliance | Notes |
|------|----------------------|-------|
| `AddTransient` only (no Singleton/Scoped for repos/services) | ✅ | T011 and T022 both specify `AddTransient`. Existing registrations in `InfrastructureServiceExtensions.cs` all use `AddTransient`. |
| `DateOnly` for all business dates | ✅ | `ImportDate: DateOnly`, `InitialSyncDate: DateOnly`, `MailboxCursor.LastSyncDate: DateOnly?`. Datetime conversion at boundary only (Infrastructure, T021). |
| `decimal` for money | ✅ (N/A) | No monetary values in this feature. |
| `Result<T, Error>` pattern | ✅ | `SyncMailboxCommandHandler` → `Result<SyncResult, Error>`. `IMailboxSyncService.SyncAsync` → `Result<SyncResult, Error>`. No raw exceptions thrown at handler level. |
| No `ExecuteDeleteAsync` | ✅ | T010 specifies `FindAsync + Remove` for deletions. No bulk-delete pattern used. |
| `async`/`CancellationToken` throughout | ✅ | All new repository methods include `CancellationToken ct = default`. T021 passes `ct` to `ConnectAsync`, `AuthenticateAsync`, `AddAsync`, `UpdateAsync`. `OperationCanceledException` propagates uncaught (per contract). |
| No `.Result` / `.Wait()` | ✅ | No blocking async calls specified or implied in any task. |
| `sealed` on concrete classes | ✅ | spec.md uses `sealed record` for DTOs. T025 `ReportsViewModel` uses `sealed class`. |

---

## Metrics

| Metric | Value |
|--------|-------|
| Total spec requirements mapped | 20 |
| Total tasks | 30 |
| Requirements with ≥1 task | 18 / 20 (90%) |
| Requirements with coverage gaps | 2 (AttachmentRegex empty behavior, truncation direction — M1, M5) |
| Ambiguity findings | 2 (M2, M5) |
| Duplication findings | 0 |
| Critical issues | 1 (C1) |
| High issues | 3 (H1, H2, H3) |
| Medium issues | 5 (M1–M5) |
| Low issues | 3 (L1–L3) |
| Constitution MUST violations | 0 |
| Unmapped tasks | 0 |

---

## Next Actions

### Before running `/speckit.implement`

**C1 must be resolved** (or explicitly acknowledged):  
- plan.md Implementation Status section will mislead any agent or implementor that reads it and assumes core implementation exists. All ✅ items should be ⬜ or the section should note "speculative/pre-written; tasks.md is authoritative."

**H1 must be resolved** before writing any code:  
- Fix spec.md `MailboxSyncResult` → `SyncResult` (one-line change). Otherwise the first code generation from spec.md will produce a type that doesn't compile with the rest.

**H2 must be resolved** before T005 is executed:  
- Add a sub-step to T002: make the existing public constructor private. Otherwise `Report.Create()` factory validation is bypassed.

### Recommended pre-implement edits

```
/speckit.specify  — pass "Fix: rename MailboxSyncResult → SyncResult in IMailboxSyncService signature (H1); 
                          add AttachmentRegex empty-string behaviour rule (M1); 
                          specify ReportName truncation direction (M5)"

Manual edit tasks.md T002  — add sub-step: make Report's existing public constructor private
Manual edit tasks.md T029  — add sub-step: extend DiRegistrationSmokeTests with IMailboxSyncService + SyncMailboxCommandHandler assertions
Manual edit plan.md §Implementation Status — change all ✅ to ⬜ (C1)
```

### May proceed with caution (LOW/MEDIUM only remaining)

Once C1, H1, H2, H3 are addressed, all remaining findings are LOW or MEDIUM and do not block implementation correctness. The architecture is sound, the task ordering is correct, and the test plan is comprehensive.

---

## Remediation Offer

Would you like me to suggest concrete remediation edits for the top findings (C1, H1, H2, H3)?  
- **C1**: Specific line edits to `plan.md` Implementation Status table  
- **H1**: Single-line fix to `spec.md` L47  
- **H2**: New sub-step text to insert into `tasks.md` T002  
- **H3**: Replacement text for `tasks.md` T029  

Reply "yes, remediate top findings" to proceed.
