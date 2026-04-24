# Tasks: Publish Configuration

**Input**: Design documents from `specs/040-publish-configuration/`  
**Spec**: `.specify/specs/040-publish-configuration/spec.md`  
**Branch**: `040-publish-configuration`  
**Scope**: 2 files changed — `Rentier.Desktop.csproj` and `release.yml`. No source code, no new projects, no tests required.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: Which user story this task belongs to (US1, US2, US3)
- Exact file paths are included in every description

---

## Phase 1: Setup

**Purpose**: Establish the baseline — confirm the exact insertion point in the `.csproj` and identify all redundant flags to remove from `release.yml` before touching any file.

- [ ] T001 Confirm insertion point in `src/Rentier.Desktop/Rentier.Desktop.csproj`: locate the last existing `<PropertyGroup>` block and verify no Release-conditioned groups already exist; confirm the file closes with `</Project>` and note the line immediately before it as the insertion site

**Checkpoint**: Baseline confirmed — ready to begin user story implementation.

---

## Phase 2: Foundational (Blocking Prerequisites)

> **N/A** — This feature is a pure build-configuration change with no shared runtime infrastructure. Both story tracks (`Rentier.Desktop.csproj` and `release.yml`) are independent files. User story implementation can begin directly after Phase 1.

---

## Phase 3: User Story 1 — Consistent Release Builds (Priority: P1) 🎯 MVP

**Goal**: Make `dotnet publish -c Release -r <rid>` produce a self-contained, single-file executable with embedded debug symbols and no separate `.pdb` files, using only project-file settings and no extra CLI flags.

**Independent Test**: `dotnet publish src/Rentier.Desktop/Rentier.Desktop.csproj -c Release -r win-x64 -o ./publish/test-win` → output directory contains exactly one `.exe`, zero `.pdb` files, and no loose DLL files.

### Implementation for User Story 1

- [ ] T002 [US1] Add Release-conditioned PropertyGroup (`SelfContained`, `PublishSingleFile`, `IncludeNativeLibrariesForSelfExtract`, `EnableCompressionInSingleFile`, `DebugType=embedded`) to `src/Rentier.Desktop/Rentier.Desktop.csproj` — insert before the closing `</Project>` tag per the exact XML in `specs/040-publish-configuration/data-model.md`

- [ ] T003 [US1] Add Windows-only Release PropertyGroup (`PublishReadyToRun=true`) with condition `'$(Configuration)' == 'Release' And '$([MSBuild]::IsOSPlatform(Windows))'` to `src/Rentier.Desktop/Rentier.Desktop.csproj` — insert immediately after the PropertyGroup added in T002

- [ ] T004 [US1] Run `dotnet publish src/Rentier.Desktop/Rentier.Desktop.csproj -c Release -r win-x64 -o ./publish/test-win` and verify: (a) output contains exactly one `.exe` file, (b) `(Get-ChildItem ./publish/test-win -Filter *.pdb).Count` returns 0, (c) no `e_sqlite3.dll` or other loose native DLLs are present in the output directory

- [ ] T005 [US1] Run `dotnet publish src/Rentier.Desktop/Rentier.Desktop.csproj -c Release -r win-x64 -v:n 2>&1 | Select-String "ReadyToRun"` and verify ReadyToRun compilation messages appear, confirming Windows-only R2R is active

**Checkpoint**: User Story 1 fully functional — `dotnet publish -c Release -r win-x64` produces a self-contained single-file executable with embedded symbols and ReadyToRun compilation.

---

## Phase 4: User Story 2 — Debug Builds Unaffected (Priority: P1)

**Goal**: Confirm that no Release publish settings bleed into Debug builds or Debug publish invocations — developer workflow and build times remain identical to pre-feature state.

**Independent Test**: `dotnet publish src/Rentier.Desktop/Rentier.Desktop.csproj -c Debug -r win-x64 -o ./publish/test-debug` → output directory contains many DLL files (>50), confirming framework-dependent multi-file output.

> **Note**: No implementation tasks — US2 acceptance is entirely verified by the conditional MSBuild groups added in US1. The Release `Condition` attribute inherently prevents Debug builds from seeing these properties. The tasks below are verification only.

### Verification for User Story 2

- [ ] T006 [US2] Run `dotnet build src/Rentier.Desktop/Rentier.Desktop.csproj -c Debug` and verify the build completes successfully with no single-file, self-contained, or ReadyToRun messages in the output; confirm build time is not significantly different from pre-change baseline

