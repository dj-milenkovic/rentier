using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAssertions;
using Rentier.Desktop.Views;
using Xunit;

namespace Rentier.UnitTests;

/// <summary>Headless smoke tests for <see cref="DiagnosticsSettingsView"/>: it renders and its controls bind.</summary>
[Trait("Category", "UI")]
public class DiagnosticsSettingsViewHeadlessTests
{
    private static (Window Window, DiagnosticsSettingsView View) Show()
    {
        var vm = DesktopTestDoubles.DiagnosticsViewModel();
        var view = new DiagnosticsSettingsView { DataContext = vm };
        var window = new Window { Content = view, Width = 800, Height = 600 };
        window.Show();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        return (window, view);
    }

    [AvaloniaFact]
    public void DiagnosticsSettingsView_WhenCreated_RendersWithoutError()
    {
        var (window, _) = Show();

        window.IsVisible.Should().BeTrue();

        window.Close();
    }

    [AvaloniaFact]
    public void DiagnosticsSettingsView_WhenCreated_HasDebugLoggingToggleBoundToViewModel()
    {
        var (window, _) = Show();

        window.GetVisualDescendants().OfType<ToggleSwitch>().Should().ContainSingle();

        window.Close();
    }

    [AvaloniaFact]
    public void DiagnosticsSettingsView_WhenCreated_OpenLogsFolderButtonIsBoundToCommand()
    {
        var (window, view) = Show();

        var command = view.ViewModel!.OpenLogsFolderCommand;

        window.GetVisualDescendants().OfType<Button>()
            .Should().Contain(b => ReferenceEquals(b.Command, command));

        window.Close();
    }

    [AvaloniaFact]
    public void DiagnosticsSettingsView_WhenCreated_ShowsLogDirectory()
    {
        var (window, _) = Show();

        window.GetVisualDescendants().OfType<SelectableTextBlock>()
            .Should().Contain(t => t.Text == @"C:\logs");

        window.Close();
    }
}
