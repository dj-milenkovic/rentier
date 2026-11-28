using FluentAssertions;
using Rentier.Application.ReleaseNotes;
using Rentier.ReleaseNotes;
using Xunit;

namespace Rentier.UnitTests.Tools;

public class ReleaseNotesBuilderTests
{
    private static string Body(string en, string sr) => $"## Release note\nEN: {en}\nSR: {sr}\n";

    /// <summary>PR descriptions numbered #1, #2, ... in order.</summary>
    private static IEnumerable<PrBody> Prs(params string[] bodies) => bodies.Select((b, i) => new PrBody(i + 1, b));

    private static readonly Version V160 = new(1, 6, 0);
    private static readonly Version V150 = new(1, 5, 0);

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
            ? Prs(Body("Six A", "Šest A"), "## Summary\nInternal only.", Body("Six B", "Šest B"))
            : Prs(Body("Five", "Pet")));

        result.Entries.Select(e => e.DisplayVersion).Should().Equal("v1.6.0", "v1.5.0");
        result.Entries[0].English.Should().Be("- Six A\n- Six B");
        result.Entries[0].Serbian.Should().Be("- Šest A\n- Šest B");
        result.WithoutUserFacingChanges.Should().BeEmpty();
    }

    [Fact]
    public void Build_VersionWhosePrsHaveNoNotes_GetsGenericLineAndIsReported()
    {
        var result = ReleaseNotesBuilder.Build([new Version(1, 6, 0), new Version(1, 5, 0)],
            v => v == new Version(1, 6, 0) ? Prs("chore: bump") : Prs(Body("Five", "Pet")));

        result.Entries[0].Should().Be(ReleaseNotesBuilder.GenericEntry(new Version(1, 6, 0)));
        result.WithoutUserFacingChanges.Should().Equal(new Version(1, 6, 0));
    }

    [Theory]
    [InlineData("Reverts #1")]
    [InlineData("Reverts dj-milenkovic/rentier#1")]
    [InlineData("This reverts the update bar.\n\nreverts #1")]
    public void Build_PrRevertedInSameVersion_LeavesOutItsNote(string revertBody)
    {
        var result = ReleaseNotesBuilder.Build([V160],
            _ => Prs(Body("Reverted feature", "Vraćena funkcija"), Body("Kept", "Zadržano"), revertBody));

        result.Entries[0].English.Should().Be("- Kept");
    }

    [Fact]
    public void Build_OnlyPrOfVersionReverted_GetsGenericLine()
    {
        var result = ReleaseNotesBuilder.Build([V160], _ => Prs(Body("Reverted", "Vraćeno"), "Reverts #1"));

        result.WithoutUserFacingChanges.Should().Equal(V160);
    }

    [Fact]
    public void Build_RevertOfPrFromOlderVersion_KeepsTheOlderNote()
    {
        // #1 was already released in v1.5.0, so its note stays there.
        var result = ReleaseNotesBuilder.Build([V160, V150], v => v == V160
            ? [new PrBody(2, Body("Six", "Šest")), new PrBody(3, "Reverts #1")]
            : [new PrBody(1, Body("Five", "Pet"))]);

        result.Entries[1].English.Should().Be("- Five");
    }

    [Fact]
    public void Build_RevertsMentionedMidSentence_IsNotARevert()
    {
        var result = ReleaseNotesBuilder.Build([V160],
            _ => Prs(Body("Feature", "Funkcija"), "## Summary\nThis PR fixes what PR 7 did; it neither reverts #1 nor anything else."));

        result.Entries[0].English.Should().Be("- Feature");
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
            _ => Prs(Body("Note", "Beleška"))).Entries;

        var parsed = ReleaseNotesDocument.Parse(ReleaseNotesDocument.Format(entries));

        parsed.Should().BeEquivalentTo(entries, o => o.WithStrictOrdering());
    }
}
