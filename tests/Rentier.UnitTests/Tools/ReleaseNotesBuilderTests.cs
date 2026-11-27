using FluentAssertions;
using Rentier.Application.ReleaseNotes;
using Rentier.ReleaseNotes;
using Xunit;

namespace Rentier.UnitTests.Tools;

public class ReleaseNotesBuilderTests
{
    private static string Body(string en, string sr) => $"## Release note\nEN: {en}\nSR: {sr}\n";

    [Fact]
    public void VersionsToCover_TakesNewVersionAndLatestTagsUpToTen()
    {
        var tags = Enumerable.Range(0, 15).Select(i => $"v2.0.{i}").Append("v2.1.0").Append("not-a-tag");

        var versions = ReleaseNotesBuilder.VersionsToCover("2.2.0", tags, since: "1.4.0");

        versions.Should().HaveCount(ReleaseNotesDocument.MAX_VERSIONS);
        versions[0].Should().Be(new Version(2, 2, 0));
        versions[1].Should().Be(new Version(2, 1, 0));
        versions[^1].Should().Be(new Version(2, 0, 7));
    }

    [Fact]
    public void VersionsToCover_LeavesOutVersionsUpToSince()
    {
        ReleaseNotesBuilder.VersionsToCover("1.6.0", ["v1.3.1", "v1.4.0", "v1.5.0"], since: "1.4.0")
            .Should().Equal(new Version(1, 6, 0), new Version(1, 5, 0));
    }

    [Fact]
    public void VersionsToCover_FirstReleaseWithNotes_CoversOnlyItself()
    {
        ReleaseNotesBuilder.VersionsToCover("1.5.0", ["v1.3.1", "v1.4.0"], since: "v1.4.0")
            .Should().Equal(new Version(1, 5, 0));
    }

    [Fact]
    public void VersionsToCover_NewVersionAlreadyTagged_IsNotDuplicated()
    {
        ReleaseNotesBuilder.VersionsToCover("v1.6.0", ["v1.6.0", "v1.5.0"], since: "1.4.0")
            .Should().Equal(new Version(1, 6, 0), new Version(1, 5, 0));
    }

    [Fact]
    public void VersionsToCover_IgnoresTagsNewerThanTheRelease()
    {
        ReleaseNotesBuilder.VersionsToCover("1.6.0", ["v1.7.0", "v1.5.0"], since: "1.4.0")
            .Should().Equal(new Version(1, 6, 0), new Version(1, 5, 0));
    }

    [Theory]
    [InlineData("dev", "1.4.0")]
    [InlineData("1.5.0", "")]
    public void VersionsToCover_InvalidVersion_Throws(string newVersion, string since)
    {
        var act = () => ReleaseNotesBuilder.VersionsToCover(newVersion, [], since);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Build_CombinesEveryPrNoteOfAVersionNewestVersionFirst()
    {
        var versions = new[] { new Version(1, 6, 0), new Version(1, 5, 0) };

        var result = ReleaseNotesBuilder.Build(versions, v => v == new Version(1, 6, 0)
            ? [Body("Six A", "Šest A"), "## Summary\nInternal only.", Body("Six B", "Šest B")]
            : [Body("Five", "Pet")]);

        result.Entries.Select(e => e.DisplayVersion).Should().Equal("v1.6.0", "v1.5.0");
        result.Entries[0].English.Should().Be("- Six A\n- Six B");
        result.Entries[0].Serbian.Should().Be("- Šest A\n- Šest B");
        result.WithoutUserFacingChanges.Should().BeEmpty();
    }

    [Fact]
    public void Build_VersionWhosePrsHaveNoNotes_GetsGenericLineAndIsReported()
    {
        var result = ReleaseNotesBuilder.Build([new Version(1, 6, 0), new Version(1, 5, 0)],
            v => v == new Version(1, 6, 0) ? ["chore: bump"] : [Body("Five", "Pet")]);

        result.Entries[0].Should().Be(ReleaseNotesBuilder.GenericEntry(new Version(1, 6, 0)));
        result.WithoutUserFacingChanges.Should().Equal(new Version(1, 6, 0));
    }

    [Fact]
    public void GenericEntry_HasBothLanguages()
    {
        var entry = ReleaseNotesBuilder.GenericEntry(new Version(2, 0, 0));

        entry.English.Should().NotBeNullOrWhiteSpace();
        entry.Serbian.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Build_ThenFormat_IsReadableByTheApp()
    {
        var entries = ReleaseNotesBuilder.Build([new Version(1, 6, 0), new Version(1, 5, 0)],
            _ => [Body("Note", "Beleška")]).Entries;

        var parsed = ReleaseNotesDocument.Parse(ReleaseNotesDocument.Format(entries));

        parsed.Should().BeEquivalentTo(entries, o => o.WithStrictOrdering());
    }
}
