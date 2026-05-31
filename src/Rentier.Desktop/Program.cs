using Avalonia;
using Avalonia.ReactiveUI;
using Microsoft.Extensions.DependencyInjection;
using Rentier.Application.Interfaces;
using Velopack;

namespace Rentier.Desktop;

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

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .UseReactiveUI();

    // ── Headless smoke test ───────────────────────────────────────────────────

    private static int RunSmokeTest()
    {
        try
        {
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

        // Reuse the same reflection-based registrar pattern as App.axaml.cs
        // so the smoke test exercises real infrastructure registration.
        var infraAssembly = System.Reflection.Assembly.Load("Rentier.Infrastructure")
            ?? throw new InvalidOperationException(
                "Could not load Rentier.Infrastructure assembly. " +
                "Ensure the assembly is present in the application output directory.");

        var registrarType = infraAssembly.GetTypes()
            .FirstOrDefault(t => typeof(IInfrastructureRegistrar).IsAssignableFrom(t)
                                 && t is { IsClass: true, IsAbstract: false })
            ?? throw new InvalidOperationException(
                "No IInfrastructureRegistrar implementation found in Rentier.Infrastructure.");

        var registrar = (IInfrastructureRegistrar)Activator.CreateInstance(registrarType)!;
        await registrar.RegisterServicesAsync(services, dbPath);

        await using var provider = services.BuildServiceProvider();
        await provider.GetRequiredService<IDatabaseInitializer>().InitializeAsync();

        Console.WriteLine("[SMOKE] All migrations applied. Artifact is functional. Exiting 0.");
        return 0;
    }
}
