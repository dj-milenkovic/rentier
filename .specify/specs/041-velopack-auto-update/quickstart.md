# Quickstart: Velopack Auto-Update

**Feature**: 041-velopack-auto-update

## Prerequisites

- .NET 8 SDK installed
- Rentier solution builds successfully (`dotnet build Rentier.slnx`)
- Familiarity with the Rentier Clean Architecture layers

## Setup

1. **Install Velopack NuGet package** in two projects:

   ```bash
   # Infrastructure (implements IUpdateService)
   dotnet add src/Rentier.Infrastructure/Rentier.Infrastructure.csproj package Velopack

   # Desktop (VelopackApp lifecycle hooks in Program.cs)
   dotnet add src/Rentier.Desktop/Rentier.Desktop.csproj package Velopack
   ```

2. **Verify the solution builds**:
   ```bash
   dotnet build Rentier.slnx
   ```

## Key Files to Modify

| File | Change |
|---|---|
| `src/Rentier.Desktop/Program.cs` | Add `VelopackApp.Build().Run()` as first line in `Main()` |
| `src/Rentier.Application/Interfaces/IUpdateService.cs` | **New** — Application-layer update contract |
| `src/Rentier.Application/DTOs/UpdateCheckResult.cs` | **New** — Update check result DTO |
| `src/Rentier.Application/DTOs/UpdateState.cs` | **New** — Update state enum |
| `src/Rentier.Infrastructure/Updates/VelopackUpdateService.cs` | **New** — IUpdateService implementation |
| `src/Rentier.Infrastructure/InfrastructureServiceExtensions.cs` | Register `IUpdateService` |
| `src/Rentier.Desktop/ViewModels/MainWindowViewModel.cs` | Add update state properties and commands |
| `src/Rentier.Desktop/Views/MainWindow.axaml` | Add notification bar AXAML |
| `src/Rentier.Desktop/Resources/Strings.resx` | Add update-related string resources |

## Key Files to Create for Tests

| File | Tests |
|---|---|
| `tests/Rentier.UnitTests/Application/UpdateCheckResultTests.cs` | DTO construction |
| `tests/Rentier.UnitTests/Desktop/MainWindowViewModel_UpdateTests.cs` | Update state machine, command enable/disable |
| `tests/Rentier.Infrastructure.Tests/Updates/VelopackUpdateServiceTests.cs` | Service behavior with mocked UpdateManager |

## Dev/Debug Mode

When running from the IDE (not packaged by Velopack):
- `VelopackApp.Build().Run()` is a no-op (no installer arguments present)
- `IUpdateService.IsInstalled` returns `false`
- No update check is performed
- No notification bar appears

To test the notification bar UI during development, consider a design-time mock that returns `UpdateAvailable` state.

## Architecture Compliance Checklist

- [ ] `IUpdateService` defined in `Rentier.Application.Interfaces` (not Desktop)
- [ ] `VelopackUpdateService` in `Rentier.Infrastructure` (not Desktop)
- [ ] Desktop only references `IUpdateService` — never `Velopack` namespace directly
- [ ] All update operations are `async Task` — no `.Result` or `.Wait()`
- [ ] UI updates use `RxApp.MainThreadScheduler` — no direct dispatcher calls
- [ ] ReactiveCommand used for all update actions
- [ ] String resources in `Strings.resx` — no hardcoded UI text
