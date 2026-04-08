using System.Reactive.Concurrency;
using System.Reactive.Linq;
using FluentAssertions;
using NSubstitute;
using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Application.Interfaces;
using Rentier.Application.Queries;
using Rentier.Desktop.ViewModels;
using Rentier.Domain.Enums;
using Xunit;

namespace Rentier.Desktop.Tests;

public sealed class ImporterSettingsViewModelTests
{
    private static IQueryHandler<GetImportersQuery, Result<IReadOnlyList<ImporterDto>, Error>> MockGetImporters()
        => Substitute.For<IQueryHandler<GetImportersQuery, Result<IReadOnlyList<ImporterDto>, Error>>>();

    private static IQueryHandler<GetTaxpayerProfileQuery, Result<TaxpayerProfileDto?, Error>> MockGetProfile()
        => Substitute.For<IQueryHandler<GetTaxpayerProfileQuery, Result<TaxpayerProfileDto?, Error>>>();

    private static IQueryHandler<GetMailboxesQuery, Result<IReadOnlyList<MailboxDto>, Error>> MockGetMailboxes()
        => Substitute.For<IQueryHandler<GetMailboxesQuery, Result<IReadOnlyList<MailboxDto>, Error>>>();

    private static ICommandHandler<AddImporterCommand, Result<Guid, Error>> MockAdd()
        => Substitute.For<ICommandHandler<AddImporterCommand, Result<Guid, Error>>>();

    private static ICommandHandler<UpdateImporterCommand, Result<VoidResult, Error>> MockUpdate()
        => Substitute.For<ICommandHandler<UpdateImporterCommand, Result<VoidResult, Error>>>();

    private static ICommandHandler<DeleteImporterCommand, Result<VoidResult, Error>> MockDelete()
        => Substitute.For<ICommandHandler<DeleteImporterCommand, Result<VoidResult, Error>>>();

    private static ImporterSettingsViewModel CreateVm(
        IQueryHandler<GetImportersQuery, Result<IReadOnlyList<ImporterDto>, Error>>? getImporters = null,
        IQueryHandler<GetTaxpayerProfileQuery, Result<TaxpayerProfileDto?, Error>>? getProfile = null,
        IQueryHandler<GetMailboxesQuery, Result<IReadOnlyList<MailboxDto>, Error>>? getMailboxes = null,
        ICommandHandler<AddImporterCommand, Result<Guid, Error>>? add = null,
        ICommandHandler<UpdateImporterCommand, Result<VoidResult, Error>>? update = null,
        ICommandHandler<DeleteImporterCommand, Result<VoidResult, Error>>? delete = null)
    {
        // Only set up defaults for mocks that were NOT provided by the caller
        if (getImporters == null)
        {
            getImporters = MockGetImporters();
            getImporters.HandleAsync(Arg.Any<GetImportersQuery>(), Arg.Any<CancellationToken>())
                .Returns(Result<IReadOnlyList<ImporterDto>, Error>.Success(new List<ImporterDto>().AsReadOnly()));
        }

        if (getProfile == null)
        {
            getProfile = MockGetProfile();
            getProfile.HandleAsync(Arg.Any<GetTaxpayerProfileQuery>(), Arg.Any<CancellationToken>())
                .Returns(Result<TaxpayerProfileDto?, Error>.Success(null));
        }

        if (getMailboxes == null)
        {
            getMailboxes = MockGetMailboxes();
            getMailboxes.HandleAsync(Arg.Any<GetMailboxesQuery>(), Arg.Any<CancellationToken>())
                .Returns(Result<IReadOnlyList<MailboxDto>, Error>.Success(new List<MailboxDto>().AsReadOnly()));
        }

        return new ImporterSettingsViewModel(
            getImporters, getProfile, getMailboxes,
            add ?? MockAdd(),
            update ?? MockUpdate(),
            delete ?? MockDelete(),
            ImmediateScheduler.Instance);
    }

    private static ImporterDto MakeImporterDto(string name = "Test Importer") =>
        new(Guid.NewGuid(), name, ReportType.IbkrCsv, null, null, "from@x.com", "Subject", @"\d+", "Notes");

    [Fact]
    public void WhenActivated_NoImporters_ImporterItemsIsEmpty()
    {
        var vm = CreateVm();

        using var activation = vm.Activator.Activate();

        vm.ImporterItems.Should().BeEmpty();
    }

    [Fact]
    public void WhenActivated_LoadsAvailableMailboxes()
    {
        var dto1 = new MailboxDto(Guid.NewGuid(), "imap1.example.com", 993, "user1@example.com", null, null);
        var dto2 = new MailboxDto(Guid.NewGuid(), "imap2.example.com", 993, "user2@example.com", null, null);

        var getMailboxes = MockGetMailboxes();
        getMailboxes.HandleAsync(Arg.Any<GetMailboxesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<MailboxDto>, Error>.Success(
                new List<MailboxDto> { dto1, dto2 }.AsReadOnly()));

        var vm = CreateVm(getMailboxes: getMailboxes);

        using var activation = vm.Activator.Activate();

        vm.AvailableMailboxes.Should().HaveCount(2);
    }

