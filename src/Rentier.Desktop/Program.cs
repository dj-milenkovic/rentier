using System.Diagnostics.CodeAnalysis;
using Avalonia;
using ReactiveUI.Avalonia;
using Microsoft.Extensions.DependencyInjection;
using Rentier.Application.Interfaces;
using Rentier.Infrastructure;
using Velopack;

namespace Rentier.Desktop;

[ExcludeFromCodeCoverage]
public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        // --smoke-test: initialize DB and exit 0/1 — used by release CI to validate artifacts.
        // Must be checked before VelopackApp.Build().Run() to avoid installer lifecycle side-effects.
        if (args.Contains("--smoke-test"))
            return RunSmokeTest();

        VelopackApp.Build().Run();
        return BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    private static AppBuilder BuildAvaloniaApp()
    => AppBuilder.Configure<App>()
        .UsePlatformDetect()
        .WithInterFont()
        .UseReactiveUI(_ => { });


    // ── Headless smoke test ───────────────────────────────────────────────────

    private static int RunSmokeTest()
    {
        try
        {
            // Deliberate sync-over-async at the process entry point. Main cannot be async:
            // [STAThread] is not applied to the compiler-generated entry point of an async Main,
            // and continuations after the first await resume on MTA thread-pool threads, which
            // breaks Avalonia's Win32 backend (COM/OLE: clipboard, drag-drop, file dialogs).
            // Blocking is safe here: this headless path runs before any SynchronizationContext
            // or UI exists, and Task.Run keeps the work off the STA thread. It is the same
            // bridge the compiler emits for an async Main.
            return Task.Run(SmokeTestAsync).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[SMOKE] FAILED: {ex}");
            return 1;
        }
    }

    /// <summary>
    /// Registers infrastructure services, applies all EF migrations, then exits.
    /// Uses a dedicated smoke DB path so it never touches a real user database.
    /// </summary>
    private static async Task<int> SmokeTestAsync()
    {
        // Isolated DB path — avoids touching a real user database on dev machines.
        var dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Rentier", "rentier-smoke.db");
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

        var services = new ServiceCollection();

        // Same explicit registration call as App.axaml.cs, so the smoke test exercises
        // the real infrastructure wiring.
        await services.AddInfrastructureServicesAsync(dbPath);

        await using var provider = services.BuildServiceProvider();
        await provider.GetRequiredService<IDatabaseInitializer>().InitializeAsync();

        Console.WriteLine("[SMOKE] All migrations applied. Artifact is functional. Exiting 0.");
        return 0;
    }
}
