using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAssertions;
using Rentier.Desktop.Controls.Markdown;
using Xunit;

namespace Rentier.UnitTests;

/// <summary>Headless tests for <see cref="MarkdownView"/>: what each kind of Markdown renders as.</summary>
[Trait("Category", "UI")]
public class MarkdownViewHeadlessTests
{
    private static (Window Window, MarkdownView View) Show(string markdown)
    {
        var view = new MarkdownView { Markdown = markdown };
        var window = new Window { Content = view, Width = 600, Height = 400 };
        window.Show();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        return (window, view);
    }

    private static string AllText(Window window) =>
        string.Join("\n", window.GetVisualDescendants().OfType<TextBlock>()
            .Select(t => t.Inlines is { Count: > 0 } inlines ? string.Concat(inlines.OfType<Run>().Select(r => r.Text)) + InlineText(inlines) : t.Text));

    private static string InlineText(InlineCollection inlines) =>
        string.Concat(inlines.OfType<Span>().Select(s => string.Concat(s.Inlines.OfType<Run>().Select(r => r.Text)) + InlineText(s.Inlines)));

    private static IEnumerable<Inline> AllInlines(Window window) =>
        window.GetVisualDescendants().OfType<SelectableTextBlock>().SelectMany(t => Flatten(t.Inlines!));

    private static IEnumerable<Inline> Flatten(InlineCollection inlines) =>
        inlines.SelectMany(i => i is Span span ? Flatten(span.Inlines).Prepend(i) : [i]);

    [AvaloniaTheory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  \n ")]
    public void MarkdownView_Blank_RendersNothing(string? markdown)
    {
        var view = new MarkdownView { Markdown = "x" };
        view.Markdown = markdown;

        view.Child.Should().BeOfType<StackPanel>().Which.Children.Should().BeEmpty();
    }

    [AvaloniaFact]
    public void MarkdownView_Heading_RendersSemiBoldText()
    {
        var (window, _) = Show("### Fixes");

        var heading = window.GetVisualDescendants().OfType<SelectableTextBlock>().Should().ContainSingle().Subject;
        heading.FontWeight.Should().Be(FontWeight.SemiBold);
        AllText(window).Should().Contain("Fixes").And.NotContain("#");

        window.Close();
    }

    [AvaloniaFact]
    public void MarkdownView_ConsecutiveLines_FormOneParagraphJoinedBySpace()
    {
        var (window, _) = Show("first line\nsecond line");

        window.GetVisualDescendants().OfType<SelectableTextBlock>().Should().ContainSingle();
        string.Concat(AllInlines(window).OfType<Run>().Select(r => r.Text)).Should().Be("first line second line");

        window.Close();
    }

    [AvaloniaFact]
    public void MarkdownView_TwoTrailingSpaces_RenderLineBreak()
    {
        var (window, _) = Show("one  \ntwo");

        AllInlines(window).OfType<LineBreak>().Should().ContainSingle();

        window.Close();
    }

    [AvaloniaFact]
    public void MarkdownView_NestedBulletItem_UsesInnerMarker()
    {
        var (window, _) = Show("- outer\n  - inner");

        window.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).Should().Contain(["•", "◦"]);
        AllText(window).Should().Contain("outer").And.Contain("inner");

        window.Close();
    }

    [AvaloniaFact]
    public void MarkdownView_OrderedListStartingAtThree_KeepsStartNumber()
    {
        var (window, _) = Show("3. Three\n4. Four");

        window.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).Should().Contain(["3.", "4."]);

