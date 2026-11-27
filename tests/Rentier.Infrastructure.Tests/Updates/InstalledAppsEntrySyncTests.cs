using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Rentier.Infrastructure.Updates;
using Rentier.Tests.Common.Fakes;

namespace Rentier.Infrastructure.Tests.Updates;

/// <summary>
/// Tests for <see cref="InstalledAppsEntrySync"/>, driven through the <see cref="IUninstallEntry"/>
/// seam so they run on every OS without touching the real registry.
/// </summary>
public class InstalledAppsEntrySyncTests
{
    private const string InstallDir = @"C:\Users\someone\AppData\Local\Programs\Rentier";

    private static InstalledAppsEntrySync CreateSync(FakeUninstallEntry entry)
        => new(entry, NullLogger<InstalledAppsEntrySync>.Instance);

    /// <summary>The entry exactly as the v1.4.0 installer writes it (InstallLocation ends in '\').</summary>
    private static FakeUninstallEntry InstalledEntry(string version = "1.4.0", string name = "Rentier") => new()
    {
        Values =
        {
            [InstalledAppsEntrySync.INSTALL_LOCATION] = InstallDir + @"\",
            [InstalledAppsEntrySync.DISPLAY_VERSION] = version,
            [InstalledAppsEntrySync.DISPLAY_NAME] = name,
        },
    };

    [Fact]
    public void Sync_AfterAnInAppUpdate_WritesTheNewVersion()
    {
        var entry = InstalledEntry(version: "1.4.0");

        CreateSync(entry).Sync(InstallDir, "1.5.0");

        entry.Values[InstalledAppsEntrySync.DISPLAY_VERSION].Should().Be("1.5.0");
    }

    [Fact]
    public void Sync_AfterAnInAppUpdate_ReturnsTrue()
    {
        var result = CreateSync(InstalledEntry(version: "1.4.0")).Sync(InstallDir, "1.5.0");

        result.Value.Should().BeTrue();
    }

    [Fact]
    public void Sync_WhenTheEntryIsCurrent_WritesNothing()
    {
        // Runs on every start: an up-to-date entry must not be rewritten each time.
        var entry = InstalledEntry(version: "1.5.0");

        CreateSync(entry).Sync(InstallDir, "1.5.0");

        entry.WriteCount.Should().Be(0);
    }

    [Fact]
    public void Sync_WhenNoInstallerEntryExists_WritesNothing()
    {
        // The portable ZIP and dev runs have no uninstall key at all.
        var entry = new FakeUninstallEntry();

        CreateSync(entry).Sync(InstallDir, "1.5.0");

        entry.WriteCount.Should().Be(0);
    }

    [Fact]
    public void Sync_WhenTheEntryBelongsToAnotherFolder_WritesNothing()
    {
        // A portable copy elsewhere must not claim the installed copy's version.
        var entry = InstalledEntry(version: "1.4.0");

        CreateSync(entry).Sync(@"D:\Tools\Rentier", "1.5.0");

        entry.WriteCount.Should().Be(0);
    }

    [Theory]
    [InlineData(@"C:\Users\someone\AppData\Local\Programs\Rentier")]
    [InlineData(@"c:\users\someone\appdata\local\programs\rentier\")]
    public void Sync_WhenTheFolderDiffersOnlyInCaseOrTrailingSeparator_WritesTheVersion(string rootAppDir)
    {
        var entry = InstalledEntry(version: "1.4.0");

        CreateSync(entry).Sync(rootAppDir, "1.5.0");

        entry.Values[InstalledAppsEntrySync.DISPLAY_VERSION].Should().Be("1.5.0");
    }

    [Fact]
    public void Sync_WhenTheNameCarriesAnOldVersion_RenamesItToRentier()
    {
        // Installers up to v1.3.1 registered "Rentier 1.3.1" as the name.
        var entry = InstalledEntry(version: "1.3.1", name: "Rentier 1.3.1");

        CreateSync(entry).Sync(InstallDir, "1.5.0");

        entry.Values[InstalledAppsEntrySync.DISPLAY_NAME].Should().Be("Rentier");
    }

    [Fact]
    public void Sync_WhenTheRegistryThrows_ReturnsFailure()
    {
        // A locked or unreadable key must never stop the app from starting.
        var entry = InstalledEntry(version: "1.4.0");
        entry.ThrowOnWrite = true;

        var result = CreateSync(entry).Sync(InstallDir, "1.5.0");

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void Sync_WhenTheRegistryThrows_LogsAWarningWithoutTheInstallPath()
    {
        // The install path contains the Windows user name.
        var logger = new CapturingLogger<InstalledAppsEntrySync>();
        var entry = InstalledEntry(version: "1.4.0");
        entry.ThrowOnWrite = true;

        new InstalledAppsEntrySync(entry, logger).Sync(InstallDir, "1.5.0");

        logger.Entries.Should().ContainSingle(e =>
            e.Level == LogLevel.Warning && !e.Message.Contains("someone", StringComparison.OrdinalIgnoreCase));
    }

    private sealed class FakeUninstallEntry : IUninstallEntry
    {
        public Dictionary<string, string> Values { get; } = [];

        public int WriteCount { get; private set; }

        public bool ThrowOnWrite { get; set; }

        public string? GetString(string name) => Values.GetValueOrDefault(name);

        public void SetString(string name, string value)
        {
            if (ThrowOnWrite)
                throw new UnauthorizedAccessException("Access to the registry key is denied.");
            Values[name] = value;
            WriteCount++;
        }
    }
}
