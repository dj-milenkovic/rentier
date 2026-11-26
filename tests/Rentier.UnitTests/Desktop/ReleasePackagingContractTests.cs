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

    [Fact]
    public void ReleaseWorkflow_SmokeTestsThatVelopackRecognisesTheLayout()
    {
        // --smoke-test returns before VelopackApp.Run(), so it passes even for a layout Velopack
        // rejects. Only --smoke-test-update catches "IsInstalled == false" before release.
        ReleaseWorkflow().Should().Contain("--smoke-test-update");
    }

    [Fact]
    public void ReleaseWorkflow_DryRun_DoesNotCreateGitHubRelease()
    {
        // A dry run is meant to be safe on any branch: it must never publish a release.
        var releaseStep = Regex.Match(ReleaseWorkflow(),
            @"- name: Create GitHub Release\n(?<body>(?:(?!\n\s*- name:).)*)", RegexOptions.Singleline);

        releaseStep.Success.Should().BeTrue("auto-release.yml should have a Create GitHub Release step");
        releaseStep.Groups["body"].Value.Should().Contain("if: ${{ !inputs.dry_run }}");
    }

    [Fact]
    public void ReleaseWorkflow_DryRun_ExitsBeforePushingTheTag()
    {
        var workflow = ReleaseWorkflow();
        var dryRunExit = workflow.IndexOf("if [ \"${DRY_RUN}\" = \"true\" ]", StringComparison.Ordinal);
        var tagPush = workflow.IndexOf("git push origin \"${NEW_TAG}\"", StringComparison.Ordinal);

        dryRunExit.Should().BeInRange(0, tagPush, "the dry-run exit must come before the tag push");
    }

    [Fact]
    public void VelopackInstaller_AllUsersInstallCheck_UsesTheInstallerAppId()
    {
        // The per-user Velopack installer detects an all-users classic install by its HKLM
        // uninstall key, which is named after AppId. If the two drift apart, the check silently
        // never fires and the installer creates a second copy again.
        var script = ReadRepoFile(".github/installers/rentier-setup.iss");
        var appId = Regex.Match(script, @"^AppId=\{(\{[0-9A-Fa-f-]+\})", RegexOptions.Multiline);
        var uninstallKey = Regex.Match(script, @"ClassicUninstallKey = '[^']*\\Uninstall\\(\{[0-9A-Fa-f-]+\})_is1'");

        appId.Success.Should().BeTrue("rentier-setup.iss should define AppId");
        uninstallKey.Groups[1].Value.Should().Be(appId.Groups[1].Value);
    }
}
