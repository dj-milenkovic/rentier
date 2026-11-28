using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAssertions;
using Rentier.Application.DTOs;
using Rentier.Desktop.Controls.Markdown;
using Rentier.Desktop.Views;
using Xunit;

namespace Rentier.UnitTests;

/// <summary>Headless tests for <see cref="WhatsNewSettingsView"/>: one card per version, loaded on activation.</summary>
[Trait("Category", "UI")]
public class WhatsNewSettingsViewHeadlessTests
{
    private static Window Show(IReadOnlyList<ReleaseNoteDto> notes)
    {
        var vm = DesktopTestDoubles.WhatsNewViewModel(notes);
        var window = new Window { Content = new WhatsNewSettingsView { DataContext = vm, ViewModel = vm }, Width = 800, Height = 600 };
        window.Show();
        for (var i = 0; i < 20; i++)
            Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        return window;
    }

    [AvaloniaFact]
    public void WhatsNewSettingsView_WithNotes_RendersOneMarkdownViewPerVersion()
    {
        var window = Show([new("v1.4.0", "- New"), new("v1.3.1", "- Old")]);

        window.GetVisualDescendants().OfType<MarkdownView>().Select(m => m.Markdown)
            .Should().Equal("- New", "- Old");
        window.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text)
            .Should().Contain(["v1.4.0", "v1.3.1"]);

        window.Close();
    }

    [AvaloniaFact]
    public void WhatsNewSettingsView_WithNotes_LinksToReleasesPage()
    {
        var window = Show([new("v1.4.0", "- New")]);

        window.GetVisualDescendants().OfType<HyperlinkButton>()
            .Should().Contain(b => b.NavigateUri != null && b.NavigateUri.AbsolutePath.EndsWith("/releases"));

        window.Close();
    }

    [AvaloniaFact]
    public void WhatsNewSettingsView_WithoutNotes_ShowsNoCards()
    {
        var window = Show([]);

        window.GetVisualDescendants().OfType<MarkdownView>().Should().BeEmpty();

        window.Close();
    }
}
