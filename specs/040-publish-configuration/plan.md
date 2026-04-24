# Implementation Plan: Publish Configuration

**Branch**: `040-publish-configuration` | **Date**: 2025-07-17 | **Spec**: [spec.md](../../.specify/specs/040-publish-configuration/spec.md)
**Input**: Feature specification from `.specify/specs/040-publish-configuration/spec.md`

## Summary

Consolidate scattered `dotnet publish` command-line flags (`--self-contained`, `-p:PublishSingleFile`, `-p:PublishReadyToRun`, `-p:DebugType=embedded`) into MSBuild property groups in `Rentier.Desktop.csproj`, conditioned on `Release` configuration. This makes the project file the single source of truth for Release publish settings, enables simpler CI invocations, and ensures Debug builds remain unaffected. A secondary cleanup simplifies `release.yml` by removing the now-redundant `-p:` flags.

## Technical Context

**Language/Version**: C# 14 / .NET 10 (`net10.0` target framework)
**Primary Dependencies**: Avalonia 11, ReactiveUI 20, EF Core 10, CommunityToolkit.Mvvm 8
**Storage**: N/A — build configuration only, no runtime data changes
**Testing**: Build-level validation via `dotnet publish` / `dotnet build` with output inspection; xUnit + FluentAssertions for existing tests (regression only)
**Target Platform**: Windows x64, macOS x64/ARM64, Linux x64
**Project Type**: Desktop application (Avalonia UI)
**Performance Goals**: Debug build time must not regress (±5%); ReadyToRun on Windows for faster cold start
**Constraints**: `PublishTrimmed` excluded from scope (remains CI-only flag); `PublishReadyToRun` Windows-only due to R2R limitations on other platforms
**Scale/Scope**: 2 files changed (`Rentier.Desktop.csproj`, `.github/workflows/release.yml`)

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- [x] Clean Architecture boundary is preserved (`Desktop -> Application -> Domain`; Infrastructure implements Application contracts only).
  > ✅ Only `Rentier.Desktop.csproj` is modified — build configuration properties, no dependency or source code changes. Architecture boundaries untouched.
- [x] All monetary/rate/percentage values are modeled as `decimal`.
  > ✅ N/A — no monetary or rate values involved in build configuration.
- [x] All business dates are modeled as `DateOnly`; boundary conversions are identified.
  > ✅ N/A — no date handling involved.
- [x] Security/privacy constraints hold: local-first data, OS credential store for secrets, no telemetry.
  > ✅ N/A — no data storage, credential, or telemetry changes. Self-contained publish model does not alter local-first architecture.
- [x] External network usage is limited to approved endpoints (IMAP and NBS) or explicitly justified as a constitution amendment.
  > ✅ N/A — no network endpoints added or modified.
- [x] All I/O paths are async; UI work avoids blocking calls and uses reactive async command flow.
  > ✅ N/A — no runtime code changes. ReadyToRun improves startup but does not alter async patterns.
- [x] Tests and coverage impact are defined for Domain (rule/state coverage) and Application (>=90%).
  > ✅ No domain or application code changes. Existing test suite runs as regression check. Verification is build-output inspection, not unit tests.
- [x] Feature work is mapped to an approved spec task under `.specify/tasks/`.
  > ✅ Spec exists at `.specify/specs/040-publish-configuration/spec.md`.

**Pre-design gate: ✅ PASSED** — All checks pass. This is a build-configuration-only feature with zero runtime code changes and zero architecture boundary impact.

## Project Structure

### Documentation (this feature)

```text
specs/040-publish-configuration/
├── plan.md              # This file
├── research.md          # Phase 0: MSBuild property research
├── data-model.md        # Phase 1: Property model and conditions
├── quickstart.md        # Phase 1: Verification guide
└── tasks.md             # Phase 2: Implementation tasks (future)
```

### Source Code (affected files)

```text
src/Rentier.Desktop/
└── Rentier.Desktop.csproj       # Add Release-conditioned PropertyGroup

.github/workflows/
└── release.yml                  # Remove redundant -p: flags from publish steps
```

**Structure Decision**: This feature modifies only existing files — no new source directories, projects, or test files are created. The Desktop `.csproj` is the sole build-configuration target. The release workflow is a secondary cleanup target.

## Complexity Tracking

> No Constitution Check violations. No complexity justification required.
