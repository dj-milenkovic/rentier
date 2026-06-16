using System.Reactive.Concurrency;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAssertions;
using NSubstitute;
using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Application.Interfaces;
using Rentier.Application.Queries;
using Rentier.Desktop.ViewModels;
using Rentier.Desktop.Views;
using Xunit;

namespace Rentier.UnitTests;

/// <summary>
/// Headless UI tests for <see cref="ProfileSettingsView"/>.
/// Verifies rendering and control state — only what cannot be covered at the ViewModel level.
/// </summary>
[Trait("Category", "UI")]
[Collection("HeadlessUI")]
public class ProfileSettingsViewHeadlessTests
{
    [AvaloniaFact]
    public void ProfileSettingsView_WhenCreated_RendersWithoutError()
    {
        // Arrange
        var vm = CreateMinimalProfileViewModel();
        var view = new ProfileSettingsView { DataContext = vm };
        var window = new Window { Content = view, Width = 800, Height = 600 };

        // Act
        window.Show();

        // Assert
        view.Should().NotBeNull();
        window.IsVisible.Should().BeTrue();

        window.Close();
    }

    [AvaloniaFact]
    public void ProfileSettingsView_WhenJmbgIsEmpty_SaveButtonIsDisabled()
    {
        // Arrange
        var vm = CreateMinimalProfileViewModel();
        vm.Jmbg = string.Empty;
        vm.FullName = "Test User";
        vm.Address = "Test Address";
        vm.OpstinaCode = "018";

        var view = new ProfileSettingsView { DataContext = vm };
        var window = new Window { Content = view, Width = 800, Height = 600 };
        window.Show();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        // Act — find the button by its bound command
        var saveButton = window.GetVisualDescendants()
            .OfType<Button>()
            .FirstOrDefault(b => b.Command == vm.SaveCommand);

        // Assert — verify the binding is in place and the command reports CanExecute=false.
        // Checking button.Command.CanExecute() is more reliable than button.IsEnabled in
        // headless tests, because Avalonia schedules the IsEnabled update asynchronously
        // via RxSchedulers.MainThreadScheduler when there is no ImmediateScheduler override.
        saveButton.Should().NotBeNull("the Save button should be bound to SaveCommand");
        saveButton!.Command?.CanExecute(saveButton.CommandParameter).Should().BeFalse(
            "JMBG is empty so canSave should be false");

        window.Close();
    }

    [AvaloniaFact]
    public void ProfileSettingsView_WhenRequiredFieldsAreValid_SaveButtonIsEnabled()
    {
        // Arrange
        var vm = CreateMinimalProfileViewModel();
        vm.Jmbg = "1234567890123"; // exactly 13 digits
        vm.FullName = "Test User";
        vm.Address = "Test Address";
        vm.OpstinaCode = "018";

        var view = new ProfileSettingsView { DataContext = vm };
        var window = new Window { Content = view, Width = 800, Height = 600 };
        window.Show();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        // Act
        var saveButton = window.GetVisualDescendants()
            .OfType<Button>()
            .FirstOrDefault(b => b.Command == vm.SaveCommand);

        // Assert
        saveButton.Should().NotBeNull();
        saveButton!.IsEnabled.Should().BeTrue();

        window.Close();
    }

    [AvaloniaFact]
    public void ProfileSettingsView_WhenJmbgHasInvalidLength_SaveButtonIsDisabled()
    {
        // Arrange
        var vm = CreateMinimalProfileViewModel();
        vm.Jmbg = "018"; // only 5 digits — fails the length == 13 check
        vm.FullName = "Test User";
        vm.Address = "Test Address";
        vm.OpstinaCode = "018";

        var view = new ProfileSettingsView { DataContext = vm };
        var window = new Window { Content = view, Width = 800, Height = 600 };
        window.Show();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        // Act — find the button by its bound command
        var saveButton = window.GetVisualDescendants()
            .OfType<Button>()
            .FirstOrDefault(b => b.Command == vm.SaveCommand);

        // Assert — verify binding is in place and command reports CanExecute=false
        saveButton.Should().NotBeNull("the Save button should be bound to SaveCommand");
        saveButton!.Command?.CanExecute(saveButton.CommandParameter).Should().BeFalse(
            "JMBG has only 5 digits so canSave should be false");

        window.Close();
    }

