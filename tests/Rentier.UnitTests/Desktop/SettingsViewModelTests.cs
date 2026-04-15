using FluentAssertions;
using NSubstitute;
using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Application.Interfaces;
using Rentier.Application.Queries;
using System.Reactive.Linq;
using Xunit;

namespace Rentier.UnitTests;

public class SettingsViewModelTests
{
    private ICommandHandler<SaveTaxpayerProfileCommand, Result<VoidResult, Error>> MockSaveHandler()
        => Substitute.For<ICommandHandler<SaveTaxpayerProfileCommand, Result<VoidResult, Error>>>();

    private IQueryHandler<GetTaxpayerProfileQuery, Result<TaxpayerProfileDto?, Error>> MockGetHandler()
        => Substitute.For<IQueryHandler<GetTaxpayerProfileQuery, Result<TaxpayerProfileDto?, Error>>>();

    private Rentier.Desktop.ViewModels.ProfileSettingsViewModel CreateVm(
        ICommandHandler<SaveTaxpayerProfileCommand, Result<VoidResult, Error>>? save = null,
        IQueryHandler<GetTaxpayerProfileQuery, Result<TaxpayerProfileDto?, Error>>? get = null)
    {
        return new Rentier.Desktop.ViewModels.ProfileSettingsViewModel(
            save ?? MockSaveHandler(),
            get ?? MockGetHandler());
    }

    [Fact]
    public void SaveCommand_EmptyJmbg_CannotExecute()
    {
        var vm = CreateVm();
        vm.Jmbg = "";
        vm.FullName = "Test";
        vm.Address = "Addr";
        vm.OpstinaCode = "7101";

        bool canExecute = false;
        vm.SaveCommand.CanExecute.Subscribe(v => canExecute = v);

        canExecute.Should().BeFalse();
    }

    [Fact]
    public void SaveCommand_ValidFields_CanExecute()
    {
        var vm = CreateVm();
        vm.Jmbg = "1234567890123";
        vm.FullName = "Test User";
        vm.Address = "Test Address";
        vm.OpstinaCode = "7101";

        bool canExecute = false;
        vm.SaveCommand.CanExecute.Subscribe(v => canExecute = v);

        canExecute.Should().BeTrue();
    }

    [Fact]
    public void SaveCommand_JmbgNot13Digits_CannotExecute()
    {
        var vm = CreateVm();
        vm.Jmbg = "123456";  // too short
        vm.FullName = "Test User";
        vm.Address = "Test Address";
        vm.OpstinaCode = "7101";

        bool canExecute = false;
        vm.SaveCommand.CanExecute.Subscribe(v => canExecute = v);

        canExecute.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteSave_SuccessfulSave_SetsSuccessMessage()
    {
        var saveHandler = MockSaveHandler();
        saveHandler
            .HandleAsync(Arg.Any<SaveTaxpayerProfileCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<VoidResult, Error>.Success(VoidResult.Value));

        var vm = CreateVm(save: saveHandler);
        vm.Jmbg = "1234567890123";
        vm.FullName = "Marko";
        vm.Address = "Knez 1";
        vm.OpstinaCode = "7101";

        await vm.SaveCommand.Execute().FirstAsync();

        vm.SuccessMessage.Should().NotBeEmpty();
        vm.ErrorMessage.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteSave_FailedSave_SetsErrorMessage()
    {
        var saveHandler = MockSaveHandler();
        saveHandler
            .HandleAsync(Arg.Any<SaveTaxpayerProfileCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<VoidResult, Error>.Failure(Error.Domain("JMBG already exists")));

        var vm = CreateVm(save: saveHandler);
        vm.Jmbg = "1234567890123";
        vm.FullName = "Marko";
        vm.Address = "Knez 1";
        vm.OpstinaCode = "7101";

        await vm.SaveCommand.Execute().FirstAsync();

        vm.ErrorMessage.Should().NotBeEmpty();
        vm.SuccessMessage.Should().BeEmpty();
    }
}
