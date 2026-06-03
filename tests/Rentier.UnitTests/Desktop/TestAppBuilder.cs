using Avalonia;
using Avalonia.Headless;
using ReactiveUI.Avalonia;
using Rentier.UnitTests.Desktop;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

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
