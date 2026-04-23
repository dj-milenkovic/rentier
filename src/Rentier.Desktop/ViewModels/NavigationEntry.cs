using Avalonia.Media;
using ReactiveUI;

namespace Rentier.Desktop.ViewModels;

/// <summary>
/// Navigation sidebar entry binding model.
/// <para>
/// Entries with <see cref="IsVisible"/> set to <c>false</c> are transient placeholders
/// (e.g. sub-pages such as ManualFiling) that should not appear in the sidebar.
/// </para>
/// </summary>
public class NavigationEntry : ReactiveObject
{
    private string _label;

    public string Label
    {
        get => _label;
        set => this.RaiseAndSetIfChanged(ref _label, value);
    }

    public ReactiveObject ViewModel { get; }
    public bool IsVisible { get; }
    public StreamGeometry? Icon { get; }

    public NavigationEntry(string label, ReactiveObject viewModel, bool IsVisible = true, StreamGeometry? Icon = null)
    {
        _label = label;
        ViewModel = viewModel;
        this.IsVisible = IsVisible;
        this.Icon = Icon;
    }
}

