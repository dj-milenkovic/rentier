using FluentAssertions;
using Rentier.Application.ReleaseNotes;
using Xunit;

namespace Rentier.UnitTests.Application.ReleaseNotes;

public class ReleaseNotesDocumentTests
{
    private const string TWO_VERSIONS = """
        <!-- header comment with ## v9.9.9 inside is ignored -->

        ## v1.3.1

        ### EN
        - Updates itself.

        ### SR
        - Sam se ažurira.

        ## v1.4.0

        ### EN
        - **Every** download updates itself.
          - Nested detail.

        ### SR
        - **Svaki** paket se sam ažurira.
        """;

    [Fact]
    public void Parse_TwoVersions_ReturnsNewestFirstWithBothLanguages()
    {
        var entries = ReleaseNotesDocument.Parse(TWO_VERSIONS);

        entries.Select(e => e.DisplayVersion).Should().Equal("v1.4.0", "v1.3.1");
        entries[0].English.Should().Be("- **Every** download updates itself.\n  - Nested detail.");
        entries[0].Serbian.Should().Be("- **Svaki** paket se sam ažurira.");
    }

    [Fact]
    public void Parse_HtmlComment_IsIgnored()
    {
        ReleaseNotesDocument.Parse(TWO_VERSIONS).Should().NotContain(e => e.Version == new Version(9, 9, 9));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Just some text without any version heading.")]
    public void Parse_NoVersionBlocks_ReturnsEmpty(string? markdown)
    {
        ReleaseNotesDocument.Parse(markdown).Should().BeEmpty();
    }

    [Fact]
    public void Parse_VersionWithoutNotes_IsLeftOut()
    {
        var entries = ReleaseNotesDocument.Parse("## v1.0.0\n\n## v1.1.0\n\n### EN\n- One\n");

        entries.Should().ContainSingle().Which.DisplayVersion.Should().Be("v1.1.0");
    }

    [Fact]
    public void Parse_RepeatedVersion_KeepsFirstBlock()
    {
        var entries = ReleaseNotesDocument.Parse("## v1.0.0\n### EN\n- First\n## v1.0.0\n### EN\n- Second\n");

        entries.Should().ContainSingle().Which.English.Should().Be("- First");
    }

    [Fact]
    public void Parse_CrLfLineEndings_AreNormalised()
    {
        var entries = ReleaseNotesDocument.Parse("## v1.0.0\r\n\r\n### EN\r\n- One\r\n- Two\r\n");

        entries.Single().English.Should().Be("- One\n- Two");
    }

    [Fact]
    public void Parse_DeeperHeadingsInsideNotes_StayInTheNotes()
    {
        var entries = ReleaseNotesDocument.Parse("## v1.0.0\n### EN\n#### Fixes\n- One\n");

        entries.Single().English.Should().Be("#### Fixes\n- One");
    }

    [Fact]
    public void Format_ThenParse_RoundTrips()
    {
        var original = ReleaseNotesDocument.Parse(TWO_VERSIONS);

        var reparsed = ReleaseNotesDocument.Parse(ReleaseNotesDocument.Format(original));

        reparsed.Should().BeEquivalentTo(original, o => o.WithStrictOrdering());
    }

    [Fact]
    public void Format_WritesNewestFirstAndSkipsEmptyLanguages()
    {
        var text = ReleaseNotesDocument.Format(
        [
            new ReleaseNotesEntry(new Version(1, 0, 0), "- Old", ""),
            new ReleaseNotesEntry(new Version(1, 1, 0), "- New", "- Novo"),
        ]);

        text.Should().Be("## v1.1.0\n\n### EN\n- New\n\n### SR\n- Novo\n\n## v1.0.0\n\n### EN\n- Old\n");
    }

    [Fact]
    public void NewerThan_ReturnsOnlyLaterVersionsNewestFirst()
    {
        var entries = ReleaseNotesDocument.Parse(TWO_VERSIONS);

        ReleaseNotesDocument.NewerThan(entries, new Version(1, 3, 1))
            .Select(e => e.DisplayVersion).Should().Equal("v1.4.0");
        ReleaseNotesDocument.NewerThan(entries, new Version(1, 3)).Should().HaveCount(2);
        ReleaseNotesDocument.NewerThan(entries, new Version(1, 4, 0)).Should().BeEmpty();
    }

    [Theory]
    [InlineData("1.4.0", 1, 4, 0)]
    [InlineData("v1.4.0", 1, 4, 0)]
    [InlineData("V2.0", 2, 0, 0)]
    [InlineData("1.4.0-beta.1", 1, 4, 0)]
    [InlineData("1.4.0+abc123", 1, 4, 0)]
    public void TryParseVersion_ValidText_ReturnsMajorMinorPatch(string text, int major, int minor, int patch)
    {
        ReleaseNotesDocument.TryParseVersion(text, out var version).Should().BeTrue();
        version.Should().Be(new Version(major, minor, patch));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("dev")]
    [InlineData("v")]
    public void TryParseVersion_InvalidText_ReturnsFalse(string? text)
    {
        ReleaseNotesDocument.TryParseVersion(text, out _).Should().BeFalse();
    }

    [Theory]
    [InlineData("sr-Latn", "- Srpski")]
    [InlineData("sr", "- Srpski")]
    [InlineData("en", "- English")]
    [InlineData("de-DE", "- English")]
    public void For_PicksLanguageByCulture(string culture, string expected)
    {
        new ReleaseNotesEntry(new Version(1, 0, 0), "- English", "- Srpski").For(culture).Should().Be(expected);
    }

    [Fact]
    public void For_PreferredLanguageEmpty_FallsBackToOther()
    {
        new ReleaseNotesEntry(new Version(1, 0, 0), "- English", " ").For("sr-Latn").Should().Be("- English");
        new ReleaseNotesEntry(new Version(1, 0, 0), "", "- Srpski").For("en").Should().Be("- Srpski");
    }
}
