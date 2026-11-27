using FluentAssertions;
using Rentier.Application.ReleaseNotes;
using Rentier.ReleaseNotes;
using Xunit;

namespace Rentier.UnitTests.Tools;

public class ReleaseNotesBuilderTests
{
    private static readonly IReadOnlyList<ReleaseNotesEntry> History =
    [
        new(new Version(1, 4, 0), "- Four", "- Četiri"),
        new(new Version(1, 3, 0), "- Three", "- Tri"),
    ];

    private static string Body(string en, string sr) => $"## Release note\nEN: {en}\nSR: {sr}\n";

    [Fact]
    public void VersionsToCover_TakesNewVersionAndLatestTagsUpToTen()
    {
        var tags = Enumerable.Range(0, 15).Select(i => $"v1.0.{i}").Append("v1.1.0").Append("not-a-tag");

        var versions = ReleaseNotesBuilder.VersionsToCover("1.2.0", tags);

        versions.Should().HaveCount(ReleaseNotesDocument.MAX_VERSIONS);
        versions[0].Should().Be(new Version(1, 2, 0));
        versions[1].Should().Be(new Version(1, 1, 0));
        versions[^1].Should().Be(new Version(1, 0, 7));
    }

    [Fact]
    public void VersionsToCover_NewVersionAlreadyTagged_IsNotDuplicated()
    {
        ReleaseNotesBuilder.VersionsToCover("v1.2.0", ["v1.2.0", "v1.1.0"])
            .Should().Equal(new Version(1, 2, 0), new Version(1, 1, 0));
    }

    [Fact]
    public void VersionsToCover_IgnoresTagsNewerThanTheRelease()
    {
        ReleaseNotesBuilder.VersionsToCover("1.2.0", ["v1.3.0", "v1.1.0"])
            .Should().Equal(new Version(1, 2, 0), new Version(1, 1, 0));
    }

    [Fact]
    public void VersionsToCover_InvalidNewVersion_Throws()
    {
        var act = () => ReleaseNotesBuilder.VersionsToCover("dev", []);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void VersionsNeedingPrNotes_OnlyVersionsNewerThanHistory()
    {
        var versions = new[] { new Version(1, 6, 0), new Version(1, 5, 0), new Version(1, 4, 0), new Version(1, 3, 5) };

        ReleaseNotesBuilder.VersionsNeedingPrNotes(History, versions)
            .Should().Equal(new Version(1, 6, 0), new Version(1, 5, 0));
    }

    [Fact]
    public void Build_CombinesHistoryAndPrNotesNewestFirst()
    {
        var versions = new[] { new Version(1, 5, 0), new Version(1, 4, 0), new Version(1, 3, 5), new Version(1, 3, 0) };

        var entries = ReleaseNotesBuilder.Build(History, versions, v => v == new Version(1, 5, 0)
            ? [Body("Five A", "Pet A"), "## Summary\nInternal only.", Body("Five B", "Pet B")]
            : throw new InvalidOperationException("history versions must not read PRs"));

        entries.Select(e => e.DisplayVersion).Should().Equal("v1.5.0", "v1.4.0", "v1.3.0");
        entries[0].English.Should().Be("- Five A\n- Five B");
        entries[0].Serbian.Should().Be("- Pet A\n- Pet B");
        entries[1].Should().Be(History[0]);
    }

    [Fact]
    public void Build_VersionWhosePrsHaveNoNotes_IsLeftOut()
    {
        var entries = ReleaseNotesBuilder.Build(History, [new Version(1, 5, 0), new Version(1, 4, 0)], _ => ["chore: bump"]);

        entries.Select(e => e.DisplayVersion).Should().Equal("v1.4.0");
    }

    [Fact]
    public void Build_ThenFormat_IsReadableByTheApp()
    {
        var entries = ReleaseNotesBuilder.Build(History, [new Version(1, 5, 0), new Version(1, 4, 0)],
            _ => [Body("Five", "Pet")]);

        var parsed = ReleaseNotesDocument.Parse(ReleaseNotesDocument.Format(entries));

        parsed.Should().BeEquivalentTo(entries, o => o.WithStrictOrdering());
    }
}
