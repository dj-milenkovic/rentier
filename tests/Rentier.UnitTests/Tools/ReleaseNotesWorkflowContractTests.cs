using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace Rentier.UnitTests.Tools;

/// <summary>
/// Guards how the workflows deliver release notes: built once per release, embedded in every
/// platform's app, packed into every update, and checked on every PR.
/// </summary>
public class ReleaseNotesWorkflowContractTests
{
    private const string NOTES_FILE = "release-notes-app/release-notes.md";

    private static string ReleaseWorkflow() => RepositoryFiles.Read(".github/workflows/auto-release.yml");

    private static IReadOnlyList<string> Commands(string command) =>
        Regex.Matches(ReleaseWorkflow(), $@"{Regex.Escape(command)}\n(?<args>(?:[ ]+-.*\n)+)")
            .Select(m => m.Groups["args"].Value)
            .ToList();

    [Fact]
    public void EveryVpkPack_PacksTheReleaseNotes()
    {
        var packs = Commands("dotnet vpk pack");

        packs.Should().HaveCount(3, "Windows, macOS (matrix) and Linux each pack one release");
        packs.Should().OnlyContain(p => p.Contains($"--releaseNotes {NOTES_FILE}"),
            "the update bar's What's new reads the notes Velopack stores in the update feed");
    }

    [Fact]
    public void EveryPublish_EmbedsTheReleaseNotesInTheApp()
    {
        var publishes = Commands("dotnet publish ${{ env.PROJECT_PATH }}");

        publishes.Should().HaveCount(3);
        publishes.Should().OnlyContain(p => p.Contains($"-p:RentierReleaseNotesPath=${{{{ github.workspace }}}}/{NOTES_FILE}"),
            "Settings > What's new reads the notes embedded by Rentier.Infrastructure.csproj");
    }

    [Theory]
    [InlineData("build-windows")]
    [InlineData("build-macos")]
    [InlineData("build-linux")]
    [InlineData("release")]
    public void Jobs_ThatUseTheNotes_NeedTheReleaseNotesJob(string job)
    {
        Regex.Match(ReleaseWorkflow(), $@"\n  {job}:\n(?:    .*\n)*?    needs: \[(?<needs>[^\]]*)\]")
            .Groups["needs"].Value.Split(',', StringSplitOptions.TrimEntries)
            .Should().Contain("release-notes");
    }

    [Fact]
    public void Tag_WaitsForTheReleaseNotes()
    {
        Regex.Match(ReleaseWorkflow(), @"\n  tag:\n(?:    .*\n)*?    needs: \[(?<needs>[^\]]*)\]")
            .Groups["needs"].Value.Split(',', StringSplitOptions.TrimEntries)
            .Should().Contain("release-notes", "a release without user-facing changes must stop before anything is tagged");
    }

    [Fact]
    public void BuildReleaseNotes_PassesTheOverrideInput()
    {
        var workflow = ReleaseWorkflow();

        workflow.Should().Contain("allow_no_user_changes:");
        workflow.Should().Contain("ALLOW_NO_USER_CHANGES: ${{ inputs.allow_no_user_changes }}");
        workflow.Should().Contain("--allow-no-user-changes \"${ALLOW_NO_USER_CHANGES:-false}\"");
    }

    [Fact]
    public void ReleaseNotes_AreBuiltFromPrsSinceTheCutoffOnly()
    {
        var workflow = ReleaseWorkflow();

        workflow.Should().MatchRegex(@"RELEASE_NOTES_SINCE: '\d+\.\d+\.\d+'");
        Regex.Matches(workflow, @"--since ""\$\{RELEASE_NOTES_SINCE\}""").Should().HaveCount(2, "plan and build use the same cutoff");
        workflow.Should().NotContain("history.md", "the notes are a temporary release file, not a committed one");
    }

    [Fact]
    public void NotesArtifact_IsNotPickedUpAsAReleaseAsset()
    {
        var workflow = ReleaseWorkflow();
        var assetPatterns = Regex.Matches(workflow, @"pattern: (?<p>\S+)").Select(m => m.Groups["p"].Value);
        var notesArtifacts = Regex.Matches(workflow, @"name: (?<n>\S*notes\S*)\n").Select(m => m.Groups["n"].Value).Distinct().ToList();

        notesArtifacts.Should().Equal("app-release-notes");
        assetPatterns.Should().NotContain(p => Regex.IsMatch("app-release-notes", "^" + Regex.Escape(p).Replace("\\*", ".*") + "$"));
    }

    [Fact]
    public void FeedVerification_RequiresNotesInEveryFeed()
    {
        ReleaseWorkflow().Should().Contain(".NotesMarkdown",
            "a release whose feeds lack notes would show an update without What's new");
    }

    [Fact]
    public void PrCheck_ReadsTitleAndBodyFromEnvironmentOnly()
    {
        var workflow = RepositoryFiles.Read(".github/workflows/release-note.yml");
        var run = Regex.Match(workflow, @"run: \|\n(?<script>(?:[ ]{10}.*\n?)+)").Groups["script"].Value;

        run.Should().NotBeEmpty();
        run.Should().NotContain("${{", "PR titles and descriptions are untrusted; interpolating them into a script allows injection");
        workflow.Should().Contain("PR_BODY: ${{ github.event.pull_request.body }}");
        workflow.Should().Contain("PR_TITLE: ${{ github.event.pull_request.title }}");
    }

    [Fact]
    public void PullRequestTemplate_HasAnEmptyReleaseNoteSection()
    {
        var template = RepositoryFiles.Read(".github/pull_request_template.md");

        template.Should().Contain("## Release note").And.Contain("\n### EN\n").And.Contain("\n### SR\n");
        Rentier.ReleaseNotes.PrReleaseNote.Parse(template).IsEmpty.Should().BeTrue(
            "an untouched template must not put placeholder text into the app");
    }
}