- [ ] T007 [US2] Run `dotnet publish src/Rentier.Desktop/Rentier.Desktop.csproj -c Debug -r win-x64 -o ./publish/test-debug` and verify `(Get-ChildItem ./publish/test-debug -Filter *.dll).Count` returns >50, confirming framework-dependent multi-file output

- [ ] T008 [US2] Run `dotnet publish src/Rentier.Desktop/Rentier.Desktop.csproj --getProperty:PublishSingleFile` (no `-c` flag — defaults to Debug) and verify the returned value is empty or `false`, confirming the default Debug configuration is completely unaffected

**Checkpoint**: User Story 2 verified — Debug builds and Debug publishes are unchanged.

---

## Phase 5: User Story 3 — Simplified CI Pipeline (Priority: P2)

**Goal**: Remove the now-redundant CLI flags from all three platform publish steps in `.github/workflows/release.yml`, making the project file the single source of truth and reducing per-platform flag count by 4 (Windows), 2 (macOS), and 3 (Linux).

**Independent Test**: Running `dotnet publish $PROJECT -c Release -r win-x64 -p:PublishTrimmed=false -p:Version=0.0.0 -p:AssemblyVersion=0.0.0 -p:FileVersion=0.0.0 -o ./publish/verify-ci` (simulating the simplified CI command) produces output identical to T004.

### Implementation for User Story 3

- [ ] T009 [US3] Remove `--self-contained`, `-p:PublishSingleFile=true`, `-p:PublishReadyToRun=true`, and `-p:DebugType=embedded` from the `Publish win-x64` step (lines 67–79) in `.github/workflows/release.yml`; retain `-c Release`, `-r win-x64`, `-p:PublishTrimmed=...`, `-p:Version=...`, `-p:AssemblyVersion=...`, `-p:FileVersion=...`, `-o ./publish/win-x64`

- [ ] T010 [US3] Remove `--self-contained` and `-p:DebugType=embedded` from the `Publish ${{ matrix.rid }}` step (lines 125–136) in `.github/workflows/release.yml` (covers both `osx-x64` and `osx-arm64` matrix entries); retain `-c Release`, `-r ${{ matrix.rid }}`, `-p:PublishTrimmed=...`, version flags, and `-o ./publish/${{ matrix.rid }}`

- [ ] T011 [US3] Remove `--self-contained`, `-p:PublishSingleFile=true`, and `-p:DebugType=embedded` from the `Publish linux-x64` step (lines 226–238) in `.github/workflows/release.yml`; retain `-c Release`, `-r linux-x64`, `-p:PublishTrimmed=...`, version flags, and `-o ./publish/linux-x64`

- [ ] T012 [US3] Simulate the simplified CI command locally: run `dotnet publish src/Rentier.Desktop/Rentier.Desktop.csproj -c Release -r win-x64 -p:PublishTrimmed=false -p:Version=0.0.0 -p:AssemblyVersion=0.0.0 -p:FileVersion=0.0.0 -o ./publish/verify-ci` and verify output is structurally identical to the T004 result (single executable, zero `.pdb` files)

**Checkpoint**: User Story 3 verified — CI workflow is simplified, 9 total flags removed across 3 platforms (4 Windows + 2 macOS + 3 Linux), exceeding SC-005 requirement of ≥3 per platform for Windows and Linux.

---

## Phase 6: Polish & Cleanup

**Purpose**: Final validation against all acceptance criteria and cleanup of test artifacts.

- [ ] T013 Run the full quickstart verification sequence from `specs/040-publish-configuration/quickstart.md`: all 6 verification commands (Release single-file, Debug build, Debug publish, ReadyToRun check, zero PDB check, CI simulation) and confirm all pass

- [ ] T014 [P] Clean up test publish artifacts: `Remove-Item -Recurse -Force ./publish/test-win, ./publish/test-debug, ./publish/verify-ci` (and any other `./publish/test-*` directories created during verification)

- [ ] T015 [P] Review the final diff of `src/Rentier.Desktop/Rentier.Desktop.csproj` against the exact XML in `specs/040-publish-configuration/data-model.md` to confirm both PropertyGroups match the specification precisely (conditions, property names, values)

