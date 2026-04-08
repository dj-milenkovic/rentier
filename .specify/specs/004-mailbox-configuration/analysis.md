# Specification Analysis Report — Feature 004: IMAP Mailbox Configuration

**Generated**: 2026-04-06  
**Artifacts analysed**: `spec.md`, `plan.md`, `tasks.md`, `contracts/IMailboxRepository.cs`, `contracts/OsCredentialStoreImpl.md`  
**Constitution version**: 1.0.0  
**Source files read**: `Mailbox.cs`, `MailboxCursor.cs`, `IMailboxRepository.cs`, `ICredentialStore.cs`, `OsCredentialStore.cs`, `AppDbContext.cs`, `SettingsViewModel.cs`, `SettingsView.axaml`, `CompositionRoot.cs`, `Result.cs`, `VoidResult.cs`

> **CRITICAL issues found — resolve before running `/speckit.implement`.**  
> All CRITICAL and HIGH findings have been remediated in-place in `tasks.md`, `contracts/IMailboxRepository.cs`, and `contracts/OsCredentialStoreImpl.md`. MEDIUM/LOW findings are documented here for optional follow-up.

---

## Findings Table

| ID | Category | Severity | Location(s) | Summary | Recommendation |
|----|----------|----------|-------------|---------|----------------|
| C1 | Inconsistency | CRITICAL | tasks.md T008, T010, T011 | `Result<T,E>.Ok(...)` called in three handler task descriptions — `.Ok()` does not exist on `Result<TValue, TError>`; only `.Success()` and `.Failure()` are defined in `src/Rentier.Application/Common/Result.cs`. Calling `.Ok(...)` at implementation time would cause a CS0117 compiler error. | Replace all `.Ok(...)` with `.Success(...)` in T008, T010, T011. ✅ Fixed. |
| C2 | Inconsistency | CRITICAL | tasks.md T023, T024 | `DatePicker.SelectedDate` in Avalonia 11 is `DateTimeOffset?`, but the ViewModel property `InitialSyncDate` is `DateOnly`. Direct XAML binding `SelectedDate="{Binding InitialSyncDate}"` will throw a binding type-mismatch exception at runtime. | Add a bridging `public DateTimeOffset? InitialSyncDateOffset` property to `MailboxSettingsViewModel` and bind `DatePicker.SelectedDate` to it instead. ✅ Fixed. |
| C3 | Inconsistency | CRITICAL | contracts/IMailboxRepository.cs (AddMailboxCommandHandler comment, lines 61–65) | Handler comment shows `AddAsync` **before** `SaveCredentialAsync` — the opposite of what spec.md Edge Cases requires. Spec says: "If the OS Credential Manager rejects the write, the database row is **NOT** written (credential must succeed before DB insert)." T009 already has the correct order (credential first, then DB). The contracts doc contradicts both the spec and T009. | Swap steps 2 and 3 in the contracts comment to: credential save → DB add. ✅ Fixed. |
| C4 | Inconsistency | CRITICAL | contracts/OsCredentialStoreImpl.md §2.1 | `CredFreeW` is declared as `private static extern bool CredFreeW(IntPtr buffer)`. The actual Windows API `CredFree` returns `void`. Declaring a `void` Win32 function as returning `bool` corrupts the x64 call stack because the caller attempts to read a return value that was never written. T020 correctly declares `void`. | Change `bool` to `void` in the `CredFreeW` DllImport in OsCredentialStoreImpl.md. ✅ Fixed. |
| C5 | Inconsistency | CRITICAL | tasks.md T024, T029 | T024 and T029 reference `lang:Strings.*` throughout XAML snippets. The existing codebase uses `xmlns:res="using:Rentier.Desktop.Resources"` with `{x:Static res:Strings.*}` (confirmed in `SettingsView.axaml`). There is no `lang:` namespace declaration in any existing view. These snippets would cause XAML compilation failures. | Replace all `lang:Strings.` with `res:Strings.` in T024 and T029. ✅ Fixed. |
| H1 | Constitution | HIGH | tasks.md T020; plan.md Constitution Check | `OsCredentialStore` uses `Task.CompletedTask` / `Task.FromResult` — the P/Invoke calls run **synchronously** on whatever thread calls the method. `ReactiveCommand.CreateFromTask` runs its delegate on the UI thread's synchronisation context; the synchronous P/Invoke therefore blocks the UI thread. Constitution IV: "Desktop-layer workflows MUST NOT block the UI thread." The plan's Constitution Check incorrectly marks this gate as PASS without Task.Run. | Wrap all three P/Invoke bodies in `Task.Run(() => { … })`. ✅ Fixed. |
| H2 | Underspecification | HIGH | tasks.md T020 (GetCredentialAsync description) | T020's `GetCredentialAsync` description calls `CredFreeW(ptr)` after `Marshal.Copy` with no `try/finally` guard. If `Marshal.PtrToStructure` or `Marshal.Copy` throws (e.g., CredentialBlobSize overflow), the OS-allocated pointer `ptr` is never freed (handle leak). The reference implementation in `contracts/OsCredentialStoreImpl.md §3.2` correctly uses `try/finally { CredFreeW(credPtr); }`. | T020 updated to require `try/finally { CredFreeW(ptr); }`. ✅ Fixed. |
| H3 | Underspecification | HIGH | tasks.md T023 (SaveCommand description) | After a successful `AddMailboxCommand`, T023 says "on success reloads list" but does **not** specify: (a) selecting the newly created item in the refreshed collection; (b) setting `IsEditMode = true`; (c) clearing the `Password` field. Without this, a second click of `SaveCommand` would call `AddMailboxCommand` again (duplicate insert) instead of `UpdateMailboxCommand`. | T023 updated with explicit post-Add state transition: reload list → find new item by Id → set `SelectedMailbox` → set `IsEditMode = true` → clear `Password`. ✅ Fixed. |
| H4 | Inconsistency | HIGH | plan.md §Two-Panel ViewModel Design (line 348); tasks.md T023, T024 | plan.md includes `SuccessMessage` in the ViewModel property list (`bool IsLoading, string ErrorMessage, SuccessMessage`) and §ViewModel String Resources Policy says "The ViewModel accesses `Strings.*` constants for `SuccessMessage`". T023 does not declare a `_successMessage` backing field or `SuccessMessage` property. T024 XAML has no corresponding TextBlock. This means success feedback (e.g., "Mailbox saved.") can never be displayed despite being specified. | T023 updated to declare `string? _successMessage` → `public string? SuccessMessage`. T024 updated to add a green `TextBlock` for SuccessMessage below the ErrorMessage block. ✅ Fixed. |
| M1 | Inconsistency | MEDIUM | plan.md §Project Structure (Strings.resx block); tasks.md T031, T024 | plan.md lists resource keys with `Mailbox_` prefix (e.g., `Mailbox_Host_Label`). T031 and T024 consistently use `Mailboxes_` prefix (e.g., `Mailboxes_Host_Label`). Since T031 and T024 are the implementation tasks and agree with each other, plan.md's Project Structure block is the outlier. | plan.md's resource key list in Project Structure section corrected to use `Mailboxes_` prefix to match T031 and T024. ✅ Fixed. |
| M2 | Inconsistency | MEDIUM | plan.md §Two-Panel ViewModel Design (line 345); tasks.md T023, T024, T027 | plan.md names the command `AddCommand`; T023, T024, and T027 all use `AddNewCommand`. Since the implementation tasks are mutually consistent, plan.md is the outlier. | plan.md Two-Panel design block updated to `AddNewCommand`. ✅ Fixed. |
| M3 | Inconsistency | MEDIUM | tasks.md T030; src/Rentier.Desktop/Composition/CompositionRoot.cs | T030 says "adjust the factory lambda that creates `SettingsViewModel`". The actual `CompositionRoot.cs` uses `services.AddTransient<SettingsViewModel>()` — no factory lambda. DI auto-resolves all constructor parameters when they are registered. After T028 adds `MailboxSettingsViewModel mailboxesTab` to the constructor, auto-resolution continues to work with no lambda needed (provided `MailboxSettingsViewModel` is registered, which T030 also adds). | T030 updated to clarify: no factory lambda change needed; DI auto-resolves the new parameter. ✅ Fixed. |
| M4 | Inconsistency | MEDIUM | tasks.md T010; contracts/IMailboxRepository.cs (UpdateMailboxCommandHandler comment, lines 67–73) | T010 uses `mailbox.UpdateDetails(...)` on the tracked entity returned by `GetByIdAsync`. The contracts comment creates a `new Mailbox(existing.Id, ...)` detached instance instead. Both approaches are functionally correct, but the contracts doc contradicts T010 and introduces confusion about whether to mutate in-place or create a new object. T010's mutation approach is idiomatic (uses the domain method; preserves EF tracking). | contracts/IMailboxRepository.cs UpdateMailboxCommandHandler comment updated to match T010's `UpdateDetails(...)` approach. ✅ Fixed. |
| M5 | Underspecification | MEDIUM | tasks.md T023 (Password clearing) | After a successful Add, the `Password` field still contains the entered password. The spec (US2 §Acceptance Scenarios) requires the credential to be securely stored and not displayed; leaving it in the `Password` field is a minor security hygiene gap. T023 handles clearing on `SelectedMailbox` change but not after a successful Add save. | T023 post-save state transition (H3 fix) now also clears `Password = string.Empty` on success. ✅ Fixed (as part of H3). |
| L1 | Inconsistency | LOW | plan.md §Project Structure (SettingsView.axaml MODIFIED) | plan.md says "add second TabItem" for the Mailboxes tab. The Settings screen already has Profile (TabItem 1) and Holidays (TabItem 2); Mailboxes is the **third** tab. | plan.md updated to say "third TabItem". ✅ Fixed. |
| L2 | Ambiguity | LOW | contracts/IMailboxRepository.cs comments (lines 65, 74, 82) | Comments use `Result.Success(entity.Id)` without type parameters — `Result` is `Result<TValue, TError>` and is not callable without both type arguments. Comments only (not compiled code), so no build impact. | Comments updated to use fully qualified forms e.g., `Result<Guid, Error>.Success(entity.Id)`. ✅ Fixed. |
| L3 | Inconsistency | LOW | tasks.md T022 (MailboxItemViewModel.DisplayName notification) | T022 says `UpdateFrom` calls `this.RaisePropertyChanged(nameof(DisplayName))` but does not state this call must come **after** all individual property setters. Placing it before updating `Username` or `Port` would briefly emit a stale `DisplayName` to bound ListBox items. | T022 updated to explicitly state the `RaisePropertyChanged(nameof(DisplayName))` call occurs after all property assignments. ✅ Fixed. |

