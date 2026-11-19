using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.Interfaces;
using Rentier.Application.Queries;
using Rentier.Desktop.Composition;
using Rentier.Desktop.Logging;
using Rentier.Desktop.Services;
using Rentier.Desktop.ViewModels;
using Rentier.Desktop.Views;
using Rentier.Infrastructure;

namespace Rentier.Desktop;

/// <summary>
/// Application bootstrap and DI composition root: wires Infrastructure
/// (AddInfrastructureServicesAsync) and Desktop/Application services
/// (CompositionRoot.AddDesktopServices) into one container.
/// </summary>
public partial class App : Avalonia.Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override async void OnFrameworkInitializationCompleted()
    {
        // Set up a fallback LocalizationService immediately so AXAML bindings work
        // even if DI initialization fails (e.g., in headless test environments).
        // English is used as the initial fallback — the saved language preference
        // (defaulting to sr-Latn) is applied below after DI and DB are initialized.
        var fallbackLocalizer = new LocalizationService();
        fallbackLocalizer.SetCulture("en");
        Avalonia.Application.Current!.Resources["Localizer"] = fallbackLocalizer;

        try
        {
            var dbPath = AppPaths.DatabasePath;
            System.IO.Directory.CreateDirectory(AppPaths.RootDirectory);

            var services = new ServiceCollection();

            // Logging first, so every ILogger<T> below resolves to the file logger. When no
            // session was started (headless tests) it is a disabled session that writes nowhere.
            var loggingSession = LoggingSession.Current ?? LoggingSession.CreateDisabled(AppPaths.LogDirectory);
            services.AddRentierLogging(loggingSession);

            // Composition root: Infrastructure is wired explicitly, so a missing or renamed
            // registration entry point is a compile error rather than a startup failure.
            using (var startupLoggerFactory = new Serilog.Extensions.Logging.SerilogLoggerFactory(loggingSession.Logger))
            {
                await services.AddInfrastructureServicesAsync(
                    dbPath, startupLoggerFactory.CreateLogger("Startup"));
            }

            services.AddDesktopServices();
            var provider = services.BuildServiceProvider();

            // Apply EF migrations via the abstraction — Desktop no longer references AppDbContext
            await provider.GetRequiredService<IDatabaseInitializer>().InitializeAsync();

            // Seed holiday defaults if the database has no holiday configuration yet
            var seedHandler = provider.GetRequiredService<
                ICommandHandler<EnsureHolidaysSeededCommand, Result<bool, Error>>>();
            await seedHandler.HandleAsync(new EnsureHolidaysSeededCommand());

            // Apply the user's saved theme before the window is shown
            var themeService = provider.GetRequiredService<IThemeService>();
            Services.ThemeService.ApplyOnStartup(themeService.GetPreference());

            // T032: Read saved language preference, apply culture, then register Localizer resource
            var localizationService = provider.GetRequiredService<ILocalizationService>();
            var getLangHandler = provider.GetRequiredService<
                IQueryHandler<GetUserPreferenceQuery, Result<string?, Error>>>();
            var langResult = await getLangHandler.HandleAsync(new GetUserPreferenceQuery(UserPreferenceKeys.LANGUAGE));
            var savedLang = langResult.IsSuccess ? langResult.Value : null;
            localizationService.SetCulture(savedLang ?? "sr-Latn");

            // Restore the Debug-logging toggle. It lives in the database, so startup up to this
            // point logs at Information; the switch takes effect immediately from here on.
            var debugResult = await getLangHandler.HandleAsync(new GetUserPreferenceQuery(UserPreferenceKeys.DEBUG_LOGGING));
            provider.GetRequiredService<IDiagnosticsService>()
                .SetDebugLogging(debugResult.IsSuccess && debugResult.Value == "true");

            // T024: Set the DI singleton as the AXAML Localizer resource (NOT a new instance)
            // <!-- Localizer resource is set programmatically in App.axaml.cs -->
            Avalonia.Application.Current!.Resources["Localizer"] = localizationService;

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var mainVm = provider.GetRequiredService<MainWindowViewModel>();
                desktop.MainWindow = new MainWindow(mainVm);
            }

            base.OnFrameworkInitializationCompleted();
        }
        catch (Exception ex)
        {
            Serilog.Log.Fatal(ex, "Startup failed");

            // Startup failure — show a minimal error window so the user has actionable information
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var errorWindow = new Avalonia.Controls.Window
                {
                    Title = "Startup Error",
                    Content = new Avalonia.Controls.TextBlock
                    {
                        Text = $"Rentier failed to start:\n\n{ex.Message}\n\nDetails were written to the log folder:\n{AppPaths.LogDirectory}",
                        Margin = new Avalonia.Thickness(16),
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap
                    },
                    Width = 480,
                    Height = 260
                };
                desktop.MainWindow = errorWindow;
            }
        }
    }
}

