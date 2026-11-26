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

    private static string InstallerScript() => ReadRepoFile(".github/installers/rentier-setup.iss");

    /// <summary>The body of one workflow step, from its <c>- name:</c> line to the next step.</summary>
    private static string WorkflowStep(string name)
    {
        var step = Regex.Match(ReleaseWorkflow(),
            $@"- name: {Regex.Escape(name)}\n(?<body>(?:(?!\n\s*- name:).)*)", RegexOptions.Singleline);
        step.Success.Should().BeTrue($"auto-release.yml should have a '{name}' step");
        return step.Groups["body"].Value;
    }

    /// <summary>The file names the release job requires, e.g. <c>Rentier-${VERSION}-win-x64.zip</c>.</summary>
    private static IReadOnlyList<string> ExpectedReleaseAssets()
    {
        var list = Regex.Match(WorkflowStep("Verify all platform artifacts present"),
            @"expected=\((?<items>[^)]*)\)", RegexOptions.Singleline);
        list.Success.Should().BeTrue("the verify step should list the expected assets in expected=( )");
        return Regex.Matches(list.Groups["items"].Value, "\"([^\"]+)\"").Select(m => m.Groups[1].Value).ToList();
    }

    /// <summary>Every <c>Name:</c> entry of one <c>[Section]</c> of the installer script.</summary>
    private static IReadOnlyList<string> InstallerEntryNames(string section)
    {
        var body = Regex.Match(InstallerScript(),
            $@"^\[{section}\]\n(?<body>(?:(?!^\[).)*)", RegexOptions.Singleline | RegexOptions.Multiline);
        body.Success.Should().BeTrue($"rentier-setup.iss should have an [{section}] section");
        return Regex.Matches(body.Groups["body"].Value, @"Name:\s*""([^""]+)""").Select(m => m.Groups[1].Value).ToList();
    }

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
        // The per-user installer detects an all-users install of an earlier version by its HKLM
        // uninstall key, which is named after AppId. If the two drift apart, the check silently
        // never fires and the installer creates a second copy again.
        var script = InstallerScript();
        var appId = Regex.Match(script, @"^AppId=\{(\{[0-9A-Fa-f-]+\})", RegexOptions.Multiline);
        var uninstallKey = Regex.Match(script, @"ClassicUninstallKey = '[^']*\\Uninstall\\(\{[0-9A-Fa-f-]+\})_is1'");

        appId.Success.Should().BeTrue("rentier-setup.iss should define AppId");
        uninstallKey.Groups[1].Value.Should().Be(appId.Groups[1].Value);
    }

    [Fact]
    public void ReleaseWorkflow_ExpectedAssets_IncludeNoPackageThatCannotUpdateItself()
    {
        // A .deb installs root-owned files the app cannot replace, and a plain archive has no
        // Velopack layout: neither can ever update itself, so neither may ship again.
        ExpectedReleaseAssets().Should().NotContain(name =>
            name.EndsWith(".deb", StringComparison.Ordinal) || name.EndsWith(".tar.gz", StringComparison.Ordinal));
    }

    [Fact]
    public void ReleaseNotes_ListEveryExpectedAsset()
    {
        // The download table is how users pick a file; it must not drift from what is published.
        var notes = WorkflowStep("Generate release notes");

        ExpectedReleaseAssets().Should().OnlyContain(name => notes.Contains(name, StringComparison.Ordinal));
    }

    [Fact]
    public void ReleaseWorkflow_SmokeTestsTheInstaller()
    {
        // setup.exe is the main Windows download: it must be installed and checked before release.
        WorkflowStep("Install, smoke test and uninstall the installer")
            .Should().Contain("-Filter \"Rentier-*-win-x64-setup.exe\"");
    }

    [Fact]
    public void Installer_OutputName_IsAnExpectedReleaseAsset()
    {
        var baseName = Regex.Match(InstallerScript(), @"^OutputBaseFilename=(\S+)$", RegexOptions.Multiline);
        baseName.Success.Should().BeTrue("rentier-setup.iss should define OutputBaseFilename");

        var fileName = baseName.Groups[1].Value.Replace("{#AppVersion}", "${VERSION}") + ".exe";

        ExpectedReleaseAssets().Should().Contain(fileName);
    }

    [Fact]
    public void Installer_PrivilegesRequired_IsLowest()
    {
        // The app replaces its own files when it updates, which it cannot do in Program Files.
        InstallerScript().Should().MatchRegex(@"(?m)^PrivilegesRequired=lowest$");
    }

    [Fact]
    public void Installer_DoesNotOfferAnAllUsersInstall()
    {
        // PrivilegesRequiredOverridesAllowed would bring back the "install for all users" choice,
        // and with it an install the app cannot update.
        InstallerScript().Should().NotContain("PrivilegesRequiredOverridesAllowed");
    }

    [Fact]
    public void Installer_DefaultDirName_IsThePerUserProgramsFolder()
    {
        // For a per-user install {autopf} is %LocalAppData%\Programs, so the install folder is
        // never the user data folder (%LocalAppData%\Rentier), and the [InstallDelete] entries
        // for {app} can never reach the user's database.
        InstallerScript().Should().MatchRegex(@"(?m)^DefaultDirName=\{autopf\}\\Rentier$");
    }

    [Fact]
    public void Installer_Shortcuts_StartTheVelopackLauncher()
    {
        // Rentier.exe is Velopack's launcher stub: it survives updates and always starts the
        // current version. A shortcut to anything else breaks after the first update.
        InstallerScript().Should().MatchRegex(@"(?m)^#define MainExe ""Rentier\.exe""$");
    }

    [Theory]
    [InlineData("InstallDelete")]
    [InlineData("UninstallDelete")]
    public void Installer_DeleteEntries_StayInsideTheInstallFolder(string section)
    {
        // The user's tax database lives outside {app}; a delete entry anywhere else could reach it.
        InstallerEntryNames(section).Should().OnlyContain(name => name.StartsWith(@"{app}\", StringComparison.Ordinal));
    }
}
