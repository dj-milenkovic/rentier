using System.Reactive.Concurrency;
using FluentAssertions;
using NSubstitute;
using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Application.Interfaces;
using Rentier.Application.Queries;
using Rentier.Desktop.ViewModels;
using Xunit;

namespace Rentier.UnitTests;

public class ProfileSettingsViewModelTests
{
    private static IQueryHandler<GetTaxpayerProfileQuery, Result<TaxpayerProfileDto?, Error>> MockGetHandler(
        TaxpayerProfileDto? dto = null)
    {
        var mock = Substitute.For<IQueryHandler<GetTaxpayerProfileQuery, Result<TaxpayerProfileDto?, Error>>>();
        mock.HandleAsync(Arg.Any<GetTaxpayerProfileQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<TaxpayerProfileDto?, Error>.Success(dto));
        return mock;
    }

    private static ICommandHandler<SaveTaxpayerProfileCommand, Result<VoidResult, Error>> MockSaveHandler(
        bool success = true,
        string? errorMessage = null)
    {
        var mock = Substitute.For<ICommandHandler<SaveTaxpayerProfileCommand, Result<VoidResult, Error>>>();
        mock.HandleAsync(Arg.Any<SaveTaxpayerProfileCommand>(), Arg.Any<CancellationToken>())
            .Returns(success
                ? Result<VoidResult, Error>.Success(VoidResult.Value)
                : Result<VoidResult, Error>.Failure(new Error("DOMAIN_ERROR", errorMessage ?? "Save failed")));
        return mock;
    }

    private static ProfileSettingsViewModel CreateVm(
        ICommandHandler<SaveTaxpayerProfileCommand, Result<VoidResult, Error>>? saveHandler = null,
        IQueryHandler<GetTaxpayerProfileQuery, Result<TaxpayerProfileDto?, Error>>? getHandler = null)
    {
        return new ProfileSettingsViewModel(
            saveHandler ?? MockSaveHandler(),
            getHandler ?? MockGetHandler());
    }

    [Fact]
    public void Constructor_InitializesWithEmptyFieldsAndNoError()
    {
        var vm = CreateVm();

        vm.Jmbg.Should().BeEmpty();
        vm.FullName.Should().BeEmpty();
        vm.Address.Should().BeEmpty();
        vm.OpstinaCode.Should().BeEmpty();
        vm.PhoneNumber.Should().BeEmpty();
        vm.Email.Should().BeEmpty();
        vm.IsLoading.Should().BeFalse();
        vm.ErrorMessage.Should().BeEmpty();
        vm.SuccessMessage.Should().BeEmpty();
    }

    [Fact]
    public async Task OnActivation_LoadsExistingProfile()
    {
        var dto = new TaxpayerProfileDto(
            Id: Guid.NewGuid(),
            Jmbg: "1234567890123",
            FullName: "John Doe",
            Address: "123 Main St",
            OpstinaCode: "018",
            PhoneNumber: "555-1234",
            Email: "john@example.com");

        var getHandler = MockGetHandler(dto);
        var vm = CreateVm(getHandler: getHandler);

        using var _ = vm.Activator.Activate();

        // Wait for async load to complete
        await Task.Delay(100);

        vm.Jmbg.Should().Be("1234567890123");
        vm.FullName.Should().Be("John Doe");
        vm.Address.Should().Be("123 Main St");
        vm.OpstinaCode.Should().Be("018");
        vm.PhoneNumber.Should().Be("555-1234");
        vm.Email.Should().Be("john@example.com");
    }

    [Fact]
    public async Task OnActivation_WhenNoProfileExists_FieldsRemainEmpty()
    {
        var getHandler = MockGetHandler(null);
        var vm = CreateVm(getHandler: getHandler);

        using var _ = vm.Activator.Activate();

        // Wait for async load to complete
        await Task.Delay(100);

        vm.Jmbg.Should().BeEmpty();
        vm.FullName.Should().BeEmpty();
        vm.Address.Should().BeEmpty();
        vm.OpstinaCode.Should().BeEmpty();
    }

    [Fact]
    public void SaveCommand_WithValidData_CallsSaveHandlerAndSetsSuccessMessage()
    {
        var saveHandler = MockSaveHandler(success: true);
        var vm = CreateVm(saveHandler: saveHandler);

        // Set valid data
        vm.Jmbg = "1234567890123";
        vm.FullName = "John Doe";
        vm.Address = "123 Main St";
        vm.OpstinaCode = "018";

        vm.SaveCommand.Execute().Subscribe();

        saveHandler.Received(1).HandleAsync(
            Arg.Is<SaveTaxpayerProfileCommand>(c =>
                c.Jmbg == "1234567890123" &&
                c.FullName == "John Doe" &&
                c.Address == "123 Main St" &&
                c.OpstinaCode == "018"),
            Arg.Any<CancellationToken>());

        vm.SuccessMessage.Should().NotBeEmpty();
        vm.ErrorMessage.Should().BeEmpty();
    }

    [Fact]
    public void SaveCommand_WithHandlerFailure_SetsErrorMessage()
    {
        var saveHandler = MockSaveHandler(success: false, errorMessage: "Profile validation failed");
        var vm = CreateVm(saveHandler: saveHandler);

        // Set valid data
        vm.Jmbg = "1234567890123";
        vm.FullName = "John Doe";
        vm.Address = "123 Main St";
        vm.OpstinaCode = "018";

        vm.SaveCommand.Execute().Subscribe();

        vm.ErrorMessage.Should().Be("Profile validation failed");
        vm.SuccessMessage.Should().BeEmpty();
    }

    [Fact]
    public void SaveCommand_CannotExecute_WhenJmbgIsNotThirteenDigits()
    {
        var vm = CreateVm();

        vm.Jmbg = "123";
        vm.FullName = "John Doe";
        vm.Address = "123 Main St";
        vm.OpstinaCode = "018";

        bool canExecute = false;
        vm.SaveCommand.CanExecute.Subscribe(x => canExecute = x);

        canExecute.Should().BeFalse();
    }

    [Fact]
    public void SaveCommand_CannotExecute_WhenRequiredFieldEmpty()
    {
        var vm = CreateVm();

        vm.Jmbg = "1234567890123";
        vm.FullName = "";
        vm.Address = "123 Main St";
        vm.OpstinaCode = "018";

        bool canExecute = false;
        vm.SaveCommand.CanExecute.Subscribe(x => canExecute = x);

        canExecute.Should().BeFalse();
    }

    [Fact]
    public void SaveCommand_CannotExecute_WhenJmbgContainsNonDigits()
    {
        var vm = CreateVm();

        vm.Jmbg = "12345678901AB";
        vm.FullName = "John Doe";
        vm.Address = "123 Main St";
        vm.OpstinaCode = "018";

        bool canExecute = false;
        vm.SaveCommand.CanExecute.Subscribe(x => canExecute = x);

        canExecute.Should().BeFalse();
    }

    [Fact]
    public void SaveCommand_CanExecute_WhenAllRequiredFieldsValid()
    {
        var vm = CreateVm();

        vm.Jmbg = "1234567890123";
        vm.FullName = "John Doe";
        vm.Address = "123 Main St";
        vm.OpstinaCode = "018";

        bool canExecute = false;
        vm.SaveCommand.CanExecute.Subscribe(x => canExecute = x);

        canExecute.Should().BeTrue();
    }
}