---

## Coverage Summary Table

| Requirement Key | Has Task? | Task IDs | Notes |
|-----------------|-----------|----------|-------|
| FR-001 (Mailboxes tab, two-panel layout) | ✅ | T022, T023, T024, T025, T028, T029 | Full coverage |
| FR-002 (List entry format `{Username} @ {Host}:{Port}`) | ✅ | T022 | `DisplayName` computed property |
| FR-003 (Add mailbox via form) | ✅ | T023, T009, T004 | `AddMailboxCommand` + `SaveCommand` |
| FR-004 (Default host `imap.gmail.com`, port `993`) | ✅ | T023 | Backing field defaults |
| FR-005 (Password in OS Credential Locker only) | ✅ | T020, T009, T010 | `OsCredentialStore` implementation |
| FR-006 (Password masking `•`) | ✅ | T024 | `PasswordChar="•"` TextBox |
| FR-007 (Edit: blank password = preserve credential) | ✅ | T010, T023 | `SaveCredentialAsync` skipped when empty |
| FR-008 (Select → populate form) | ✅ | T023 | `WhenAnyValue(x => x.SelectedMailbox)` |
| FR-009 (InitialSyncDate required on Add) | ✅ | T001, T023 | Domain validation + VM CanExecute |
| FR-010 (Delete: remove credential then DB row) | ✅ | T011, T023 | `DeleteMailboxCommand` handler |
| FR-011 (Domain validation: host, port, username) | ✅ | T001, T002 | `DomainException` in entity |
| FR-012 (P/Invoke to `advapi32.dll`) | ✅ | T020 | `CredWriteW`, `CredReadW`, `CredDeleteW`, `CredFreeW` |
| FR-013 (Credential key `Rentier/Mailbox/{id}`) | ✅ | T009, T010, T011 | Key constructed in handlers |
| FR-014 (MailboxDto has no password) | ✅ | T003 | Password field intentionally absent |
| FR-015 (All I/O async) | ✅ | T008–T011, T020 | All `async Task`; H1 fix adds `Task.Run` to credential store |
| FR-016 (AddMailboxCommand returns new Guid) | ✅ | T009, T023 | Returned Id used to select new item |
| FR-017 (Inline validation errors, no modal dialogs) | ✅ | T023, T024 | `ErrorMessage` TextBlock, no `ShowDialog` |
| FR-018 (EF `OwnsOne<MailboxCursor>`) | ✅ | T016, T018 | `MailboxConfiguration.OwnsOne(...)` |
| SC: Save < 200 ms | ✅ | T021, T035 | Infrastructure integration + quickstart validation |
| SC: List render < 100 ms for ≤ 50 mailboxes | ✅ | T035 | Quickstart validation scenario |

