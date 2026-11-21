using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using FluentAssertions;
using Rentier.Desktop.Composition;
using Xunit;

namespace Rentier.UnitTests.Desktop;

/// <summary>
/// Guards the release packaging decisions that keep Velopack auto-update working and user data
/// safe. These read the repository's build files, so a change to the workflow that breaks the
/// contract fails here instead of in a shipped release.
/// </summary>
public class ReleasePackagingContractTests
{
    private static string ReadRepoFile(string relativePath) => RepositoryFiles.Read(relativePath);

    private static string ReleaseWorkflow() => ReadRepoFile(".github/workflows/auto-release.yml");

    private static string WorkflowEnv(string name)
    {
        var match = Regex.Match(ReleaseWorkflow(), $@"^\s*{name}:\s*(\S+)\s*$", RegexOptions.Multiline);
        match.Success.Should().BeTrue($"auto-release.yml should define the {name} env variable");
        return match.Groups[1].Value;
    }

    [Fact]
    public void VpkToolVersion_MatchesVelopackPackageVersion()
    {
        // Velopack requires the vpk that builds the packages to match the library in the app.
        var tools = JsonDocument.Parse(ReadRepoFile(".config/dotnet-tools.json"));
        var vpkVersion = tools.RootElement.GetProperty("tools").GetProperty("vpk").GetProperty("version").GetString();

        var packages = XDocument.Parse(ReadRepoFile("Directory.Packages.props"));
        var velopackVersion = packages.Descendants("PackageVersion")
            .Single(e => (string?)e.Attribute("Include") == "Velopack")
            .Attribute("Version")!.Value;

        vpkVersion.Should().Be(velopackVersion);
    }

    [Fact]
    public void PackId_IsTheDataFolderName()
    {
        // Documents why the next test matters: the Velopack package id names the folder
        // Velopack's own installer would use, and it equals the user data folder's name.
        WorkflowEnv("PACK_ID").Should().BeEquivalentTo(Path.GetFileName(AppPaths.RootDirectory));
    }

    [Fact]
    public void WindowsVelopackPack_SkipsVelopackInstaller()
    {
        // Velopack's own Setup.exe installs to %LocalAppData%\<packId> and replaces that whole
        // folder on install (and deletes it on uninstall). With packId "Rentier" that folder is
        // AppPaths.RootDirectory — the user's tax database. The Windows pack must therefore skip
        // Velopack's installer (--noInst) and ship its portable layout inside InnoSetup.
        var windowsPack = Regex.Match(ReleaseWorkflow(),
            @"dotnet vpk pack(?:(?!\n\s*- ).)*?--runtime win-x64(?:(?!\n\s*- ).)*", RegexOptions.Singleline);

        windowsPack.Success.Should().BeTrue("auto-release.yml should pack a win-x64 Velopack release");
        windowsPack.Value.Should().Contain("--noInst");
    }

    [Fact]
    public void ReleaseWorkflow_PublishesVelopackFeedFiles()
    {
        // Without releases.<channel>.json in the GitHub release, installed apps never see updates.
        ReleaseWorkflow().Should().Contain("dist/releases.*.json");
    }
}
