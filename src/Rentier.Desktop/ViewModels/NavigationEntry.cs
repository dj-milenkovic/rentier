using ReactiveUI;

namespace Rentier.Desktop.ViewModels;

/// <summary>
/// Navigation sidebar entry binding model.
/// <para>
/// Entries with <see cref="IsVisible"/> set to <c>false</c> are transient placeholders
/// (e.g. sub-pages such as ManualFiling) that should not appear in the sidebar.
/// </para>
/// </summary>
public record NavigationEntry(string Label, ReactiveObject ViewModel, bool IsVisible = true);