    [Fact]
    public void WhenActivated_LoadsAvailableProfiles()
    {
        var profile = new TaxpayerProfileDto(Guid.NewGuid(), "1234567890123", "John Doe", "123 St", "BEO", null, null);

        var getProfile = MockGetProfile();
        getProfile.HandleAsync(Arg.Any<GetTaxpayerProfileQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<TaxpayerProfileDto?, Error>.Success(profile));

        var vm = CreateVm(getProfile: getProfile);

        using var activation = vm.Activator.Activate();

        vm.AvailableProfiles.Should().HaveCount(1);
    }

    [Fact]
    public void SelectImporter_PopulatesFormFields()
    {
        var dto = MakeImporterDto("My Importer");
        var vm = CreateVm();

        var item = ImporterItemViewModel.From(dto);
        vm.ImporterItems.Add(item);

        vm.SelectedImporter = item;

        vm.DisplayName.Should().Be(dto.DisplayName);
        vm.ReportType.Should().Be(dto.ReportType);
        vm.FromFilter.Should().Be(dto.FromFilter);
        vm.SubjectFilter.Should().Be(dto.SubjectFilter);
        vm.AttachmentRegex.Should().Be(dto.AttachmentRegex);
        vm.PaymentNotes.Should().Be(dto.PaymentNotes);
    }

    [Fact]
    public void AddNewCommand_ClearsFormAndSetsEditModeFalse()
    {
        var dto = MakeImporterDto();
        var vm = CreateVm();

        var item = ImporterItemViewModel.From(dto);
        vm.ImporterItems.Add(item);
        vm.SelectedImporter = item;
        vm.DisplayName = "Some name";

        vm.AddNewCommand.Execute().Subscribe();

        vm.DisplayName.Should().Be(string.Empty);
        vm.IsEditMode.Should().BeFalse();
        vm.SelectedImporter.Should().BeNull();
    }

    [Fact]
    public async Task SaveCommand_NewImporter_CallsAddHandler()
    {
        var newId = Guid.NewGuid();
        var addHandler = MockAdd();
        addHandler.HandleAsync(Arg.Any<AddImporterCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<Guid, Error>.Success(newId));

        var getImporters = MockGetImporters();
        getImporters.HandleAsync(Arg.Any<GetImportersQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<ImporterDto>, Error>.Success(new List<ImporterDto>().AsReadOnly()));

        var updateHandler = MockUpdate();
        var vm = CreateVm(getImporters: getImporters, add: addHandler, update: updateHandler);
        vm.DisplayName = "New Importer";
        vm.IsEditMode = false;

        await vm.SaveCommand.Execute().FirstAsync();

        await addHandler.Received(1).HandleAsync(Arg.Any<AddImporterCommand>(), Arg.Any<CancellationToken>());
        await updateHandler.DidNotReceive().HandleAsync(Arg.Any<UpdateImporterCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveCommand_EditMode_CallsUpdateHandler()
    {
        var dto = MakeImporterDto("Test");
        var updateHandler = MockUpdate();
        updateHandler.HandleAsync(Arg.Any<UpdateImporterCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<VoidResult, Error>.Success(VoidResult.Value));

        var getImporters = MockGetImporters();
        getImporters.HandleAsync(Arg.Any<GetImportersQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<ImporterDto>, Error>.Success(new List<ImporterDto>().AsReadOnly()));

        var addHandler = MockAdd();
        var vm = CreateVm(getImporters: getImporters, add: addHandler, update: updateHandler);

        var item = ImporterItemViewModel.From(dto);
        vm.ImporterItems.Add(item);
        vm.SelectedImporter = item;
        // IsEditMode is now true because SelectedImporter was set

        await vm.SaveCommand.Execute().FirstAsync();

        await updateHandler.Received(1).HandleAsync(Arg.Any<UpdateImporterCommand>(), Arg.Any<CancellationToken>());
        await addHandler.DidNotReceive().HandleAsync(Arg.Any<AddImporterCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void DeleteCommand_CanExecute_OnlyWhenImporterSelectedAndEditMode()
    {
        var vm = CreateVm();

        // Initially: no selection, so CanExecute = false
        bool canExecuteInitial = false;
        vm.DeleteCommand.CanExecute.Subscribe(v => canExecuteInitial = v);
        canExecuteInitial.Should().BeFalse();

        // Select an importer
        var dto = MakeImporterDto();
        var item = ImporterItemViewModel.From(dto);
        vm.ImporterItems.Add(item);
        vm.SelectedImporter = item;

        bool canExecuteAfterSelect = false;
        vm.DeleteCommand.CanExecute.Subscribe(v => canExecuteAfterSelect = v);
        canExecuteAfterSelect.Should().BeTrue();
    }
}
