using Avalonia;
using Avalonia.Headless;
using Avalonia.ReactiveUI;

[assembly: AvaloniaTestApplication(typeof(Rentier.UnitTests.TestAppBuilder))]

namespace Rentier.UnitTests;

/// <summary>
/// Configures Avalonia for headless testing. Used by [AvaloniaFact] tests.
/// </summary>
public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<Rentier.Desktop.App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = true })
            .UseReactiveUI();
}
