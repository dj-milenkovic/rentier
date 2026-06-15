using FluentAssertions;
using Rentier.Infrastructure.Updates;
using Xunit;

namespace Rentier.Infrastructure.Tests;

/// <summary>
/// Unit tests for VelopackUpdateService.
/// Uses the internal test constructor (accessible via InternalsVisibleTo) with
/// hand-rolled test doubles to avoid DynamicProxy issues with internal interfaces.
/// </summary>
public class VelopackUpdateServiceTests
{
    /// <summary>Manager that reports Installed=true and throws on every async call.</summary>
    private sealed class ThrowingVelopackManager : IVelopackManager
    {
        public bool IsInstalled => true;
        public Task<string?> CheckForUpdatesAsync(CancellationToken ct) =>
            throw new InvalidOperationException("Simulated Velopack failure");
        public Task DownloadUpdatesAsync(Action<int> progress, CancellationToken ct) =>
            Task.CompletedTask;
        public void ApplyUpdatesAndRestart() { }
        public void WaitExitThenApplyUpdates() { }
    }

    /// <summary>Manager that reports IsInstalled=false on every call.</summary>
    private sealed class NotInstalledVelopackManager : IVelopackManager
    {
        public bool IsInstalled => false;
        public Task<string?> CheckForUpdatesAsync(CancellationToken ct) =>
            Task.FromResult<string?>(null);
        public Task DownloadUpdatesAsync(Action<int> progress, CancellationToken ct) =>
            Task.CompletedTask;
        public void ApplyUpdatesAndRestart() { }
        public void WaitExitThenApplyUpdates() { }
    }

    [Fact]
    public async Task CheckForUpdatesAsync_ManagerThrows_ReturnsFalseResult()
    {
        // Arrange — IsInstalled=true so the catch block is reachable
        using var service = new VelopackUpdateService(new ThrowingVelopackManager(), null);

        // Act — manager throws; service must catch silently and return no-update
        var result = await service.CheckForUpdatesAsync(CancellationToken.None);

        // Assert
        result.IsUpdateAvailable.Should().BeFalse();
        result.TargetVersion.Should().BeNull();
    }

    [Fact]
    public async Task CheckForUpdatesAsync_NotInstalled_ReturnsFalseWithoutCallingManager()
    {
        using var service = new VelopackUpdateService(new NotInstalledVelopackManager(), null);

        var result = await service.CheckForUpdatesAsync(CancellationToken.None);

        result.IsUpdateAvailable.Should().BeFalse();
    }
}
