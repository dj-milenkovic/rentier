using FluentAssertions;
using Rentier.Infrastructure.Updates;

namespace Rentier.Infrastructure.Tests.Updates;

/// <summary>
/// Tests for VelopackUpdateService.
///
/// Since Velopack's UpdateManager is a sealed class that cannot be easily mocked,
/// we test the service using the IsInstalled=false guard path (which covers the
/// most important safety behaviors) plus the internal IVelopackManager wrapper
/// abstraction for full download/apply coverage.
/// </summary>
public class VelopackUpdateServiceTests
{
    // ── CheckForUpdatesAsync — IsInstalled guard ──────────────────────────────

    [Fact]
    public async Task CheckForUpdatesAsync_WhenNotInstalled_ReturnsFalseWithoutCallingManager()
    {
        // When the app is not installed (dev mode), the service must return immediately
        // without performing any network call.
        var manager = new FakeVelopackManager(isInstalled: false);
        var service = new VelopackUpdateService(manager);

        var result = await service.CheckForUpdatesAsync(TestContext.Current.CancellationToken);

        result.IsUpdateAvailable.Should().BeFalse();
        result.TargetVersion.Should().BeNull();
        manager.CheckCallCount.Should().Be(0);
    }

    // ── CheckForUpdatesAsync — update available ───────────────────────────────

    [Fact]
    public async Task CheckForUpdatesAsync_WhenUpdateAvailable_ReturnsTrueWithVersion()
    {
        var manager = new FakeVelopackManager(isInstalled: true, availableVersion: "2.0.0");
        var service = new VelopackUpdateService(manager);

        var result = await service.CheckForUpdatesAsync(TestContext.Current.CancellationToken);

        result.IsUpdateAvailable.Should().BeTrue();
        result.TargetVersion.Should().Be("2.0.0");
    }

    // ── CheckForUpdatesAsync — no update available ────────────────────────────

    [Fact]
    public async Task CheckForUpdatesAsync_WhenNoUpdateAvailable_ReturnsFalse()
    {
        var manager = new FakeVelopackManager(isInstalled: true, availableVersion: null);
        var service = new VelopackUpdateService(manager);

        var result = await service.CheckForUpdatesAsync(TestContext.Current.CancellationToken);

        result.IsUpdateAvailable.Should().BeFalse();
        result.TargetVersion.Should().BeNull();
    }

    // ── CheckForUpdatesAsync — exception handling ─────────────────────────────

    [Fact]
    public async Task CheckForUpdatesAsync_WhenNetworkException_ReturnsFalse()
    {
        var manager = new FakeVelopackManager(isInstalled: true, throwOnCheck: true);
        var service = new VelopackUpdateService(manager);

        var result = await service.CheckForUpdatesAsync(TestContext.Current.CancellationToken);

        result.IsUpdateAvailable.Should().BeFalse();
        result.TargetVersion.Should().BeNull();
    }

    // ── DownloadUpdateAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task DownloadUpdateAsync_WhenNotInstalled_IsNoOp()
    {
        var manager = new FakeVelopackManager(isInstalled: false);
        var service = new VelopackUpdateService(manager);

        // Should complete without throwing even when not installed
        await service.DownloadUpdateAsync(_ => { }, TestContext.Current.CancellationToken);

        manager.DownloadCallCount.Should().Be(0);
    }

    [Fact]
    public async Task DownloadUpdateAsync_WhenInstalled_CallsManager()
    {
        var manager = new FakeVelopackManager(isInstalled: true, availableVersion: "2.0.0");
        var service = new VelopackUpdateService(manager);

        // First check so the service knows what to download
        await service.CheckForUpdatesAsync(TestContext.Current.CancellationToken);

        var progressValues = new List<int>();
        await service.DownloadUpdateAsync(p => progressValues.Add(p), TestContext.Current.CancellationToken);

        manager.DownloadCallCount.Should().Be(1);
    }

    [Fact]
    public async Task DownloadUpdateAsync_WhenInstalled_InvokesProgressCallback()
    {
        var manager = new FakeVelopackManager(isInstalled: true, availableVersion: "2.0.0",
            progressValues: [0, 50, 100]);
        var service = new VelopackUpdateService(manager);
        await service.CheckForUpdatesAsync(TestContext.Current.CancellationToken);

        var progressValues = new List<int>();
        await service.DownloadUpdateAsync(p => progressValues.Add(p), TestContext.Current.CancellationToken);

        progressValues.Should().Contain([0, 50, 100]);
    }

