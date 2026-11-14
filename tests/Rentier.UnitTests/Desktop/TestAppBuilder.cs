using Avalonia;
using Avalonia.Headless;
using ReactiveUI.Avalonia;
using Rentier.UnitTests.Desktop;
using Xunit;
using Xunit.Sdk;
using Xunit.v3;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]
// Avalonia headless runs on a single dispatcher thread, so tests in this assembly run serially.
[assembly: Parallelization(Mode = ParallelMode.None)]
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
