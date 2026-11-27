using FluentAssertions;
using Xunit;

namespace Rentier.UnitTests.Tools;

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
    public void Check_UserFacingTypeWithoutNote_WarnsButPasses(string title)
    {
        var (exitCode, output) = Check(title, "## Summary\nNothing for users.");

        exitCode.Should().Be(0);
        output.Should().Contain("::warning");
    }

    [Theory]
    [InlineData("chore: Bump Avalonia")]
    [InlineData("ci: faster builds")]
    [InlineData("refactor(desktop): split view model")]
    public void Check_InternalTypeWithoutNote_PassesQuietly(string title)
    {
        var (exitCode, output) = Check(title, "Dependabot body");

        exitCode.Should().Be(0);
        output.Should().NotContain("::warning").And.NotContain("::error");
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
