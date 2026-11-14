using FluentAssertions;
using ReactiveUI;
using Rentier.Desktop;
using Xunit;

namespace Rentier.UnitTests.Desktop;

/// <summary>
/// Guards the explicit <see cref="ViewLocator"/> registry against drifting from the
/// FooViewModel -> FooView naming convention. No Avalonia runtime is needed: only types are inspected.
/// </summary>
public class ViewLocatorTests
{
    private static readonly System.Reflection.Assembly DESKTOP_ASSEMBLY = typeof(ViewLocator).Assembly;

    private static string ViewNameFor(Type viewModelType) =>
        viewModelType.FullName!.Replace("ViewModels", "Views").Replace("ViewModel", "View");

    [Fact]
    public void RegisteredViewModelTypes_AllHaveMatchingViewType()
    {
        foreach (var viewModelType in ViewLocator.RegisteredViewModelTypes)
        {
            DESKTOP_ASSEMBLY.GetType(ViewNameFor(viewModelType))
                .Should().NotBeNull($"{viewModelType.Name} is registered but its View does not exist");
        }
    }

    [Fact]
    public void ViewModelsWithAMatchingView_AreAllRegistered()
    {
        var registered = ViewLocator.RegisteredViewModelTypes.ToHashSet();

        var missing = DESKTOP_ASSEMBLY.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false }
                        && t.Namespace == "Rentier.Desktop.ViewModels"
                        && t.Name.EndsWith("ViewModel", StringComparison.Ordinal)
                        && typeof(ReactiveObject).IsAssignableFrom(t)
                        && DESKTOP_ASSEMBLY.GetType(ViewNameFor(t)) is not null
                        && t.Name != "MainWindowViewModel") // hosted by MainWindow, not resolved via ViewLocator
            .Where(t => !registered.Contains(t))
            .Select(t => t.Name)
            .ToList();

        missing.Should().BeEmpty("every ViewModel with a View must be registered in ViewLocator");
    }
}
