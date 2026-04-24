# Quickstart: Publish Configuration

## What This Feature Does

Moves all Release publish settings (self-contained, single-file, ReadyToRun, embedded debug symbols) from scattered CLI flags in `release.yml` into `Rentier.Desktop.csproj` as MSBuild property groups. After this change, `dotnet publish -c Release -r <rid>` produces correct release artifacts without extra `-p:` flags.

## Prerequisites

- .NET 10 SDK installed
- Repository cloned at `F:\Projects\Rentier\rentier`

## Files Changed

| File | Change |
|---|---|
| `src/Rentier.Desktop/Rentier.Desktop.csproj` | Add 2 Release-conditioned PropertyGroups |
| `.github/workflows/release.yml` | Remove redundant `--self-contained`, `-p:PublishSingleFile`, `-p:PublishReadyToRun`, `-p:DebugType` flags |

## Verification Commands

### 1. Verify Release publish produces single-file (Windows)

```powershell
dotnet publish src/Rentier.Desktop/Rentier.Desktop.csproj -c Release -r win-x64 -o ./publish/test-win
# Expected: ./publish/test-win/ contains Rentier.Desktop.exe (single file, no .pdb, no loose DLLs)
Get-ChildItem ./publish/test-win -Recurse | Select-Object Name, Length
```

### 2. Verify Debug build is unaffected

```powershell
dotnet build src/Rentier.Desktop/Rentier.Desktop.csproj -c Debug
# Expected: Normal build, no single-file or self-contained behavior
```

### 3. Verify Debug publish is unaffected

```powershell
dotnet publish src/Rentier.Desktop/Rentier.Desktop.csproj -c Debug -r win-x64 -o ./publish/test-debug
# Expected: ./publish/test-debug/ contains many DLLs (framework-dependent, multi-file)
(Get-ChildItem ./publish/test-debug -Filter *.dll).Count  # Should be > 50
```

### 4. Verify ReadyToRun is Windows-only

```powershell
# On Windows: Check R2R headers in the executable
dotnet publish src/Rentier.Desktop/Rentier.Desktop.csproj -c Release -r win-x64 -o ./publish/test-r2r -v:n 2>&1 | Select-String "ReadyToRun"
# Expected: ReadyToRun compilation messages visible

# On macOS/Linux: ReadyToRun should NOT appear
# dotnet publish ... -c Release -r osx-arm64 -v:n 2>&1 | grep "ReadyToRun"
# Expected: No ReadyToRun compilation messages
```

### 5. Verify no separate PDB files in Release output

```powershell
dotnet publish src/Rentier.Desktop/Rentier.Desktop.csproj -c Release -r win-x64 -o ./publish/test-pdb
(Get-ChildItem ./publish/test-pdb -Filter *.pdb).Count  # Should be 0
```

### 6. Verify existing CI workflow still works

After removing redundant flags from `release.yml`, the CI release workflow should produce identical artifacts. This is verified by running the release workflow on a test tag.

## Cleanup

```powershell
Remove-Item -Recurse -Force ./publish/test-*
```

## Rollback

If issues are discovered, revert the two changed files:
1. Remove the two new `<PropertyGroup>` blocks from `Rentier.Desktop.csproj`
2. Restore the removed CLI flags in `release.yml`

The CLI flags and project-file properties are functionally equivalent, so reverting is a clean swap with zero runtime impact.
