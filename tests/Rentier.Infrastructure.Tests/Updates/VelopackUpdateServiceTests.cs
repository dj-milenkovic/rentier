using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Rentier.Infrastructure.Updates;
using Rentier.Tests.Common.Fakes;

namespace Rentier.Infrastructure.Tests.Updates;

/// <summary>
/// Tests for VelopackUpdateService, driven through the internal <see cref="IVelopackManager"/>
/// seam because Velopack's UpdateManager needs a real Velopack install to do anything.
/// </summary>
public class VelopackUpdateServiceTests
{
    private static VelopackUpdateService CreateService(FakeVelopackManager manager)
        => new(manager, NullLogger<VelopackUpdateService>.Instance);

    // ── CheckForUpdatesAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task CheckForUpdatesAsync_WhenNotInstalled_ReturnsFalseWithoutCallingManager()
    {
        // Not a Velopack install (dev run, or a package without the Velopack layout):
        // return immediately, no network call.
        var manager = new FakeVelopackManager(isInstalled: false);
        var service = CreateService(manager);

        var result = await service.CheckForUpdatesAsync(TestContext.Current.CancellationToken);

        result.IsUpdateAvailable.Should().BeFalse();
        manager.CheckCallCount.Should().Be(0);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_WhenNotInstalled_LogsThatTheCheckWasSkipped()
    {
        // Without this line a skipped check is indistinguishable from "up to date" in a user's log,
        // which is how the updater stayed silently broken for every release up to v1.3.0.
        var logger = new CapturingLogger<VelopackUpdateService>();
        var service = new VelopackUpdateService(new FakeVelopackManager(isInstalled: false), logger);

        await service.CheckForUpdatesAsync(TestContext.Current.CancellationToken);

        logger.Entries.Should().ContainSingle(e =>
            e.Level == LogLevel.Information && e.Message.Contains("not a Velopack install"));
    }

    [Fact]
    public async Task CheckForUpdatesAsync_WhenUpdateAvailable_ReturnsTrueWithVersion()
    {
        var manager = new FakeVelopackManager(isInstalled: true, availableVersion: "2.0.0");
        var service = CreateService(manager);

        var result = await service.CheckForUpdatesAsync(TestContext.Current.CancellationToken);

        result.Should().Be(new Rentier.Application.DTOs.UpdateCheckResult(true, "2.0.0"));
    }

    [Fact]
    public async Task CheckForUpdatesAsync_WhenUpdateAvailable_LogsTargetVersion()
    {
        var logger = new CapturingLogger<VelopackUpdateService>();
        var service = new VelopackUpdateService(
            new FakeVelopackManager(isInstalled: true, availableVersion: "2.0.0"), logger);

        await service.CheckForUpdatesAsync(TestContext.Current.CancellationToken);

        logger.Entries.Should().ContainSingle(e =>
            e.Level == LogLevel.Information && e.Message.Contains("2.0.0"));
    }

    [Fact]
    public async Task CheckForUpdatesAsync_WhenNoUpdateAvailable_ReturnsFalse()
    {
        var manager = new FakeVelopackManager(isInstalled: true, availableVersion: null);
        var service = CreateService(manager);

        var result = await service.CheckForUpdatesAsync(TestContext.Current.CancellationToken);

        result.Should().Be(new Rentier.Application.DTOs.UpdateCheckResult(false, null));
    }

    [Fact]
    public async Task CheckForUpdatesAsync_WhenManagerThrows_ReturnsFalse()
    {
        var manager = new FakeVelopackManager(isInstalled: true, throwOnCheck: true);
        var service = CreateService(manager);

        var result = await service.CheckForUpdatesAsync(TestContext.Current.CancellationToken);

        result.Should().Be(new Rentier.Application.DTOs.UpdateCheckResult(false, null));
    }

    [Fact]
    public async Task CheckForUpdatesAsync_WhenManagerThrows_LogsWarningWithException()
    {
        var logger = new CapturingLogger<VelopackUpdateService>();
        var service = new VelopackUpdateService(
            new FakeVelopackManager(isInstalled: true, throwOnCheck: true), logger);

        await service.CheckForUpdatesAsync(TestContext.Current.CancellationToken);

        logger.Entries.Should().ContainSingle(e =>
            e.Level == LogLevel.Warning && e.Exception is InvalidOperationException);
    }

    // ── DownloadUpdateAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task DownloadUpdateAsync_WhenNotInstalled_ReturnsNotInstalledWithoutCallingManager()
    {
        var manager = new FakeVelopackManager(isInstalled: false);
        var service = CreateService(manager);

        var result = await service.DownloadUpdateAsync(_ => { }, TestContext.Current.CancellationToken);

        result.Error.Code.Should().Be(VelopackUpdateService.UPDATE_NOT_INSTALLED);
        manager.DownloadCallCount.Should().Be(0);
    }

    [Fact]
    public async Task DownloadUpdateAsync_WhenNoUpdateWasFound_ReturnsNoPendingWithoutCallingManager()
    {
        // Previously this path silently did nothing, and the UI then offered a restart that
        // could not apply anything.
        var manager = new FakeVelopackManager(isInstalled: true, availableVersion: null);
        var service = CreateService(manager);
        await service.CheckForUpdatesAsync(TestContext.Current.CancellationToken);

        var result = await service.DownloadUpdateAsync(_ => { }, TestContext.Current.CancellationToken);

        result.Error.Code.Should().Be(VelopackUpdateService.UPDATE_NO_PENDING);
        manager.DownloadCallCount.Should().Be(0);
    }

    [Fact]
    public async Task DownloadUpdateAsync_WhenUpdateFound_ReturnsSuccess()
    {
        var manager = new FakeVelopackManager(isInstalled: true, availableVersion: "2.0.0");
        var service = CreateService(manager);
        await service.CheckForUpdatesAsync(TestContext.Current.CancellationToken);

        var result = await service.DownloadUpdateAsync(_ => { }, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task DownloadUpdateAsync_WhenUpdateFound_InvokesProgressCallback()
    {
        var manager = new FakeVelopackManager(isInstalled: true, availableVersion: "2.0.0",
            progressValues: [0, 50, 100]);
        var service = CreateService(manager);
        await service.CheckForUpdatesAsync(TestContext.Current.CancellationToken);

        var progressValues = new List<int>();
        await service.DownloadUpdateAsync(progressValues.Add, TestContext.Current.CancellationToken);

        progressValues.Should().Equal(0, 50, 100);
    }

    [Fact]
    public async Task DownloadUpdateAsync_WhenManagerThrows_ReturnsDownloadFailedWithMessage()
    {
        var manager = new FakeVelopackManager(isInstalled: true, availableVersion: "2.0.0",
            throwOnDownload: true);
        var service = CreateService(manager);
        await service.CheckForUpdatesAsync(TestContext.Current.CancellationToken);

        var result = await service.DownloadUpdateAsync(_ => { }, TestContext.Current.CancellationToken);

        result.Error.Should().Be(new Rentier.Application.Common.Error(
            VelopackUpdateService.UPDATE_DOWNLOAD_FAILED, "Simulated download error"));
    }

    [Fact]
    public async Task DownloadUpdateAsync_WhenManagerThrows_LogsWarningWithException()
    {
        var logger = new CapturingLogger<VelopackUpdateService>();
        var service = new VelopackUpdateService(
            new FakeVelopackManager(isInstalled: true, availableVersion: "2.0.0", throwOnDownload: true),
            logger);
        await service.CheckForUpdatesAsync(TestContext.Current.CancellationToken);

        await service.DownloadUpdateAsync(_ => { }, TestContext.Current.CancellationToken);

        logger.Entries.Should().ContainSingle(e =>
            e.Level == LogLevel.Warning && e.Exception is InvalidOperationException);
    }

    // ── ApplyUpdateAndRestart ─────────────────────────────────────────────────

    [Fact]
    public void ApplyUpdateAndRestart_WhenNotInstalled_ReturnsNotInstalledWithoutCallingManager()
    {
        var manager = new FakeVelopackManager(isInstalled: false);
        var service = CreateService(manager);

        var result = service.ApplyUpdateAndRestart();

        result.Error.Code.Should().Be(VelopackUpdateService.UPDATE_NOT_INSTALLED);
        manager.ApplyCallCount.Should().Be(0);
    }

    [Fact]
    public void ApplyUpdateAndRestart_WhenNothingDownloaded_ReturnsNoPendingWithoutCallingManager()
    {
        var manager = new FakeVelopackManager(isInstalled: true, availableVersion: "2.0.0");
        var service = CreateService(manager);

        var result = service.ApplyUpdateAndRestart();

        result.Error.Code.Should().Be(VelopackUpdateService.UPDATE_NO_PENDING);
        manager.ApplyCallCount.Should().Be(0);
    }

    [Fact]
    public async Task ApplyUpdateAndRestart_WhenDownloadedThisSession_CallsManager()
    {
        var manager = new FakeVelopackManager(isInstalled: true, availableVersion: "2.0.0");
        var service = CreateService(manager);
        await service.CheckForUpdatesAsync(TestContext.Current.CancellationToken);
        await service.DownloadUpdateAsync(_ => { }, TestContext.Current.CancellationToken);

        service.ApplyUpdateAndRestart();

        manager.ApplyCallCount.Should().Be(1);
    }

    [Fact]
    public void ApplyUpdateAndRestart_WhenDownloadedInEarlierSession_CallsManager()
    {
        // A package downloaded before the last restart is still pending; it must be appliable
        // without checking and downloading again.
        var manager = new FakeVelopackManager(isInstalled: true, pendingRestart: true);
        var service = CreateService(manager);

        service.ApplyUpdateAndRestart();

        manager.ApplyCallCount.Should().Be(1);
    }

    [Fact]
    public void ApplyUpdateAndRestart_WhenManagerThrows_ReturnsApplyFailed()
    {
        var manager = new FakeVelopackManager(isInstalled: true, pendingRestart: true, throwOnApply: true);
        var service = CreateService(manager);

        var result = service.ApplyUpdateAndRestart();

        result.Error.Code.Should().Be(VelopackUpdateService.UPDATE_APPLY_FAILED);
    }

    [Fact]
    public void ApplyUpdateAndRestart_WhenManagerThrows_LogsWarningWithException()
    {
        var logger = new CapturingLogger<VelopackUpdateService>();
        var service = new VelopackUpdateService(
            new FakeVelopackManager(isInstalled: true, pendingRestart: true, throwOnApply: true), logger);

        service.ApplyUpdateAndRestart();

        logger.Entries.Should().ContainSingle(e =>
            e.Level == LogLevel.Warning && e.Exception is InvalidOperationException);
    }

    // ── IDisposable ───────────────────────────────────────────────────────────

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        var service = CreateService(new FakeVelopackManager(isInstalled: false));

        var act = () =>
        {
            service.Dispose();
            service.Dispose();
        };

        act.Should().NotThrow();
    }
}

/// <summary>
/// Test double for <see cref="IVelopackManager"/> that mirrors the real adapter's state:
/// a successful check with a newer version makes an update downloadable, and a successful
/// download makes it appliable.
/// </summary>
internal sealed class FakeVelopackManager : IVelopackManager
{
    private readonly string? _availableVersion;
    private readonly bool _throwOnCheck;
    private readonly bool _throwOnDownload;
    private readonly bool _throwOnApply;
    private readonly int[]? _progressValues;

    public bool IsInstalled { get; }
    public bool HasUpdateToDownload { get; private set; }
    public bool HasUpdateToApply { get; private set; }
    public int CheckCallCount { get; private set; }
    public int DownloadCallCount { get; private set; }
    public int ApplyCallCount { get; private set; }

    public FakeVelopackManager(
        bool isInstalled,
        string? availableVersion = null,
        bool throwOnCheck = false,
        bool throwOnDownload = false,
        bool throwOnApply = false,
        bool pendingRestart = false,
        int[]? progressValues = null)
    {
        IsInstalled = isInstalled;
        _availableVersion = availableVersion;
        _throwOnCheck = throwOnCheck;
        _throwOnDownload = throwOnDownload;
        _throwOnApply = throwOnApply;
        HasUpdateToApply = pendingRestart;
        _progressValues = progressValues;
    }

    public Task<string?> CheckForUpdatesAsync(CancellationToken cancellationToken)
    {
        CheckCallCount++;
        if (_throwOnCheck)
            throw new InvalidOperationException("Simulated network error");
        HasUpdateToDownload = _availableVersion is not null;
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
        HasUpdateToApply = true;
        return Task.CompletedTask;
    }

    public void ApplyUpdatesAndRestart()
    {
        ApplyCallCount++;
        if (_throwOnApply)
            throw new InvalidOperationException("Simulated apply error");
    }
}
