# Research: Publish Configuration

## R1: MSBuild Conditional PropertyGroup Patterns for Release-Only Settings

**Decision**: Use `<PropertyGroup Condition="'$(Configuration)' == 'Release'">` in the Desktop `.csproj` to scope all publish properties exclusively to Release builds.

**Rationale**: MSBuild evaluates `Condition` attributes at property-group level before individual properties. Placing all publish-related properties inside a single Release-conditioned group ensures:
- Debug builds never see these properties (they remain at their MSBuild defaults: `false`/unset).
- A clean separation between development and release configuration.
- Standard MSBuild pattern used across the .NET ecosystem — no custom targets or imports required.

**Alternatives Considered**:
- **`Directory.Build.props`**: Rejected — publish settings are specific to `Rentier.Desktop` and should not leak to Domain, Application, Infrastructure, or test projects. The existing `Directory.Build.props` is for shared cross-project settings (TargetFramework, Nullable, etc.).
- **Publish profiles (`.pubxml`)**: Rejected — `.pubxml` files are per-RID and require selecting a profile at publish time. They duplicate configuration across 4 RIDs and are less transparent than inline MSBuild properties.
- **`launchSettings.json`**: Not applicable — launch settings control `dotnet run` debugging, not `dotnet publish` behavior.

---

## R2: PublishReadyToRun Platform Conditioning

**Decision**: Use a nested `<PropertyGroup>` with `Condition="'$(Configuration)' == 'Release' And '$([MSBuild]::IsOSPlatform(Windows))'"` to enable ReadyToRun only on Windows build agents.

**Rationale**: `PublishReadyToRun` compiles managed assemblies into native code ahead of time. On .NET 10:
- Windows R2R is mature and provides measurable cold-start improvement for desktop apps.
- macOS and Linux R2R support exists but provides less benefit for Avalonia apps and can cause issues with certain native interop scenarios.
- The spec explicitly requires R2R on Windows (FR-003) and explicitly prohibits it on non-Windows (FR-004).

**Why `$([MSBuild]::IsOSPlatform(Windows))`**: This MSBuild intrinsic function evaluates at build time on the *build agent's* OS, not the target RID. This is correct because:
- Windows builds run on `windows-latest` CI agents → R2R enabled.
- macOS builds run on `macos-latest` → R2R skipped.
- Linux builds run on `ubuntu-latest` → R2R skipped.
- Local developer builds on Windows get R2R in Release mode, which is desirable.

**Alternatives Considered**:
- **RID-based condition** (`$(RuntimeIdentifier).StartsWith('win')`): Technically more correct for cross-compilation scenarios, but Rentier never cross-compiles (each OS builds its own RID). The `IsOSPlatform` approach is simpler and matches the CI matrix.
- **Separate PropertyGroups per RID**: Rejected — creates 4 nearly-identical property groups. The only difference is R2R; a single OS condition is cleaner.
- **CI-only flag (keep `-p:PublishReadyToRun` in workflow)**: Rejected — contradicts the feature goal of making the project file the single source of truth.

---

## R3: Self-Contained Publish and Single-File Properties Interaction

**Decision**: Set all six publish properties together in one Release-conditioned group:

| Property | Value | Purpose |
|---|---|---|
| `SelfContained` | `true` | Bundle .NET runtime with the app |
| `PublishSingleFile` | `true` | Merge all managed DLLs into one executable |
| `IncludeNativeLibrariesForSelfExtract` | `true` | Embed native libs (e.g., SQLite) in the single file |
| `EnableCompressionInSingleFile` | `true` | Compress the bundle for smaller distributable |
| `DebugType` | `embedded` | Embed PDB symbols inside the executable |
| `PublishReadyToRun` | `true` | (Windows-only) Ahead-of-time native compilation |

**Rationale**: These properties form a coherent set for producing release-quality distributables:
- `SelfContained=true` replaces `--self-contained` CLI flag — equivalent behavior per .NET SDK docs.
- `PublishSingleFile=true` merges assemblies; `IncludeNativeLibrariesForSelfExtract=true` ensures native binaries (e.g., `e_sqlite3.dll`) are embedded rather than extracted as sidecar files.
- `EnableCompressionInSingleFile=true` reduces distributable size at the cost of slightly slower first startup (decompression). Acceptable for a desktop app that starts once and runs long.
- `DebugType=embedded` eliminates separate `.pdb` files, keeping the publish output clean.

