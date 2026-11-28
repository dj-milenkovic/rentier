using FluentAssertions;
using Rentier.ReleaseNotes;
using Xunit;

namespace Rentier.ReleaseNotes.Tests;

public class PrTitleTests
{
    [Theory]
    [InlineData("feat: What's new page")]
    [InlineData("fix: wrong deadline")]
    [InlineData("perf: faster sync")]
    [InlineData("feat(desktop): new page")]
    [InlineData("fix(update)!: change feed")]
    [InlineData("fix(ci,desktop): mixed scopes")]
    [InlineData("feat(release/desktop): mixed scopes")]
    public void IsUserFacing_FeatFixPerfWithoutOnlyInternalScopes_IsTrue(string title)
    {
        PrTitle.IsUserFacing(title).Should().BeTrue();
    }

    [Theory]
    [InlineData("fix(ci): flaky job")]
    [InlineData("fix(deps): upgrade MailKit")]
    [InlineData("feat(build): new target")]
    [InlineData("feat(release): ship only Velopack")]
    [InlineData("fix(test): race")]
    [InlineData("fix(tests): race")]
    [InlineData("fix(docs): typo")]
    [InlineData("fix(CI): upper case scope")]
    [InlineData("fix(ci, deps): two internal scopes")]
    [InlineData("chore: Bump Avalonia from 12.1.1 to 12.1.2")]
    [InlineData("ci: faster builds")]
    [InlineData("refactor(desktop): split view model")]
    [InlineData("docs: guide")]
    [InlineData("test: more cases")]
    [InlineData("not a conventional title")]
    [InlineData("")]
    [InlineData(null)]
    public void IsUserFacing_InternalTypesAndScopes_IsFalse(string? title)
    {
        PrTitle.IsUserFacing(title).Should().BeFalse();
    }
}
