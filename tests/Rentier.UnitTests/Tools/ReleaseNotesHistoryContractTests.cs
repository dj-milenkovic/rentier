using System.Text.RegularExpressions;
using FluentAssertions;
using Rentier.Application.ReleaseNotes;
using Xunit;

namespace Rentier.UnitTests.Tools;

/// <summary>
/// Guards <c>.github/release-notes/history.md</c>: it is embedded in local builds and merged into every
/// release's notes, so a malformed entry would silently vanish from the app.
/// </summary>
public class ReleaseNotesHistoryContractTests
{
    private static string History() => RepositoryFiles.Read(".github/release-notes/history.md");

    [Fact]
    public void History_EveryVersionHeading_ParsesToAnEntry()
    {
        var headings = Regex.Matches(History(), @"^## v\S+\s*$", RegexOptions.Multiline).Count;

        ReleaseNotesDocument.Parse(History()).Should().HaveCount(headings,
            "every '## vX.Y.Z' block needs a valid, unique version and at least one note");
    }

    [Fact]
    public void History_EveryVersion_HasEnglishAndSerbian()
    {
        ReleaseNotesDocument.Parse(History()).Should().OnlyContain(
            e => !string.IsNullOrWhiteSpace(e.English) && !string.IsNullOrWhiteSpace(e.Serbian));
    }

    [Fact]
    public void History_IsNewestFirst()
    {
        var versions = Regex.Matches(History(), @"^## v(\S+)\s*$", RegexOptions.Multiline)
            .Select(m => Version.Parse(m.Groups[1].Value))
            .ToList();

        versions.Should().BeInDescendingOrder();
    }
}
