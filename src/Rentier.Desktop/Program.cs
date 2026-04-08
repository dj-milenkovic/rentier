using Avalonia;
using Avalonia.ReactiveUI;

namespace Rentier.Desktop;

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        return BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .UseReactiveUI();
}
