using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.InteropServices;
using Avalonia;
using ReactiveUI.Avalonia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Rentier.Application.Interfaces;
using Rentier.Desktop.Composition;
using Rentier.Desktop.Logging;
using Rentier.Infrastructure;
using Rentier.Infrastructure.Updates;
using Serilog;
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

        // --smoke-test-update: exit 0 only if Velopack recognises this copy as installed, i.e.
        // the in-app updater will actually run. Checked after VelopackApp.Run() on purpose — that
        // is the state the real app sees. Used by release CI on the extracted portable layout.
        if (args.Contains("--smoke-test-update"))
            return RunUpdateLayoutSmokeTest();

        // Logging starts here, not in App: headless tests run App too and must not write
        // into the developer's real logs folder.
        var loggingSession = LoggingSession.Start(AppPaths.LogDirectory);
        GlobalExceptionLogging.Attach();
        LogStartupBanner();
        SyncInstalledAppsEntry(loggingSession);

        // No catch here: a crash escaping Main is logged (Fatal) and flushed by the
        // AppDomain.UnhandledException hook in GlobalExceptionLogging, so the runtime still
        // reports it exactly as before and it is written to the log once.
        try
        {
            return BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            Log.Information("Application exiting");
            Log.CloseAndFlush();
        }
    }

    private static AppBuilder BuildAvaloniaApp()
    => AppBuilder.Configure<App>()
        .UsePlatformDetect()
        .WithInterFont()
        .UseReactiveUI(rx => rx.WithExceptionHandler(new GlobalExceptionLogging.ReactiveExceptionObserver()));

    /// <summary>Environment facts useful for support. Deliberately no machine or user name.</summary>
    private static void LogStartupBanner()
    {
        var version = typeof(Program).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";

        Log.Information(
            "Rentier {Version} starting on {OS} ({Architecture}), .NET {Runtime}",
            version,
            RuntimeInformation.OSDescription,
            RuntimeInformation.ProcessArchitecture,
            Environment.Version);
    }


    /// <summary>
    /// After an in-app update, show the new version in Settings &gt; Apps (Windows installer
    /// installs only). Failures are logged inside and never stop the app.
    /// </summary>
    private static void SyncInstalledAppsEntry(LoggingSession loggingSession)
    {
        using var loggerFactory = new Serilog.Extensions.Logging.SerilogLoggerFactory(loggingSession.Logger);
        _ = InstalledAppsEntrySync.SyncThisInstall(
            new Microsoft.Extensions.Logging.Logger<InstalledAppsEntrySync>(loggerFactory));
    }


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
        // Process boundary: any failure becomes a non-zero exit code for the CI smoke step.
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[SMOKE] FAILED: {ex}");
            return 1;
        }
    }

    /// <summary>
    /// Fails when the update service would report "not installed" — the state in which every
    /// update check is skipped and the updater silently does nothing. Also fails when the update
    /// feed URL was not baked into the build (the service cannot be constructed).
    /// </summary>
    private static int RunUpdateLayoutSmokeTest()
    {
        try
        {
            // No logging session on this path; the result goes to the console for CI.
            using var updates = new VelopackUpdateService(NullLogger<VelopackUpdateService>.Instance);
            if (!updates.IsInstalled)
            {
                Console.Error.WriteLine(
                    "[SMOKE] FAILED: Velopack does not recognise this layout as installed; " +
                    "in-app updates would silently do nothing.");
                return 1;
            }

            Console.WriteLine("[SMOKE] Velopack layout recognised; in-app updates are enabled. Exiting 0.");
            return 0;
        }
        // Process boundary: any failure becomes a non-zero exit code for the CI smoke step.
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
        var dbPath = AppPaths.SmokeDatabasePath;
        Directory.CreateDirectory(AppPaths.RootDirectory);

        var services = new ServiceCollection();

        // Same logging + registration calls as App.axaml.cs, so the smoke test exercises the
        // real wiring. No session was started on this path, so logging is disabled (no file).
        var loggingSession = LoggingSession.CreateDisabled(AppPaths.LogDirectory);
        services.AddRentierLogging(loggingSession);
        using var startupLoggerFactory = new Serilog.Extensions.Logging.SerilogLoggerFactory(loggingSession.Logger);
        await services.AddInfrastructureServicesAsync(dbPath, startupLoggerFactory.CreateLogger("Startup"));

        await using var provider = services.BuildServiceProvider();
        await provider.GetRequiredService<IDatabaseInitializer>().InitializeAsync();

        Console.WriteLine("[SMOKE] All migrations applied. Artifact is functional. Exiting 0.");
        return 0;
    }
}