    [Fact]
    public async Task DownloadUpdateAsync_WhenManagerThrows_PropagatesException()
    {
        var manager = new FakeVelopackManager(isInstalled: true, availableVersion: "2.0.0",
            throwOnDownload: true);
        var service = new VelopackUpdateService(manager);
        await service.CheckForUpdatesAsync(TestContext.Current.CancellationToken);

        var act = async () => await service.DownloadUpdateAsync(_ => { });

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // ── ApplyUpdateAndRestart ─────────────────────────────────────────────────

    [Fact]
    public void ApplyUpdateAndRestart_WhenNotInstalled_IsNoOp()
    {
        var manager = new FakeVelopackManager(isInstalled: false);
        var service = new VelopackUpdateService(manager);

        // Should not throw
        service.ApplyUpdateAndRestart();

        manager.ApplyCallCount.Should().Be(0);
    }

    [Fact]
    public async Task ApplyUpdateAndRestart_WhenInstalledAndDownloaded_CallsManager()
    {
        var manager = new FakeVelopackManager(isInstalled: true, availableVersion: "2.0.0");
        var service = new VelopackUpdateService(manager);
        await service.CheckForUpdatesAsync(TestContext.Current.CancellationToken);
        await service.DownloadUpdateAsync(_ => { }, TestContext.Current.CancellationToken);

        service.ApplyUpdateAndRestart();

        manager.ApplyCallCount.Should().Be(1);
    }

    // ── ScheduleUpdateOnExit ──────────────────────────────────────────────────

    [Fact]
    public void ScheduleUpdateOnExit_WhenNotInstalled_IsNoOp()
    {
        var manager = new FakeVelopackManager(isInstalled: false);
        var service = new VelopackUpdateService(manager);

        // Should not throw
        service.ScheduleUpdateOnExit();

        manager.ScheduleCallCount.Should().Be(0);
    }

    [Fact]
    public async Task ScheduleUpdateOnExit_WhenInstalledAndDownloaded_CallsManager()
    {
        var manager = new FakeVelopackManager(isInstalled: true, availableVersion: "2.0.0");
        var service = new VelopackUpdateService(manager);
        await service.CheckForUpdatesAsync(TestContext.Current.CancellationToken);
        await service.DownloadUpdateAsync(_ => { }, TestContext.Current.CancellationToken);

        service.ScheduleUpdateOnExit();

        manager.ScheduleCallCount.Should().Be(1);
    }

    // ── IDisposable ───────────────────────────────────────────────────────────

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        var manager = new FakeVelopackManager(isInstalled: false);
        var service = new VelopackUpdateService(manager);

        var act = () =>
        {
            service.Dispose();
            service.Dispose();
        };

        act.Should().NotThrow();
    }
}

/// <summary>
/// Test double that implements IVelopackManager so we can fully control behavior
/// without relying on Velopack's sealed UpdateManager class.
/// </summary>
internal sealed class FakeVelopackManager : IVelopackManager
{
    private readonly string? _availableVersion;
    private readonly bool _throwOnCheck;
    private readonly bool _throwOnDownload;
    private readonly int[]? _progressValues;

    public bool IsInstalled { get; }
    public int CheckCallCount { get; private set; }
    public int DownloadCallCount { get; private set; }
    public int ApplyCallCount { get; private set; }
    public int ScheduleCallCount { get; private set; }

    public FakeVelopackManager(
        bool isInstalled,
        string? availableVersion = null,
        bool throwOnCheck = false,
        bool throwOnDownload = false,
        int[]? progressValues = null)
    {
        IsInstalled = isInstalled;
        _availableVersion = availableVersion;
        _throwOnCheck = throwOnCheck;
        _throwOnDownload = throwOnDownload;
        _progressValues = progressValues;
    }

    public Task<string?> CheckForUpdatesAsync(CancellationToken cancellationToken)
    {
        CheckCallCount++;
        if (_throwOnCheck)
            throw new InvalidOperationException("Simulated network error");
        return Task.FromResult(_availableVersion);
    }

    public Task DownloadUpdatesAsync(Action<int> progress, CancellationToken cancellationToken)
    {
        DownloadCallCount++;
        if (_throwOnDownload)
            throw new InvalidOperationException("Simulated download error");
        if (_progressValues is not null)
            foreach (var p in _progressValues)
                progress(p);
        return Task.CompletedTask;
    }

    public void ApplyUpdatesAndRestart() => ApplyCallCount++;

    public void WaitExitThenApplyUpdates() => ScheduleCallCount++;
}