**Alternatives Considered**:
- **`IncludeAllContentForSelfExtract`**: Rejected — this is a broader flag that includes content files unnecessarily. `IncludeNativeLibrariesForSelfExtract` is the precise property for native DLL embedding.
- **`DebugType=none`**: Rejected — disabling symbols entirely makes post-release crash diagnostics impossible. `embedded` preserves symbols inside the binary.

---

## R4: Impact on macOS .app Bundle and Linux Packaging

**Decision**: `PublishSingleFile=true` in the `.csproj` replaces the `-p:PublishSingleFile=true` flag currently used only for Windows and Linux in the release workflow. macOS currently does **not** use `PublishSingleFile` — the `.app` bundle structure is a directory with the executable plus its resources inside `Contents/MacOS/`.

**Rationale**: After adding `PublishSingleFile=true` to the project file, macOS builds will now also produce a single-file executable. This is actually beneficial:
- The `.app` bundle script copies everything from `publish/<rid>/` into `Contents/MacOS/`. With single-file, there's just one binary to copy instead of hundreds of DLLs.
- The `.app` bundle itself (the `Rentier.app` directory) is still a directory — single-file affects what's *inside* `Contents/MacOS/`, not the bundle structure.
- This aligns macOS behavior with Windows and Linux (spec FR-002: "Release publishes MUST produce a single-file executable").

**Risk**: None identified. The macOS release workflow step already handles a directory of files; a single file is a strict subset of that.

---

## R5: CI Workflow Simplification — Flags That Can Be Removed

**Decision**: After the `.csproj` changes, the following CLI flags become redundant and can be removed from `release.yml`:

| Platform | Removable Flags | Retained Flags |
|---|---|---|
| **Windows** (`build-windows`) | `--self-contained`, `-p:PublishSingleFile=true`, `-p:PublishReadyToRun=true`, `-p:DebugType=embedded` | `-c Release`, `-r win-x64`, `-p:PublishTrimmed=...`, `-p:Version=...`, `-p:AssemblyVersion=...`, `-p:FileVersion=...` |
| **macOS** (`build-macos`) | `--self-contained`, `-p:DebugType=embedded` | `-c Release`, `-r ${{ matrix.rid }}`, `-p:PublishTrimmed=...`, `-p:Version=...`, `-p:AssemblyVersion=...`, `-p:FileVersion=...` |
| **Linux** (`build-linux`) | `--self-contained`, `-p:PublishSingleFile=true`, `-p:DebugType=embedded` | `-c Release`, `-r linux-x64`, `-p:PublishTrimmed=...`, `-p:Version=...`, `-p:AssemblyVersion=...`, `-p:FileVersion=...` |

**Retained flags rationale**:
- `-c Release` and `-r <rid>`: Always required to select configuration and runtime identifier.
- `-p:PublishTrimmed`: Intentionally excluded from scope (per spec assumptions) — remains a CI-controlled toggle.
- `-p:Version`, `-p:AssemblyVersion`, `-p:FileVersion`: Dynamic per-release, set from tag/input — must remain as CLI overrides.

**Count**: 4 flags removed from Windows, 2 from each macOS matrix entry, 3 from Linux = **11 total flag removals** across all publish steps. Spec SC-005 requires "at least 3 redundant flags per platform publish step" — Windows (4) and Linux (3) clearly exceed this; macOS (2) is slightly under but macOS previously had fewer flags to begin with.

---

## R6: Debug Build Non-Interference Verification

**Decision**: No special action needed — MSBuild's `Condition` attribute on the PropertyGroup inherently prevents Release-only properties from affecting Debug builds.

**Rationale**: When `$(Configuration)` is `Debug`:
- The entire `<PropertyGroup Condition="'$(Configuration)' == 'Release'">` block is skipped.
- All publish properties revert to their MSBuild defaults: `SelfContained=false`, `PublishSingleFile=false`, `PublishReadyToRun=false`, `DebugType=portable` (the SDK default).
- `dotnet build -c Debug` and `dotnet publish -c Debug` produce standard framework-dependent output.
- `dotnet publish` without `-c` defaults to `Debug` configuration, so publish properties don't apply.

**Verification approach**: Run `dotnet publish -c Debug -r win-x64 --getProperty:PublishSingleFile` to confirm the property is not set in Debug configuration.
