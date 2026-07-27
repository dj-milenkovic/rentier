using Avalonia;
using Avalonia.Headless;
using ReactiveUI.Avalonia.Reactive;
using Rentier.UnitTests.Desktop;
using Xunit;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]
[assembly: CollectionBehavior(DisableTestParallelization = true)]
namespace Rentier.UnitTests.Desktop;

/// <summary>
/// Configures Avalonia for headless testing. Used by [AvaloniaFact] tests.
/// </summary>
public static class TestAppBuilder
{
    private static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<Rentier.Desktop.App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = true })
            .UseReactiveUI(_ => { });
}
