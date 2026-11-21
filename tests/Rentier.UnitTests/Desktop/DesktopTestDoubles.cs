using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Application.Interfaces;
using Rentier.Desktop.Services;
using Rentier.Desktop.ViewModels;

namespace Rentier.UnitTests;

/// <summary>
/// NSubstitute factories for the Desktop UI services ViewModels depend on
/// (confirmation, file save, report import dialog). Assert interactions with
/// <c>Received()</c> on the returned substitute.
/// </summary>
internal static class DesktopTestDoubles
{
    /// <summary>A confirmation service that always answers <paramref name="answer"/>.</summary>
    public static IConfirmationService Confirmation(bool answer = false)
    {
        var confirmation = Substitute.For<IConfirmationService>();
        confirmation.ConfirmAsync(Arg.Any<ConfirmationRequest>()).Returns(answer);
        return confirmation;
    }

    /// <summary>A file save service where the user always picks a destination.</summary>
    public static IFileSaveService FileSave()
    {
        var fileSave = Substitute.For<IFileSaveService>();
        fileSave.SaveAsync(Arg.Any<FileSaveRequest>()).Returns(true);
        return fileSave;
    }

    /// <summary>
    /// Registers the Desktop UI services the way <c>CompositionRoot</c> does, but with
    /// substitutes for the dialog-backed ones (which need a real Avalonia window) and the
    /// real <see cref="NavigationService"/> so shell navigation can be exercised.
    /// </summary>
    public static IServiceCollection AddUiServiceDoubles(IServiceCollection services)
    {
        services.AddSingleton(_ => Confirmation());
        services.AddSingleton(_ => FileSave());
        services.AddSingleton(_ => ImportDialog());
        services.AddSingleton<NavigationService>();
        services.AddSingleton<INavigationService>(sp => sp.GetRequiredService<NavigationService>());
        services.AddSingleton<INavigationRequestSource>(sp => sp.GetRequiredService<NavigationService>());
        return services;
    }

    /// <summary>
    /// An update service that reports "no update available". The shell checks for updates as soon
    /// as it activates, so a bare substitute (whose check returns a null result) is not enough.
    /// </summary>
    public static IUpdateService UpdateService()
    {
        var updateService = Substitute.For<IUpdateService>();
        updateService.CheckForUpdatesAsync(Arg.Any<CancellationToken>())
            .Returns(new UpdateCheckResult(false, null));
        return updateService;
    }

    /// <summary>A folder launcher that reports <paramref name="opened"/> for every request.</summary>
    public static IFolderLauncher FolderLauncher(bool opened = true)
    {
        var launcher = Substitute.For<IFolderLauncher>();
        launcher.OpenAsync(Arg.Any<string>()).Returns(opened);
        return launcher;
    }

    /// <summary>
    /// A Diagnostics settings page backed by substitutes (Debug logging off, preference saves succeed),
    /// for shell tests that only need the page to exist.
    /// </summary>
    public static DiagnosticsSettingsViewModel DiagnosticsViewModel()
    {
        var diagnostics = Substitute.For<IDiagnosticsService>();
        diagnostics.LogDirectory.Returns(@"C:\logs");

        var setPreference = Substitute.For<ICommandHandler<SetUserPreferenceCommand, Result<VoidResult, Error>>>();
        setPreference.HandleAsync(Arg.Any<SetUserPreferenceCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<VoidResult, Error>.Success(VoidResult.Value));

        return new DiagnosticsSettingsViewModel(diagnostics, FolderLauncher(), setPreference, new LocalizationService());
    }

    /// <summary>An import dialog returning <paramref name="selection"/> (<c>null</c> = user cancelled).</summary>
    public static IReportImportDialogService ImportDialog(ReportImportSelection? selection = null)
    {
        var dialog = Substitute.For<IReportImportDialogService>();
        dialog.PickReportAsync().Returns(selection);
        return dialog;
    }
}