        window.Close();
    }

    [AvaloniaFact]
    public void MarkdownView_Quote_RendersBorderedText()
    {
        var (window, _) = Show("> quoted text");

        window.GetVisualDescendants().OfType<Border>()
            .Should().Contain(b => b.BorderThickness.Left == 3);
        AllText(window).Should().Contain("quoted text");

        window.Close();
    }

    [AvaloniaFact]
    public void MarkdownView_FencedCode_RendersVerbatimInMonospace()
    {
        var (window, _) = Show("```\nvar x = **1**;\n  indented\n```");

        var code = window.GetVisualDescendants().OfType<SelectableTextBlock>().Should().ContainSingle().Subject;
        code.Text.Should().Be("var x = **1**;\n  indented");
        code.FontFamily.Name.Should().Contain("Consolas");

        window.Close();
    }

    [AvaloniaTheory]
    [InlineData("---")]
    [InlineData("***")]
    [InlineData("_ _ _")]
    public void MarkdownView_Rule_RendersSeparator(string markdown)
    {
        var (window, _) = Show(markdown);

        window.GetVisualDescendants().OfType<Separator>().Should().ContainSingle();

        window.Close();
    }

    [AvaloniaFact]
    public void MarkdownView_ItalicStrikeAndCode_RenderMatchingInlines()
    {
        var (window, _) = Show("*italic* ~~gone~~ `code`");

        var inlines = AllInlines(window).ToList();
        inlines.OfType<Italic>().Should().ContainSingle();
        inlines.OfType<Span>().Should().Contain(s => s.TextDecorations == TextDecorations.Strikethrough);
        inlines.OfType<Run>().Should().Contain(r => r.Text == "code" && r.FontFamily.Name.Contains("Consolas"));

        window.Close();
    }

    [AvaloniaTheory]
    [InlineData("snake_case_name")]
    [InlineData("2 * 3 * 4")]
    [InlineData("** not bold **")]
    [InlineData("a [bracket] alone")]
    public void MarkdownView_NotMarkup_StaysPlainText(string markdown)
    {
        var (window, _) = Show(markdown);

        var inlines = AllInlines(window).ToList();
        inlines.Should().AllBeOfType<Run>();
        string.Concat(inlines.OfType<Run>().Select(r => r.Text)).Should().Be(markdown);

        window.Close();
    }

    [AvaloniaFact]
    public void MarkdownView_Image_ShowsAltTextWithoutLoading()
    {
        var (window, _) = Show("![tracking pixel](https://example.com/pixel.png)");

        window.GetVisualDescendants().OfType<Image>().Should().BeEmpty();
        window.GetVisualDescendants().OfType<HyperlinkButton>().Should().BeEmpty();
        AllText(window).Should().Contain("tracking pixel");

        window.Close();
    }

    [AvaloniaFact]
    public void MarkdownView_RawHtml_ShowsAsText()
    {
        var (window, _) = Show("<b>not bold</b>");

        AllInlines(window).OfType<Bold>().Should().BeEmpty();
        AllText(window).Should().Contain("<b>not bold</b>");

        window.Close();
    }

    [AvaloniaFact]
    public void MarkdownView_HttpsAutolink_IsClickable()
    {
        var (window, _) = Show("<https://github.com/dj-milenkovic/rentier>");

        window.GetVisualDescendants().OfType<HyperlinkButton>().Should().ContainSingle()
            .Which.NavigateUri.Should().Be(new Uri("https://github.com/dj-milenkovic/rentier"));

        window.Close();
    }

    [AvaloniaTheory]
    [InlineData(">")]
    [InlineData("- ")]
    [InlineData("*")]
    public void MarkdownView_DeepNesting_RendersTextWithoutThrowing(string prefix)
    {
        var markdown = prefix == "- "
            ? string.Join("\n", Enumerable.Range(0, 200).Select(i => new string(' ', i * 2) + "- deep"))
            : string.Concat(Enumerable.Repeat(prefix, 200)) + " deep " + (prefix == "*" ? string.Concat(Enumerable.Repeat("*", 200)) : "");

        var act = () =>
        {
            var (window, _) = Show(markdown);
            AllText(window).Should().Contain("deep");
            window.Close();
        };

        act.Should().NotThrow();
    }

    [AvaloniaFact]
    public void MarkdownView_NestingBelowMarkdigLimit_FlattensDeepLevels()
    {
        var markdown = string.Concat(Enumerable.Repeat(">", 20)) + " deep";

        var (window, _) = Show(markdown);

        window.GetVisualDescendants().OfType<Border>().Count(b => b.BorderThickness.Left == 3).Should().Be(8);
        AllText(window).Should().Contain("deep");

        window.Close();
    }

    [AvaloniaFact]
    public void MarkdownView_BulletList_RendersMarkerAndText()
    {
        var (window, _) = Show("- First change\n- Second change");

        var text = AllText(window);
        text.Should().Contain("•").And.Contain("First change").And.Contain("Second change");

        window.Close();
    }

    [AvaloniaFact]
    public void MarkdownView_OrderedList_RendersNumbers()
    {
        var (window, _) = Show("1. One\n2. Two");

        window.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).Should().Contain(["1.", "2."]);

        window.Close();
    }

    [AvaloniaFact]
    public void MarkdownView_Bold_RendersBoldInline()
    {
        var (window, _) = Show("A **bold** word");

        window.GetVisualDescendants().OfType<SelectableTextBlock>()
            .SelectMany(t => t.Inlines!).OfType<Bold>().Should().ContainSingle();

        window.Close();
    }

    [AvaloniaFact]
    public void MarkdownView_HttpsLink_IsClickable()
    {
        var (window, _) = Show("See [the releases](https://github.com/dj-milenkovic/rentier/releases).");

        var link = window.GetVisualDescendants().OfType<HyperlinkButton>().Should().ContainSingle().Subject;
        link.NavigateUri.Should().Be(new Uri("https://github.com/dj-milenkovic/rentier/releases"));

        window.Close();
    }

    [AvaloniaTheory]
    [InlineData("[local](file:///C:/Windows/system32/calc.exe)")]
    [InlineData("[plain http](http://example.com)")]
    [InlineData("[script](javascript:alert(1))")]
    [InlineData("[relative](/docs)")]
    public void MarkdownView_NonHttpsLink_IsPlainText(string markdown)
    {
        var (window, _) = Show(markdown);

        window.GetVisualDescendants().OfType<HyperlinkButton>().Should().BeEmpty();

        window.Close();
    }

    [AvaloniaFact]
    public void MarkdownView_MarkdownChanged_ReplacesContent()
    {
        var (window, view) = Show("- old");

        view.Markdown = "Brand new paragraph";
        Dispatcher.UIThread.RunJobs();

        var text = AllText(window);
        text.Should().Contain("Brand new paragraph").And.NotContain("old");

        window.Close();
    }
}
