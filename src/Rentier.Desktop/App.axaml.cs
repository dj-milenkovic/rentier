using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.Interfaces;
using Rentier.Application.Queries;
using Rentier.Desktop.Composition;
using Rentier.Desktop.Services;
using Rentier.Desktop.ViewModels;
using Rentier.Desktop.Views;
using Rentier.Infrastructure;
using Rentier.Infrastructure.Persistence;

namespace Rentier.Desktop;

/// <summary>
/// DI composition root. Infrastructure services registered via AddInfrastructureServices.
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
            var dbPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Rentier", "rentier.db");
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(dbPath)!);

            var services = new ServiceCollection();
            await services.AddInfrastructureServicesAsync(dbPath);
            services.AddDesktopServices();
            var provider = services.BuildServiceProvider();

            // Apply EF migrations on startup
            await using var dbContext = provider.GetRequiredService<AppDbContext>();
            await dbContext.Database.MigrateAsync();

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
            var langResult = await getLangHandler.HandleAsync(new GetUserPreferenceQuery("Language"));
            var savedLang = langResult.IsSuccess ? langResult.Value : null;
            localizationService.SetCulture(savedLang ?? "sr-Latn");

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
            // Startup failure — show a minimal error window so the user has actionable information
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var errorWindow = new Avalonia.Controls.Window
                {
                    Title = "Startup Error",
                    Content = new Avalonia.Controls.TextBlock
                    {
                        Text = $"Rentier failed to start:\n\n{ex.Message}",
                        Margin = new Avalonia.Thickness(16),
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap
                    },
                    Width = 480,
                    Height = 200
                };
                desktop.MainWindow = errorWindow;
            }
        }
    }
}
