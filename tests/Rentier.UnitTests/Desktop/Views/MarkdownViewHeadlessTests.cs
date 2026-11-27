using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
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
