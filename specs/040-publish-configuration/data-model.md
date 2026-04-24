# Data Model: Publish Configuration

This feature does not introduce domain entities, value objects, or database schema changes. It operates entirely at the MSBuild project-file level. This document models the **MSBuild property structure** that replaces the previous CLI-flag approach.

## MSBuild Property Model

### PropertyGroup 1: Release Publish Settings (all platforms)

**Condition**: `'$(Configuration)' == 'Release'`
**Location**: `src/Rentier.Desktop/Rentier.Desktop.csproj`

| Property | Type | Value | Default (when condition is false) | Purpose |
|---|---|---|---|---|
| `SelfContained` | bool | `true` | `false` | Bundle .NET runtime with app |
| `PublishSingleFile` | bool | `true` | `false` | Merge managed assemblies into one executable |
| `IncludeNativeLibrariesForSelfExtract` | bool | `true` | `false` | Embed native libs (SQLite) in single file |
| `EnableCompressionInSingleFile` | bool | `true` | `false` | Compress bundle for smaller distributable |
| `DebugType` | string | `embedded` | `portable` | Embed PDB symbols inside executable |

### PropertyGroup 2: ReadyToRun (Windows-only Release)

**Condition**: `'$(Configuration)' == 'Release' And '$([MSBuild]::IsOSPlatform(Windows))'`
**Location**: `src/Rentier.Desktop/Rentier.Desktop.csproj`

| Property | Type | Value | Default (when condition is false) | Purpose |
|---|---|---|---|---|
| `PublishReadyToRun` | bool | `true` | `false` | Ahead-of-time native compilation |

## Condition Evaluation Matrix

| Build Command | Configuration | OS | PG1 Active | PG2 Active | Result |
|---|---|---|---|---|---|
| `dotnet build -c Debug` | Debug | any | ❌ | ❌ | Standard Debug build |
| `dotnet build -c Release` | Release | any | ✅ | varies | Build with Release props (no publish) |
| `dotnet publish -c Debug -r win-x64` | Debug | Windows | ❌ | ❌ | Framework-dependent, multi-file |
| `dotnet publish -c Release -r win-x64` | Release | Windows | ✅ | ✅ | Self-contained, single-file, R2R, embedded PDB |
| `dotnet publish -c Release -r osx-arm64` | Release | macOS | ✅ | ❌ | Self-contained, single-file, no R2R, embedded PDB |
| `dotnet publish -c Release -r linux-x64` | Release | Linux | ✅ | ❌ | Self-contained, single-file, no R2R, embedded PDB |
| `dotnet publish` (no flags) | Debug (default) | any | ❌ | ❌ | Framework-dependent, multi-file |

## Exact .csproj Diff

The following XML is added to `src/Rentier.Desktop/Rentier.Desktop.csproj`, after the existing `<PropertyGroup>` blocks and before the first `<ItemGroup>`:

```xml
  <!-- Release publish settings: self-contained single-file with embedded symbols -->
  <PropertyGroup Condition="'$(Configuration)' == 'Release'">
    <SelfContained>true</SelfContained>
    <PublishSingleFile>true</PublishSingleFile>
    <IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
    <EnableCompressionInSingleFile>true</EnableCompressionInSingleFile>
    <DebugType>embedded</DebugType>
  </PropertyGroup>

  <!-- ReadyToRun: Windows-only (R2R not beneficial on macOS/Linux for Avalonia) -->
  <PropertyGroup Condition="'$(Configuration)' == 'Release' And '$([MSBuild]::IsOSPlatform(Windows))'">
    <PublishReadyToRun>true</PublishReadyToRun>
  </PropertyGroup>
```

## CI Workflow Diff Model

### Windows publish step (before → after)