- [ ] T016 [P] Review the final diff of `.github/workflows/release.yml` and count removed flags: confirm 4 removed from Windows step, 2 from macOS step, 3 from Linux step; verify no version flags or `-p:PublishTrimmed` flag were accidentally removed

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **Phase 2 (Foundational)**: N/A — skipped for this feature
- **US1 (Phase 3)**: Depends on Phase 1; no dependency on US3
- **US2 (Phase 4)**: Depends on US1 being complete (requires the `.csproj` change from T002/T003)
- **US3 (Phase 5)**: Depends only on Phase 1; can proceed in parallel with US1 and US2 (different file: `release.yml`)
- **Polish (Phase 6)**: Depends on all three user stories being complete

### User Story Dependencies

```
Phase 1 (T001)
    ├── US1: T002 → T003 → T004 → T005
    │       ↓ (must complete before)
    └── US2: T006 → T007 → T008        ← depends on US1
    
Phase 1 (T001)
    └── US3: T009 → T010 → T011 → T012 ← independent of US1/US2
    
All phases complete → Polish: T013 → T014, T015, T016 [parallel]
```

### Within Each User Story

- T002 before T003 (both modify `.csproj` — sequential edits to same file)
- T003 before T004 (need both PropertyGroups before verifying publish output)
- T004 before T005 (need the publish artifact before inspecting R2R output)
- US2 tasks (T006–T008) require US1 completion — the `.csproj` conditions must exist before Debug verification is meaningful
- T009 → T010 → T011 are sequential (all edit the same `release.yml` file); T012 follows after all three edits

### Parallel Opportunities

- **US1 and US3 can run in parallel** (different files: `.csproj` vs. `release.yml`)
- Polish tasks T014, T015, T016 are all [P] and can run simultaneously after T013

---

## Parallel Example: US1 + US3 Concurrent

```text
# After T001 (baseline review), these two tracks can proceed simultaneously:

Track A — US1 (Rentier.Desktop.csproj):
  T002 → T003 → T004 → T005
  
Track B — US3 (release.yml):
  T009 → T010 → T011 → T012

# After Track A completes, US2 verification can begin:
  T006 → T007 → T008
  
# After all tracks complete, run polish:
  T013 → (T014 ∥ T015 ∥ T016)
```

---

## Implementation Strategy

### MVP (User Story 1 Only)

1. Complete **Phase 1** (T001 — baseline review)
2. Complete **Phase 3** (T002–T005 — add PropertyGroups and verify Release publish)
3. **STOP and VALIDATE**: Run `dotnet publish -c Release -r win-x64` and confirm single-file output
4. This alone satisfies FR-001 through FR-007 and SC-001 through SC-003

### Full Feature Delivery

1. Phase 1 → US1 → US2 (verify Debug builds unaffected) → US3 (CI cleanup) → Polish
2. All 16 tasks completed; all 5 success criteria met
3. Two files changed, zero new files, zero test file changes

---

## Summary

| Phase | Tasks | User Story | Files Modified |
|---|---|---|---|
| Phase 1: Setup | T001 | — | (read only) |
| Phase 3: US1 | T002–T005 | US1 (P1) | `src/Rentier.Desktop/Rentier.Desktop.csproj` |
| Phase 4: US2 | T006–T008 | US2 (P1) | (verification only) |
| Phase 5: US3 | T009–T012 | US3 (P2) | `.github/workflows/release.yml` |
| Phase 6: Polish | T013–T016 | — | (verification + cleanup) |

**Total tasks**: 16  
**Parallel opportunities**: US1 and US3 tracks, Polish tasks T014/T015/T016  
**MVP scope**: T001 + T002 + T003 + T004 + T005 (5 tasks, Phase 1 + US1 only)  
**No test tasks**: This feature is build-configuration only; verification is `dotnet publish`/`dotnet build` output inspection (per CA-006 and spec assumption)

---

## Notes

- [P] tasks = different files or read-only steps with no write-dependency conflicts
- No xUnit test tasks: The spec explicitly states "Verification consists of build-level validation" (CA-006); no Domain or Application logic is changed
- `PublishTrimmed` intentionally excluded from `.csproj` — remains a CI-controlled toggle per spec assumptions
- If `dotnet publish` for win-x64 fails locally due to missing native toolchain, use `-r linux-x64` or `-r osx-arm64` on the appropriate machine to verify the non-Windows path (R2R will correctly be absent)
- Commit after T003 (PropertyGroups added) and after T011 (release.yml cleaned up) as two logical changesets
