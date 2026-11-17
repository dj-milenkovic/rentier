using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using FluentAssertions;
using ReactiveUI;
using Rentier.Desktop.ViewModels;
using Xunit;
using ViewLocator = Rentier.Desktop.ViewLocator;

namespace Rentier.UnitTests.Desktop;

/// <summary>
/// Guards the explicit <see cref="ViewLocator"/> registry. Mappings are checked against what
/// each factory actually builds — every page view is a <c>ReactiveUserControl&lt;TViewModel&gt;</c>,
/// i.e. an <see cref="IViewFor{T}"/> — so no naming convention is assumed.
/// </summary>
[Trait("Category", "UI")]
public class ViewLocatorTests
{
    private static readonly System.Reflection.Assembly DESKTOP_ASSEMBLY = typeof(ViewLocator).Assembly;

    public static TheoryData<Type> RegisteredViewModels => [.. ViewLocator.RegisteredViewModelTypes];

    /// <summary>
    /// A ViewModel instance whose constructor is skipped: the locator only looks at its type,
    /// so no services have to be arranged.
    /// </summary>
    private static object UninitializedInstanceOf(Type type) => RuntimeHelpers.GetUninitializedObject(type);

    /// <summary>The ViewModel type of every page view: UserControls implementing IViewFor&lt;T&gt;.</summary>
    private static IEnumerable<Type> PageViewModelTypes() =>
        DESKTOP_ASSEMBLY.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(UserControl).IsAssignableFrom(t))
            .SelectMany(t => t.GetInterfaces())
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IViewFor<>))
            .Select(i => i.GetGenericArguments()[0]);

    [AvaloniaTheory]
    [MemberData(nameof(RegisteredViewModels))]
    public void Build_RegisteredViewModel_ReturnsViewForThatViewModel(Type viewModelType)
    {
        var view = new ViewLocator().Build(UninitializedInstanceOf(viewModelType));

        view.Should().BeAssignableTo(typeof(IViewFor<>).MakeGenericType(viewModelType));
    }

    [Fact]
    public void RegisteredViewModelTypes_EveryPageViewModel_IsRegistered()
    {
        var registered = ViewLocator.RegisteredViewModelTypes.ToHashSet();

        var missing = PageViewModelTypes().Where(t => !registered.Contains(t)).Select(t => t.Name).ToList();

        missing.Should().BeEmpty("every page view's ViewModel must be registered in ViewLocator");
    }

    [AvaloniaFact]
    public void Build_NullViewModel_ReturnsPlaceholderText()
    {
        var view = new ViewLocator().Build(null);

        view.Should().BeOfType<TextBlock>().Which.Text.Should().Be("No ViewModel provided");
    }

    [AvaloniaFact]
    public void Build_UnregisteredViewModel_ReturnsNotFoundWithFullTypeName()
    {
        var view = new ViewLocator().Build(UninitializedInstanceOf(typeof(FilingRowViewModel)));

        view.Should().BeOfType<TextBlock>()
            .Which.Text.Should().Be($"View not found for {typeof(FilingRowViewModel).FullName}");
    }

    [Theory]
    [MemberData(nameof(RegisteredViewModels))]
    public void Match_RegisteredViewModel_ReturnsTrue(Type viewModelType)
    {
        new ViewLocator().Match(UninitializedInstanceOf(viewModelType)).Should().BeTrue();
    }

    [Fact]
    public void Match_UnregisteredReactiveObject_ReturnsFalse()
    {
        new ViewLocator().Match(UninitializedInstanceOf(typeof(FilingRowViewModel))).Should().BeFalse();
    }

    [Fact]
    public void Match_Null_ReturnsFalse()
    {
        new ViewLocator().Match(null).Should().BeFalse();
    }
}