    [AvaloniaFact]
    public void ProfileSettingsView_WhenLoadSucceeds_FieldsArePopulated()
    {
        // Arrange
        var expectedProfile = new TaxpayerProfileDto(
            Guid.NewGuid(),
            "1234567890123",
            "Test User",
            "Test Address",
            "018",
            null,
            null);

        var getHandler = Substitute.For<IQueryHandler<GetTaxpayerProfileQuery, Result<TaxpayerProfileDto?, Error>>>();
        getHandler.HandleAsync(Arg.Any<GetTaxpayerProfileQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<TaxpayerProfileDto?, Error>.Success(expectedProfile));

        var vm = CreateMinimalProfileViewModel(getHandler: getHandler);

        // Act — activate triggers WhenActivated → LoadAsync
        using var activation = vm.Activator.Activate();
        Dispatcher.UIThread.RunJobs();

        // Assert
        vm.Jmbg.Should().Be("1234567890123");
        vm.FullName.Should().Be("Test User");
    }

    [AvaloniaFact]
    public void ProfileSettingsView_WhenIsLoading_ProgressBarIsVisible()
    {
        // Arrange — stuck save so IsLoading stays true
        var tcs = new TaskCompletionSource<Result<VoidResult, Error>>();
        var saveHandler = Substitute.For<ICommandHandler<SaveTaxpayerProfileCommand, Result<VoidResult, Error>>>();
        saveHandler.HandleAsync(Arg.Any<SaveTaxpayerProfileCommand>(), Arg.Any<CancellationToken>())
            .Returns(_ => tcs.Task);

        var vm = CreateMinimalProfileViewModel(saveHandler: saveHandler);

        // Activate so ThrownExceptions has a subscriber — prevents unhandled exception leakage
        using var activation = vm.Activator.Activate();

        // Set valid fields so canSave = true
        vm.Jmbg = "1234567890123";
        vm.FullName = "Test User";
        vm.Address = "Test Address";
        vm.OpstinaCode = "018";

        var view = new ProfileSettingsView { DataContext = vm };
        var window = new Window { Content = view, Width = 800, Height = 600 };
        window.Show();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        // Act — trigger save (will be stuck in tcs.Task)
        vm.SaveCommand.Execute().Subscribe();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        // Assert
        var progressBar = window.GetVisualDescendants().OfType<ProgressBar>().First();
        progressBar.IsVisible.Should().BeTrue();

        // Cleanup: complete with a failure result (not Cancel) to avoid unhandled
        // TaskCanceledException propagating across test boundaries.
        tcs.TrySetResult(Result<VoidResult, Error>.Failure(Error.Infrastructure("test cleanup")));
        window.Close();
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static ProfileSettingsViewModel CreateMinimalProfileViewModel(
        ICommandHandler<SaveTaxpayerProfileCommand, Result<VoidResult, Error>>? saveHandler = null,
        IQueryHandler<GetTaxpayerProfileQuery, Result<TaxpayerProfileDto?, Error>>? getHandler = null)
    {
        saveHandler ??= Substitute.For<ICommandHandler<SaveTaxpayerProfileCommand, Result<VoidResult, Error>>>();

        if (getHandler == null)
        {
            getHandler = Substitute.For<IQueryHandler<GetTaxpayerProfileQuery, Result<TaxpayerProfileDto?, Error>>>();
            getHandler.HandleAsync(Arg.Any<GetTaxpayerProfileQuery>(), Arg.Any<CancellationToken>())
                .Returns(Result<TaxpayerProfileDto?, Error>.Success(null));
        }

        return new ProfileSettingsViewModel(saveHandler, getHandler);
    }
}
