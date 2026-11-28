using FluentAssertions;
using Rentier.ReleaseNotes;
using Xunit;

namespace Rentier.ReleaseNotes.Tests;

public class ReleaseNotesCliTests
{
    private static (int ExitCode, string Output) Check(string title, string body)
    {
        var output = new StringWriter();
        var exitCode = Cli.Check(title, body, output);
        return (exitCode, output.ToString());
    }

    [Fact]
    public void Check_BothLanguages_Passes()
    {
        var (exitCode, output) = Check("feat: x", "## Release note\nEN: One\nSR: Jedan\n");

        exitCode.Should().Be(0);
        output.Should().NotContain("::error").And.NotContain("::warning");
    }

    [Fact]
    public void Check_MissingSerbian_Fails()
    {
        var (exitCode, output) = Check("fix: x", "## Release note\nEN: One\n");

        exitCode.Should().Be(1);
        output.Should().Contain("::error");
    }

    [Theory]
    [InlineData("feat: new page")]
    [InlineData("fix(ui): button")]
    [InlineData("perf!: faster")]
    public void Check_UserFacingTitleWithoutNote_Fails(string title)
    {
        var (exitCode, output) = Check(title, "## Summary\nNothing for users.");

        exitCode.Should().Be(1);
        output.Should().Contain("::error").And.Contain("fix(ci)");
    }

    [Fact]
    public void Check_UserFacingTitleWithUntouchedTemplate_Fails()
    {
        var template = RepositoryFiles.Read(".github/pull_request_template.md");

        Check("feat: x", template).ExitCode.Should().Be(1);
    }

    [Theory]
    [InlineData("chore: Bump Avalonia")]
    [InlineData("ci: faster builds")]
    [InlineData("refactor(desktop): split view model")]
    [InlineData("fix(ci): flaky job")]
    [InlineData("feat(deps): new package")]
    public void Check_InternalTitleWithoutNote_PassesQuietly(string title)
    {
        var (exitCode, output) = Check(title, "Dependabot body");

        exitCode.Should().Be(0);
        output.Should().NotContain("::warning").And.NotContain("::error");
    }

    [Fact]
    public void Check_InternalTitleWithNoteInOneLanguage_StillFails()
    {
        Check("fix(ci): x", "## Release note\nEN: Only English\n").ExitCode.Should().Be(1);
    }

    // ── build: a release without user-facing changes ────────────────────────────

    private static readonly Version[] Versions = [new(1, 6, 0), new(1, 5, 0)];

    private static IEnumerable<PrBody> Prs(params string[] bodies) => bodies.Select((b, i) => new PrBody(i + 1, b));

    private static IEnumerable<PrBody> FiveHasNotes(Version v) =>
        v == new Version(1, 5, 0) ? Prs("## Release note\nEN: Five\nSR: Pet\n") : Prs("chore: Bump x");

    [Fact]
    public void BuildDocument_NewVersionWithoutNotes_StopsTheRelease()
    {
        var output = new StringWriter();

        var exitCode = Cli.BuildDocument(Versions, FiveHasNotes, allowNoUserChanges: false, output, out var document);

        exitCode.Should().Be(1);
        document.Should().BeEmpty();
        output.ToString().Should().Contain("Release without user-facing changes");
    }

    [Fact]
    public void BuildDocument_NewVersionWithoutNotesButAllowed_WritesGenericLine()
    {
        var exitCode = Cli.BuildDocument(Versions, FiveHasNotes, allowNoUserChanges: true, new StringWriter(), out var document);

        exitCode.Should().Be(0);
        var newest = Rentier.Application.ReleaseNotes.ReleaseNotesDocument.Parse(document)[0];
        newest.DisplayVersion.Should().Be("v1.6.0");
        newest.Should().Be(ReleaseNotesBuilder.GenericEntry(new Version(1, 6, 0)));
    }

    [Fact]
    public void BuildDocument_OlderVersionWithoutNotes_DoesNotStopTheRelease()
    {
        // An older version without notes was itself released with the override.
        var exitCode = Cli.BuildDocument(Versions, v => v == new Version(1, 6, 0) ? Prs("## Release note\nEN: Six\nSR: Šest\n") : [],
            allowNoUserChanges: false, new StringWriter(), out var document);

        exitCode.Should().Be(0);
        document.Should().Contain("Stability and security improvements.");
    }

    [Fact]
    public void BuildDocument_NewVersionWithNotes_Succeeds()
    {
        var exitCode = Cli.BuildDocument(Versions, _ => Prs("## Release note\nEN: Note\nSR: Beleška\n"),
            allowNoUserChanges: false, new StringWriter(), out var document);

        exitCode.Should().Be(0);
        document.Should().StartWith("## v1.6.0").And.Contain("- Note").And.Contain("## v1.5.0");
    }

    [Fact]
    public void Run_UnknownCommand_PrintsUsage()
    {
        var output = new StringWriter();

        Cli.Run(["publish"], output).Should().Be(2);
        output.ToString().Should().Contain("Usage");
    }

    [Fact]
    public void Run_MissingOption_ReportsError()
    {
        var output = new StringWriter();

        Cli.Run(["check", "--title", "feat: x"], output).Should().Be(2);
        output.ToString().Should().Contain("Missing --body-file");
    }
}
