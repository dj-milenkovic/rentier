using Avalonia.Media;
using ReactiveUI.Reactive;

namespace Rentier.Desktop.ViewModels;

/// <summary>
/// Navigation sidebar entry binding model.
/// <para>
/// Entries with <see cref="IsVisible"/> set to <c>false</c> are transient placeholders
/// (e.g. sub-pages such as ManualFiling) that should not appear in the sidebar.
/// </para>
/// <para>
/// Group entries (<see cref="IsGroup"/> = <c>true</c>) act as collapsible section
/// headers with no content ViewModel; their <see cref="Children"/> are shown/hidden
/// by calling <see cref="ToggleExpanded"/>.
/// </para>
/// </summary>
public class NavigationEntry : ReactiveObject
{
    private string _label;
    private bool _isVisible;
    private bool _isExpanded;
    private bool _isActive;

    public string Label
    {
        get => _label;
        set => this.RaiseAndSetIfChanged(ref _label, value);
    }

    /// <summary>
    /// The content ViewModel for this entry. Nullable — group headers have no ViewModel.
    /// </summary>
    public ReactiveObject? ViewModel { get; init; }

    /// <summary>
    /// Whether this entry is visible in the sidebar. Reactive — can be toggled at runtime
    /// (e.g. child entries hidden when parent group is collapsed).
    /// </summary>
    public bool IsVisible
    {
        get => _isVisible;
        set => this.RaiseAndSetIfChanged(ref _isVisible, value);
    }

    /// <summary>
    /// True when this entry is the currently active content page.
    /// Decoupled from ListBox selection so the pipe highlight survives the
    /// SelectedEntry=null reset used to allow re-clicking the same item.
    /// </summary>
    public bool IsActive
    {
        get => _isActive;
        set => this.RaiseAndSetIfChanged(ref _isActive, value);
    }

    /// <summary>
    /// True for child entries nested under a group header (IndentLevel &gt; 0).
    /// Used by converters to apply smaller sizing in the sidebar.
    /// </summary>
    public bool IsChild => IndentLevel > 0;

    public StreamGeometry? Icon { get; init; }

    /// <summary>
    /// True for collapsible group header entries (e.g. the Settings group).
    /// Group headers have no <see cref="ViewModel"/>.
    /// </summary>
    public bool IsGroup { get; init; }

    /// <summary>
    /// Whether the group is currently expanded (children visible). Only meaningful when
    /// <see cref="IsGroup"/> is <c>true</c>.
    /// </summary>
    public bool IsExpanded
    {
        get => _isExpanded;
        set => this.RaiseAndSetIfChanged(ref _isExpanded, value);
    }

    /// <summary>
    /// Child entries owned by this group. Empty for non-group entries.
    /// </summary>
    public IReadOnlyList<NavigationEntry> Children { get; set; } = [];

    /// <summary>
    /// Back-reference to the parent group entry. Null for top-level and group entries.
    /// Settable after construction to allow circular references during sidebar initialization.
    /// </summary>
    public NavigationEntry? ParentGroup { get; set; }

    /// <summary>
    /// Indentation level: 0 for top-level and group headers, 1 for Settings children.
    /// </summary>
    public int IndentLevel { get; init; }

    public NavigationEntry(
        string label,
        ReactiveObject? viewModel = null,
        bool IsVisible = true,
        StreamGeometry? Icon = null)
    {
        _label = label;
        ViewModel = viewModel;
        _isVisible = IsVisible;
        this.Icon = Icon;
    }

    /// <summary>
    /// Toggles <see cref="IsExpanded"/> and propagates the new visibility state to all
    /// direct <see cref="Children"/>. Must be called on the UI thread.
    /// </summary>
    public void ToggleExpanded()
    {
        IsExpanded = !IsExpanded;
        foreach (var child in Children)
            child.IsVisible = IsExpanded;
    }
}

