using FluentAssertions;
using Rentier.Desktop.Controls.Markdown;
using Xunit;

namespace Rentier.UnitTests.Desktop.Markdown;

public class MarkdownParserTests
{
    private static IReadOnlyList<MdInline> Inlines(string text) => MarkdownParser.ParseInlines(text);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  \n ")]
    public void Parse_Blank_ReturnsNoBlocks(string? markdown)
    {
        MarkdownParser.Parse(markdown).Should().BeEmpty();
    }

    [Fact]
    public void Parse_Heading_ReturnsLevelAndText()
    {
        var block = MarkdownParser.Parse("### Fixes ###").Single();

        block.Should().BeEquivalentTo(new MdHeading(3, [new MdText("Fixes")]));
    }

    [Fact]
    public void Parse_ConsecutiveLines_FormOneParagraphJoinedBySpace()
    {
        var block = MarkdownParser.Parse("first line\nsecond line").Single();

        block.Should().BeEquivalentTo(new MdParagraph([new MdText("first line second line")]));
    }

    [Fact]
    public void Parse_BlankLine_SeparatesParagraphs()
    {
        MarkdownParser.Parse("one\n\ntwo").Should().HaveCount(2).And.AllBeOfType<MdParagraph>();
    }

    [Fact]
    public void Parse_TwoTrailingSpaces_MakeAHardBreak()
    {
        var paragraph = (MdParagraph)MarkdownParser.Parse("one  \ntwo").Single();

        paragraph.Content.Select(i => i.GetType()).Should().Equal(typeof(MdText), typeof(MdLineBreak), typeof(MdText));
        paragraph.Content.OfType<MdText>().Select(t => t.Text).Should().Equal("one", "two");
    }

    [Fact]
    public void Parse_BulletList_ReturnsItemsInOrder()
    {
        var list = (MdList)MarkdownParser.Parse("- one\n* two\n+ three").Single();

        list.IsOrdered.Should().BeFalse();
        list.Items.Select(ItemText).Should().Equal("one", "two", "three");
    }

    [Fact]
    public void Parse_OrderedList_KeepsStartNumber()
    {
        var list = (MdList)MarkdownParser.Parse("3. three\n4. four").Single();

        list.IsOrdered.Should().BeTrue();
        list.Start.Should().Be(3);
        list.Items.Should().HaveCount(2);
    }

    [Fact]
    public void Parse_IndentedItem_IsNestedUnderPreviousItem()
    {
        var list = (MdList)MarkdownParser.Parse("- parent\n  - child\n- sibling").Single();

        list.Items.Should().HaveCount(2);
        var nested = list.Items[0].Blocks.OfType<MdList>().Single();
        nested.Items.Select(ItemText).Should().Equal("child");
    }

    [Fact]
    public void Parse_ContinuationLine_JoinsItemText()
    {
        var list = (MdList)MarkdownParser.Parse("- starts here\n  and goes on").Single();

        ItemText(list.Items.Single()).Should().Be("starts here and goes on");
    }

    [Fact]
    public void Parse_ParagraphAfterList_EndsTheList()
    {
        var blocks = MarkdownParser.Parse("- item\n\nAfter the list.");

        blocks.Should().HaveCount(2);
        blocks[0].Should().BeOfType<MdList>();
        blocks[1].Should().BeOfType<MdParagraph>();
    }

    [Fact]
    public void Parse_BlankLineBetweenItems_KeepsOneList()
    {
        var list = (MdList)MarkdownParser.Parse("- one\n\n- two").Single();

        list.Items.Should().HaveCount(2);
    }

    [Fact]
    public void Parse_Quote_WrapsInnerBlocks()
    {
        var quote = (MdQuote)MarkdownParser.Parse("> quoted\n> text").Single();

        quote.Blocks.Single().Should().BeEquivalentTo(new MdParagraph([new MdText("quoted text")]));
    }

    [Fact]
    public void Parse_FencedCode_KeepsLinesVerbatim()
    {
        var code = (MdCodeBlock)MarkdownParser.Parse("```\n**not bold**\n  indented\n```").Single();

        code.Code.Should().Be("**not bold**\n  indented");
    }

    [Fact]
    public void Parse_UnclosedFence_RunsToEnd()
    {
        MarkdownParser.Parse("```\ncode").Single().Should().Be(new MdCodeBlock("code"));
    }

    [Theory]
    [InlineData("---")]
    [InlineData("***")]
    [InlineData("_ _ _")]
    public void Parse_Rule_ReturnsRule(string line)
    {
        MarkdownParser.Parse(line).Single().Should().BeOfType<MdRule>();
    }

    [Fact]
    public void Parse_DeepNesting_DoesNotThrow()
    {
        var deep = string.Concat(Enumerable.Range(0, 40).Select(i => new string(' ', i * 2) + "- level\n"))
                   + new string('>', 40) + " quote";

        var act = () => MarkdownParser.Parse(deep);

        act.Should().NotThrow();
    }

    [Fact]
    public void ParseInlines_BoldItalicStrikeAndCode()
    {
        Inlines("**bold** *it* _it2_ ~~gone~~ `code`").Should().BeEquivalentTo(
            new MdInline[]
            {
                new MdBold([new MdText("bold")]), new MdText(" "),
                new MdItalic([new MdText("it")]), new MdText(" "),
                new MdItalic([new MdText("it2")]), new MdText(" "),
                new MdStrikethrough([new MdText("gone")]), new MdText(" "),
                new MdCode("code"),
            },
            o => o.PreferringRuntimeMemberTypes());
    }

    [Fact]
    public void ParseInlines_NestedEmphasis()
    {
        Inlines("**bold *and italic***").Single().Should().BeEquivalentTo(
            new MdBold([new MdText("bold "), new MdItalic([new MdText("and italic")])]),
            o => o.PreferringRuntimeMemberTypes());
    }

    [Theory]
    [InlineData("snake_case_name")]
    [InlineData("2 * 3 * 4")]
    [InlineData("** not bold **")]
    [InlineData("unclosed **bold")]
    [InlineData("a [bracket] alone")]
    public void ParseInlines_NotMarkup_StaysPlainText(string text)
    {
        Inlines(text).Should().Equal(new MdText(text));
    }

    [Fact]
    public void ParseInlines_Link_ReturnsUrlAndLabel()
    {
        Inlines("see [the docs](https://example.com/a) now").Should().BeEquivalentTo(
            new MdInline[]
            {
                new MdText("see "),
                new MdLink("https://example.com/a", [new MdText("the docs")]),
                new MdText(" now"),
            },
            o => o.PreferringRuntimeMemberTypes());
    }

    [Fact]
    public void ParseInlines_BackslashEscape_KeepsCharacterLiteral()
    {
        Inlines(@"\*not italic\*").Should().Equal(new MdText("*not italic*"));
    }

    [Fact]
    public void ParseInlines_CodeContent_IsNotParsed()
    {
        Inlines("`**raw**`").Should().Equal(new MdCode("**raw**"));
    }

    private static string ItemText(MdListItem item) =>
        string.Concat(item.Blocks.OfType<MdParagraph>().Single().Content.OfType<MdText>().Select(t => t.Text));
}