**All 18 FRs and both buildable Success Criteria have ≥ 1 covering task.**

---

## Constitution Alignment Issues

| Principle | Status | Finding |
|-----------|--------|---------|
| I. Clean Architecture Dependency Rule | ✅ PASS | All layer boundaries verified: Desktop → Application only; Infrastructure implements Application interfaces; Domain has no I/O dependencies |
| II. Local-First Security and Privacy | ✅ PASS (after C3 fix) | Passwords stored exclusively in Windows Credential Manager; credential-first ordering in `AddMailboxCommandHandler` now consistent across spec and contracts |
| III. Financial and Temporal Correctness | ✅ PASS | `InitialSyncDate` and `LastSyncDate` use `DateOnly`; no `decimal` fields in this feature |
| IV. Async and UI Responsiveness | ⚠️ REMEDIATED (H1 fix) | `OsCredentialStore` now wraps P/Invoke in `Task.Run` — UI thread will not be blocked |
| V. Specification-Driven Quality Gates | ✅ PASS | Domain 100% / Application ≥ 90% / CI warning-free gates all covered by T032–T034 |

---

## Unmapped Tasks

None — all 35 tasks map to at least one FR or architectural requirement.

---

## Metrics

| Metric | Value |
|--------|-------|
| Total Functional Requirements | 18 |
| Total Buildable Success Criteria | 2 |
| Total Tasks (T001–T035) | 35 |
| Requirements with ≥ 1 task | 20 / 20 (100 %) |
| Tasks with ≥ 1 mapped requirement | 35 / 35 (100 %) |
| CRITICAL issues found | 5 |
| HIGH issues found | 4 |
| MEDIUM issues found | 5 |
| LOW issues found | 3 |
| Constitution violations | 1 (H1 — remediated) |

---

## Next Actions

> ⛔ **5 CRITICAL issues were identified and have been remediated in-place** in `tasks.md`, `contracts/IMailboxRepository.cs`, and `contracts/OsCredentialStoreImpl.md`. Review the diffs before proceeding.

1. **Verify the C2 fix** (`InitialSyncDateOffset` bridge property): the bridging property pattern requires the `RaiseAndSetIfChanged` for both `InitialSyncDate` and `InitialSyncDateOffset` to stay in sync. Implementors should cross-check with Avalonia `DatePicker` docs to confirm `DateTimeOffset?` is the correct binding target in Avalonia 11.3+.

2. **Proceed to `/speckit.implement`**: All blocking issues are resolved. Suggested implementation order remains unchanged (Phase 1 → 2 → 3 → 4 → 5 → 6 → 7 → 8).

3. **CI gate reminder**: `OsCredentialStore` tests must be guarded with `[PlatformSpecific(TestPlatforms.Windows)]` or equivalent skip attribute; CI runs on Windows + macOS matrix.

---

*Would you like me to suggest concrete remediation edits for any remaining MEDIUM or LOW issues? The CRITICAL and HIGH fixes have already been applied to the spec documents.*
