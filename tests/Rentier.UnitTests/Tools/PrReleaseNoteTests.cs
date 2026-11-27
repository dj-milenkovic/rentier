using FluentAssertions;
using Rentier.ReleaseNotes;
using Xunit;

namespace Rentier.UnitTests.Tools;

public class PrReleaseNoteTests
{
    [Fact]
    public void Parse_SectionWithBothLanguages_ReturnsBulletsForEach()
    {
        var note = PrReleaseNote.Parse("""
            ## Summary
            Internal details.

            ## Release note
            EN: The update bar spans the whole window.
            SR: Traka za ažuriranje je preko celog prozora.

            ## Tests
            - passed
            """);

        note.HasSection.Should().BeTrue();
        note.English.Should().Be("- The update bar spans the whole window.");
        note.Serbian.Should().Be("- Traka za ažuriranje je preko celog prozora.");
        note.Problems.Should().BeEmpty();
    }

    [Fact]
    public void Parse_MultiLineAndListNotes_KeepsThem()
    {
        var note = PrReleaseNote.Parse("""
            ### Release notes
            EN:
            - First change
            - Second change
            SR: Prva rečenica
            nastavlja se ovde.
            """);

        note.English.Should().Be("- First change\n- Second change");
        note.Serbian.Should().Be("- Prva rečenica\n  nastavlja se ovde.");
    }

    [Fact]
    public void Parse_BoldMarkersAndLowerCase_AreAccepted()
    {
        var note = PrReleaseNote.Parse("## release note\n**EN:** One\nsr: Jedan\n");

        note.English.Should().Be("- One");
        note.Serbian.Should().Be("- Jedan");
    }

    [Fact]
    public void Parse_LanguageHeadings_ReturnBothLists()
    {
        var note = PrReleaseNote.Parse("""
            ## Release note

            ### EN
            - The update bar spans the whole window.
            - New What's new page.

            ### SR
            - Traka za ažuriranje je preko celog prozora.

            ## Testing
            - passed
            """);

        note.English.Should().Be("- The update bar spans the whole window.\n- New What's new page.");
        note.Serbian.Should().Be("- Traka za ažuriranje je preko celog prozora.");
    }

    [Fact]
    public void Parse_SerbianLineRightAfterEnglishList_IsStillSerbian()
    {
        // GitHub renders this "SR:" as part of the last English bullet; the parser must not.
        var note = PrReleaseNote.Parse("## Release note\nEN:\n- One\n- Two\nSR:\n- Jedan\n- Dva\n");

        note.English.Should().Be("- One\n- Two");
        note.Serbian.Should().Be("- Jedan\n- Dva");
    }

    [Fact]
    public void Parse_UntouchedTemplate_IsEmpty()
    {
        var note = PrReleaseNote.Parse("""
            ## Release note
            <!-- What users will notice, in both languages. Leave empty for internal changes. -->
            EN: <!-- English -->
            SR: <!-- Srpski -->
            """);

        note.HasSection.Should().BeTrue();
        note.IsEmpty.Should().BeTrue();
        note.Problems.Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("## Summary\nNo note here.")]
    public void Parse_NoSection_IsEmptyWithoutProblems(string? body)
    {
        var note = PrReleaseNote.Parse(body);

        note.HasSection.Should().BeFalse();
        note.IsEmpty.Should().BeTrue();
        note.Problems.Should().BeEmpty();
    }

    [Fact]
    public void Parse_EnglishOnly_ReportsMissingSerbian()
    {
        PrReleaseNote.Parse("## Release note\nEN: Only English.\n").Problems
            .Should().ContainSingle().Which.Should().Contain("no SR: line");
    }

    [Fact]
    public void Parse_SerbianOnly_ReportsMissingEnglish()
    {
        PrReleaseNote.Parse("## Release note\nSR: Samo srpski.\n").Problems
            .Should().ContainSingle().Which.Should().Contain("no EN: line");
    }

    [Fact]
    public void Parse_SectionEndsAtNextHeadingOfSameLevel()
    {
        var note = PrReleaseNote.Parse("## Release note\nEN: One\nSR: Jedan\n## Checklist\n- EN: not part of the note\n");

        note.English.Should().Be("- One");
    }

    [Fact]
    public void Parse_CrLfBody_IsNormalised()
    {
        PrReleaseNote.Parse("## Release note\r\nEN: One\r\nSR: Jedan\r\n").English.Should().Be("- One");
    }
}
