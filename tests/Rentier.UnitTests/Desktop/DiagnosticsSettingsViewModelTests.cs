using ReactiveUI.Primitives;
using FluentAssertions;
using NSubstitute;
using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.Interfaces;
using Rentier.Desktop.Services;
using Rentier.Desktop.ViewModels;
using Xunit;

namespace Rentier.UnitTests.Desktop;

public class DiagnosticsSettingsViewModelTests
{
    private const string LOG_DIR = @"C:\Rentier\logs";

    private sealed record Harness(
        DiagnosticsSettingsViewModel Vm,
        IDiagnosticsService Diagnostics,
        IFolderLauncher Launcher,
        ICommandHandler<SetUserPreferenceCommand, Result<VoidResult, Error>> SetPreference);

    private static Harness Build(
        bool debugInitiallyOn = false,
        bool launcherOpens = true,
        bool preferenceSaves = true)
    {
        var diagnostics = Substitute.For<IDiagnosticsService>();
        diagnostics.LogDirectory.Returns(LOG_DIR);
        diagnostics.IsDebugLoggingEnabled.Returns(debugInitiallyOn);

        var launcher = DesktopTestDoubles.FolderLauncher(launcherOpens);

        var setPreference = Substitute.For<ICommandHandler<SetUserPreferenceCommand, Result<VoidResult, Error>>>();
        setPreference.HandleAsync(Arg.Any<SetUserPreferenceCommand>(), Arg.Any<CancellationToken>())
            .Returns(preferenceSaves
                ? Result<VoidResult, Error>.Success(VoidResult.Value)
                : Result<VoidResult, Error>.Failure(new Error("INFRASTRUCTURE_ERROR", "db unavailable")));

        return new Harness(
            new DiagnosticsSettingsViewModel(diagnostics, launcher, setPreference),
            diagnostics, launcher, setPreference);
    }

    // ── Initial state ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Constructor_DebugLoggingState_MatchesService(bool enabled)
    {
        Build(debugInitiallyOn: enabled).Vm.IsDebugLoggingEnabled.Should().Be(enabled);
    }

    [Fact]
    public void Constructor_LogDirectory_ComesFromService()
    {
        Build().Vm.LogDirectory.Should().Be(LOG_DIR);
    }

    [Fact]
    public void Constructor_DoesNotSaveAnything()
    {
        var h = Build(debugInitiallyOn: true);

        h.SetPreference.DidNotReceive().HandleAsync(Arg.Any<SetUserPreferenceCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Constructor_ErrorMessage_IsEmpty()
    {
        Build().Vm.ErrorMessage.Should().BeEmpty();
    }

    // ── Toggle ─────────────────────────────────────────────────────────────────

    [Fact]
    public void IsDebugLoggingEnabled_SetTrue_EnablesDebugLoggingOnService()
    {
        var h = Build();

        h.Vm.IsDebugLoggingEnabled = true;

        h.Diagnostics.Received(1).SetDebugLogging(true);
    }

    [Fact]
    public void IsDebugLoggingEnabled_SetFalse_DisablesDebugLoggingOnService()
    {
        var h = Build(debugInitiallyOn: true);

        h.Vm.IsDebugLoggingEnabled = false;

        h.Diagnostics.Received(1).SetDebugLogging(false);
    }

    [Fact]
    public void IsDebugLoggingEnabled_SetTrue_PersistsPreference()
    {
        var h = Build();

        h.Vm.IsDebugLoggingEnabled = true;

        h.SetPreference.Received(1).HandleAsync(
            Arg.Is<SetUserPreferenceCommand>(c => c.Key == UserPreferenceKeys.DEBUG_LOGGING && c.Value == "true"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void IsDebugLoggingEnabled_SetFalse_PersistsPreference()
    {
        var h = Build(debugInitiallyOn: true);

        h.Vm.IsDebugLoggingEnabled = false;

        h.SetPreference.Received(1).HandleAsync(
            Arg.Is<SetUserPreferenceCommand>(c => c.Key == UserPreferenceKeys.DEBUG_LOGGING && c.Value == "false"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void IsDebugLoggingEnabled_SetToSameValue_DoesNothing()
    {
        var h = Build(debugInitiallyOn: true);

        h.Vm.IsDebugLoggingEnabled = true;

        h.Diagnostics.DidNotReceive().SetDebugLogging(Arg.Any<bool>());
    }

    [Fact]
    public void IsDebugLoggingEnabled_PreferenceSaveSucceeds_LeavesNoError()
    {
        var h = Build();

        h.Vm.IsDebugLoggingEnabled = true;

        h.Vm.ErrorMessage.Should().BeEmpty();
    }

    [Fact]
    public void IsDebugLoggingEnabled_PreferenceSaveFails_ShowsErrorButStillAppliesSetting()
    {
        var h = Build(preferenceSaves: false);

        h.Vm.IsDebugLoggingEnabled = true;

        h.Vm.ErrorMessage.Should().NotBeEmpty();
        h.Diagnostics.Received(1).SetDebugLogging(true);
    }

    // ── Open logs folder ───────────────────────────────────────────────────────

    [Fact]
    public void OpenLogsFolderCommand_Executed_OpensTheLogDirectory()
    {
        var h = Build();

        h.Vm.OpenLogsFolderCommand.Execute().Subscribe();

        h.Launcher.Received(1).OpenAsync(LOG_DIR);
    }

    [Fact]
    public void OpenLogsFolderCommand_LauncherSucceeds_LeavesNoError()
    {
        var h = Build(launcherOpens: true);

        h.Vm.OpenLogsFolderCommand.Execute().Subscribe();

        h.Vm.ErrorMessage.Should().BeEmpty();
    }

    [Fact]
    public void OpenLogsFolderCommand_LauncherFails_ShowsTheFolderPathSoItCanBeOpenedByHand()
    {
        var h = Build(launcherOpens: false);

        h.Vm.OpenLogsFolderCommand.Execute().Subscribe();

        h.Vm.ErrorMessage.Should().Contain(LOG_DIR);
    }

    [Fact]
    public void OpenLogsFolderCommand_AfterEarlierFailure_ClearsTheError()
    {
        var h = Build(launcherOpens: false);
        h.Vm.OpenLogsFolderCommand.Execute().Subscribe();
        h.Launcher.OpenAsync(Arg.Any<string>()).Returns(true);

        h.Vm.OpenLogsFolderCommand.Execute().Subscribe();

        h.Vm.ErrorMessage.Should().BeEmpty();
    }
}
