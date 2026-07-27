using ReactiveUI.Primitives.Concurrency;
using ReactiveUI.Primitives;
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

namespace Rentier.UnitTests;

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
            new ImporterSettingsHandlers(
                getImporters, getProfile, getMailboxes,
                add ?? MockAdd(),
                update ?? MockUpdate(),
                delete ?? MockDelete()),
            ImmediateSequencer.Instance,
            confirmAction: (_, _, _, _) => Task.FromResult(true));
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

        await vm.SaveCommand.Execute().FirstAsync(TestContext.Current.CancellationToken);

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

        await vm.SaveCommand.Execute().FirstAsync(TestContext.Current.CancellationToken);

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

    [Fact]
    public async Task SaveCommand_EditMode_RepopulatesAllFieldsFromRefreshedDto()
    {
        var profileId = Guid.NewGuid();
        var mailboxId = Guid.NewGuid();
        var importerId = Guid.NewGuid();

        var refreshedDto = new ImporterDto(
            importerId, "Refreshed Name", ReportType.IbkrCsv,
            profileId, mailboxId,
            "fresh@x.com", "Fresh Subject", @"fresh\d+", "Fresh Notes");

        var updateHandler = MockUpdate();
        updateHandler.HandleAsync(Arg.Any<UpdateImporterCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<VoidResult, Error>.Success(VoidResult.Value));

        var getImporters = MockGetImporters();
        getImporters.HandleAsync(Arg.Any<GetImportersQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<ImporterDto>, Error>.Success(
                new List<ImporterDto> { refreshedDto }.AsReadOnly()));

        var vm = CreateVm(getImporters: getImporters, update: updateHandler);

        var profile = new TaxpayerProfileDto(profileId, "1234567890123", "John Doe", "123 St", "BEO", null, null);
        var mailbox = new MailboxDto(mailboxId, "imap.example.com", 993, "user@example.com", null, null);
        vm.AvailableProfiles.Add(profile);
        vm.AvailableMailboxes.Add(mailbox);

        var originalDto = new ImporterDto(importerId, "Old Name", ReportType.IbkrCsv,
            null, null, "old@x.com", "Old Subject", "old", "Old Notes");
        var item = ImporterItemViewModel.From(originalDto);
        vm.ImporterItems.Add(item);
        vm.SelectedImporter = item;

        await vm.SaveCommand.Execute().FirstAsync(TestContext.Current.CancellationToken);

        vm.DisplayName.Should().Be(refreshedDto.DisplayName);
        vm.ReportType.Should().Be(refreshedDto.ReportType);
        vm.SelectedProfile.Should().Be(profile);
        vm.SelectedMailbox.Should().Be(mailbox);
        vm.FromFilter.Should().Be(refreshedDto.FromFilter);
        vm.SubjectFilter.Should().Be(refreshedDto.SubjectFilter);
        vm.AttachmentRegex.Should().Be(refreshedDto.AttachmentRegex);
        vm.PaymentNotes.Should().Be(refreshedDto.PaymentNotes);
    }

    [Fact]
    public async Task SaveCommand_AddMode_RepopulatesAllFieldsFromRefreshedDto()
    {
        var profileId = Guid.NewGuid();
        var mailboxId = Guid.NewGuid();
        var newId = Guid.NewGuid();

        var savedDto = new ImporterDto(
            newId, "Saved Importer", ReportType.IbkrCsv,
            profileId, mailboxId,
            "new@x.com", "New Subject", @"new\d+", "New Notes");

        var addHandler = MockAdd();
        addHandler.HandleAsync(Arg.Any<AddImporterCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<Guid, Error>.Success(newId));

        var getImporters = MockGetImporters();
        getImporters.HandleAsync(Arg.Any<GetImportersQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<ImporterDto>, Error>.Success(
                new List<ImporterDto> { savedDto }.AsReadOnly()));

        var vm = CreateVm(getImporters: getImporters, add: addHandler);

        var profile = new TaxpayerProfileDto(profileId, "1234567890123", "John Doe", "123 St", "BEO", null, null);
        var mailbox = new MailboxDto(mailboxId, "imap.example.com", 993, "user@example.com", null, null);
        vm.AvailableProfiles.Add(profile);
        vm.AvailableMailboxes.Add(mailbox);

        vm.DisplayName = "Saved Importer";
        vm.IsEditMode = false;

        await vm.SaveCommand.Execute().FirstAsync(TestContext.Current.CancellationToken);

        vm.DisplayName.Should().Be(savedDto.DisplayName);
        vm.ReportType.Should().Be(savedDto.ReportType);
        vm.SelectedProfile.Should().Be(profile);
        vm.SelectedMailbox.Should().Be(mailbox);
        vm.FromFilter.Should().Be(savedDto.FromFilter);
        vm.SubjectFilter.Should().Be(savedDto.SubjectFilter);
        vm.AttachmentRegex.Should().Be(savedDto.AttachmentRegex);
        vm.PaymentNotes.Should().Be(savedDto.PaymentNotes);
        vm.IsEditMode.Should().BeTrue();
    }

    [Fact]
    public void SelectedImporter_SetToNull_ClearsAllFormFields()
    {
        var dto = MakeImporterDto("Full Importer");
        var vm = CreateVm();
        var item = ImporterItemViewModel.From(dto);
        vm.ImporterItems.Add(item);
        vm.SelectedImporter = item;

        // Verify fields are populated first
        vm.DisplayName.Should().Be(dto.DisplayName);

        // Deselect
        vm.SelectedImporter = null;

        vm.DisplayName.Should().Be(string.Empty);
        vm.ReportType.Should().Be(ReportType.IbkrCsv);
        vm.SelectedProfile.Should().BeNull();
        vm.SelectedMailbox.Should().BeNull();
        vm.FromFilter.Should().Be(string.Empty);
        vm.SubjectFilter.Should().Be(string.Empty);
        vm.AttachmentRegex.Should().Be(string.Empty);
        vm.PaymentNotes.Should().Be(string.Empty);
        vm.IsEditMode.Should().BeFalse();
    }

    [Fact]
    public void SelectedImporter_SwitchToAnotherImporter_OverwritesAllFields()
    {
        var profileId = Guid.NewGuid();
        var mailboxId = Guid.NewGuid();

        var dtoA = new ImporterDto(
            Guid.NewGuid(), "Importer A", ReportType.IbkrCsv,
            profileId, mailboxId,
            "a@x.com", "Subject A", @"a\d+", "Notes A");

        var dtoB = new ImporterDto(
            Guid.NewGuid(), "Importer B", ReportType.IbkrCsv,
            null, null,
            "b@x.com", "Subject B", string.Empty, "Notes B");

        var vm = CreateVm();
        var itemA = ImporterItemViewModel.From(dtoA);
        var itemB = ImporterItemViewModel.From(dtoB);
        vm.ImporterItems.Add(itemA);
        vm.ImporterItems.Add(itemB);

        var profile = new TaxpayerProfileDto(profileId, "1234567890123", "Jane Doe", "456 Ave", "BEO", null, null);
        var mailbox = new MailboxDto(mailboxId, "imap.example.com", 993, "user@example.com", null, null);
        vm.AvailableProfiles.Add(profile);
        vm.AvailableMailboxes.Add(mailbox);

        vm.SelectedImporter = itemA;
        vm.DisplayName.Should().Be(dtoA.DisplayName);

        vm.SelectedImporter = itemB;

        vm.DisplayName.Should().Be(dtoB.DisplayName);
        vm.SelectedProfile.Should().BeNull();
        vm.SelectedMailbox.Should().BeNull();
        vm.FromFilter.Should().Be(dtoB.FromFilter);
        vm.SubjectFilter.Should().Be(dtoB.SubjectFilter);
        vm.AttachmentRegex.Should().Be(string.Empty);
        vm.PaymentNotes.Should().Be(dtoB.PaymentNotes);
        vm.IsEditMode.Should().BeTrue();
    }

    [Fact]
    public async Task SaveCommand_EditMode_ItemVanishedAfterReload_ClearsForm()
    {
        var dto = MakeImporterDto("Vanishing Importer");

        var updateHandler = MockUpdate();
        updateHandler.HandleAsync(Arg.Any<UpdateImporterCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<VoidResult, Error>.Success(VoidResult.Value));

        var getImporters = MockGetImporters();
        getImporters.HandleAsync(Arg.Any<GetImportersQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<ImporterDto>, Error>.Success(
                new List<ImporterDto>().AsReadOnly())); // item not in refreshed list

        var vm = CreateVm(getImporters: getImporters, update: updateHandler);
        var item = ImporterItemViewModel.From(dto);
        vm.ImporterItems.Add(item);
        vm.SelectedImporter = item;

        await vm.SaveCommand.Execute().FirstAsync(TestContext.Current.CancellationToken);

        vm.SelectedImporter.Should().BeNull();
        vm.DisplayName.Should().Be(string.Empty);
        vm.ReportType.Should().Be(ReportType.IbkrCsv);
        vm.SelectedProfile.Should().BeNull();
        vm.SelectedMailbox.Should().BeNull();
        vm.FromFilter.Should().Be(string.Empty);
        vm.SubjectFilter.Should().Be(string.Empty);
        vm.AttachmentRegex.Should().Be(string.Empty);
        vm.PaymentNotes.Should().Be(string.Empty);
        vm.IsEditMode.Should().BeFalse();
    }

    [Fact]
    public async Task SaveCommand_EditMode_OnFailure_PreservesEdits()
    {
        var dto = MakeImporterDto("Original");

        var updateHandler = MockUpdate();
        updateHandler.HandleAsync(Arg.Any<UpdateImporterCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<VoidResult, Error>.Failure(new Error("ERR_SAVE", "Save failed")));

        var vm = CreateVm(update: updateHandler);
        var item = ImporterItemViewModel.From(dto);
        vm.ImporterItems.Add(item);
        vm.SelectedImporter = item;

        vm.DisplayName = "User Typed Name";
        vm.FromFilter = "typed@x.com";
        vm.SubjectFilter = "Typed Subject";
        vm.AttachmentRegex = @"typed\d+";
        vm.PaymentNotes = "Typed Notes";

        await vm.SaveCommand.Execute().FirstAsync(TestContext.Current.CancellationToken);

        vm.DisplayName.Should().Be("User Typed Name");
        vm.FromFilter.Should().Be("typed@x.com");
        vm.SubjectFilter.Should().Be("Typed Subject");
        vm.AttachmentRegex.Should().Be(@"typed\d+");
        vm.PaymentNotes.Should().Be("Typed Notes");
        vm.ErrorMessage.Should().Be("Save failed");
    }

    [Fact]
    public void AddNewCommand_ClearsAllEightFields()
    {
        var profileId = Guid.NewGuid();
        var mailboxId = Guid.NewGuid();

        var dto = new ImporterDto(
            Guid.NewGuid(), "Full Importer", ReportType.IbkrCsv,
            profileId, mailboxId,
            "full@x.com", "Full Subject", @"full\d+", "Full Notes");

        var vm = CreateVm();
        var item = ImporterItemViewModel.From(dto);
        vm.ImporterItems.Add(item);

        var profile = new TaxpayerProfileDto(profileId, "1234567890123", "Jane Doe", "456 Ave", "BEO", null, null);
        var mailbox = new MailboxDto(mailboxId, "imap.example.com", 993, "user@example.com", null, null);
        vm.AvailableProfiles.Add(profile);
        vm.AvailableMailboxes.Add(mailbox);

        vm.SelectedImporter = item;

        vm.DisplayName.Should().Be(dto.DisplayName); // sanity check
        vm.SelectedProfile.Should().Be(profile);
        vm.SelectedMailbox.Should().Be(mailbox);

        vm.AddNewCommand.Execute().Subscribe();

        vm.DisplayName.Should().Be(string.Empty);
        vm.ReportType.Should().Be(ReportType.IbkrCsv);
        vm.SelectedProfile.Should().BeNull();
        vm.SelectedMailbox.Should().BeNull();
        vm.FromFilter.Should().Be(string.Empty);
        vm.SubjectFilter.Should().Be(string.Empty);
        vm.AttachmentRegex.Should().Be(string.Empty);
        vm.PaymentNotes.Should().Be(string.Empty);
        vm.IsEditMode.Should().BeFalse();
        vm.SelectedImporter.Should().BeNull();
    }

    [Fact]
    public async Task DeleteCommand_WhenSuccessful_RemovesSelectedAndCallsReload()
    {
        var deleteHandler = MockDelete();
        deleteHandler.HandleAsync(Arg.Any<DeleteImporterCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<VoidResult, Error>.Success(VoidResult.Value));

        // After deletion, the reload returns an empty list
        var getImporters = MockGetImporters();
        getImporters.HandleAsync(Arg.Any<GetImportersQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<ImporterDto>, Error>.Success(new List<ImporterDto>().AsReadOnly()));

        var vm = CreateVm(getImporters: getImporters, delete: deleteHandler);
        var item = ImporterItemViewModel.From(MakeImporterDto("To Delete"));
        vm.ImporterItems.Add(item);
        vm.SelectedImporter = item; // triggers PopulateFormFromDto → IsEditMode = true

        await vm.DeleteCommand.Execute().FirstAsync(TestContext.Current.CancellationToken);

        await deleteHandler.Received(1).HandleAsync(Arg.Any<DeleteImporterCommand>(), Arg.Any<CancellationToken>());
        vm.SelectedImporter.Should().BeNull();
        vm.IsEditMode.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteCommand_WhenHandlerFails_SetsErrorMessage()
    {
        var deleteHandler = MockDelete();
        deleteHandler.HandleAsync(Arg.Any<DeleteImporterCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<VoidResult, Error>.Failure(new Error("ERR_DELETE", "Delete failed")));

        var vm = CreateVm(delete: deleteHandler);
        var item = ImporterItemViewModel.From(MakeImporterDto("To Delete"));
        vm.ImporterItems.Add(item);
        vm.SelectedImporter = item; // triggers PopulateFormFromDto → IsEditMode = true

        await vm.DeleteCommand.Execute().FirstAsync(TestContext.Current.CancellationToken);

        vm.ErrorMessage.Should().Be("Delete failed");
    }
}