**Before** (line 67-79 of `release.yml`):
```yaml
- name: Publish win-x64
  run: >
    dotnet publish ${{ env.PROJECT_PATH }}
    -c Release
    -r win-x64
    --self-contained
    -p:PublishSingleFile=true
    -p:PublishReadyToRun=true
    -p:PublishTrimmed=${{ needs.version.outputs.trimmed }}
    -p:DebugType=embedded
    -p:Version=${{ needs.version.outputs.version }}
    -p:AssemblyVersion=${{ needs.version.outputs.version }}
    -p:FileVersion=${{ needs.version.outputs.version }}
    -o ./publish/win-x64
```

**After**:
```yaml
- name: Publish win-x64
  run: >
    dotnet publish ${{ env.PROJECT_PATH }}
    -c Release
    -r win-x64
    -p:PublishTrimmed=${{ needs.version.outputs.trimmed }}
    -p:Version=${{ needs.version.outputs.version }}
    -p:AssemblyVersion=${{ needs.version.outputs.version }}
    -p:FileVersion=${{ needs.version.outputs.version }}
    -o ./publish/win-x64
```

### macOS publish step (before → after)

**Before** (lines 126-136):
```yaml
- name: Publish ${{ matrix.rid }}
  run: >
    dotnet publish ${{ env.PROJECT_PATH }}
    -c Release
    -r ${{ matrix.rid }}
    --self-contained
    -p:PublishTrimmed=${{ needs.version.outputs.trimmed }}
    -p:DebugType=embedded
    -p:Version=${{ needs.version.outputs.version }}
    -p:AssemblyVersion=${{ needs.version.outputs.version }}
    -p:FileVersion=${{ needs.version.outputs.version }}
    -o ./publish/${{ matrix.rid }}
```

**After**:
```yaml
- name: Publish ${{ matrix.rid }}
  run: >
    dotnet publish ${{ env.PROJECT_PATH }}
    -c Release
    -r ${{ matrix.rid }}
    -p:PublishTrimmed=${{ needs.version.outputs.trimmed }}
    -p:Version=${{ needs.version.outputs.version }}
    -p:AssemblyVersion=${{ needs.version.outputs.version }}
    -p:FileVersion=${{ needs.version.outputs.version }}
    -o ./publish/${{ matrix.rid }}
```

### Linux publish step (before → after)

**Before** (lines 227-237):
```yaml
- name: Publish linux-x64
  run: >
    dotnet publish ${{ env.PROJECT_PATH }}
    -c Release
    -r linux-x64
    --self-contained
    -p:PublishSingleFile=true
    -p:PublishTrimmed=${{ needs.version.outputs.trimmed }}
    -p:DebugType=embedded
    -p:Version=${{ needs.version.outputs.version }}
    -p:AssemblyVersion=${{ needs.version.outputs.version }}
    -p:FileVersion=${{ needs.version.outputs.version }}
    -o ./publish/linux-x64
```

**After**:
```yaml
- name: Publish linux-x64
  run: >
    dotnet publish ${{ env.PROJECT_PATH }}
    -c Release
    -r linux-x64
    -p:PublishTrimmed=${{ needs.version.outputs.trimmed }}
    -p:Version=${{ needs.version.outputs.version }}
    -p:AssemblyVersion=${{ needs.version.outputs.version }}
    -p:FileVersion=${{ needs.version.outputs.version }}
    -o ./publish/linux-x64
```

## Validation Rules

1. **Release-only activation**: Properties in PG1 and PG2 MUST NOT appear when `Configuration != Release`.
2. **Windows-only R2R**: `PublishReadyToRun` MUST be `true` only when both `Configuration == Release` AND the build agent OS is Windows.
3. **No separate PDBs**: With `DebugType=embedded`, the publish output directory MUST contain zero `.pdb` files.
4. **Single executable**: With `PublishSingleFile=true`, the publish output directory MUST contain exactly one executable file (plus platform-specific companions like `.app` bundles on macOS).
5. **Native embedding**: With `IncludeNativeLibrariesForSelfExtract=true`, native libraries like `e_sqlite3.dll` MUST NOT appear as separate files in the publish output.

## State Transitions

N/A — No domain entities or state machines are affected by this feature.
