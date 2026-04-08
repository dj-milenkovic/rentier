using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Rentier.Desktop.Composition;
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

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainVm = provider.GetRequiredService<MainWindowViewModel>();
            desktop.MainWindow = new MainWindow(mainVm);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
