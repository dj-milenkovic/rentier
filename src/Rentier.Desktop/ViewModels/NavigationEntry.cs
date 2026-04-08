using ReactiveUI;

namespace Rentier.Desktop.ViewModels;

/// <summary>
/// Navigation sidebar entry binding model.
/// </summary>
public record NavigationEntry(string Label, ReactiveObject ViewModel);
