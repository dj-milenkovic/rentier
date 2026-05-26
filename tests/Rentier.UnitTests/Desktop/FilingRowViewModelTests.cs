using FluentAssertions;
using Rentier.Application.DTOs;
using Rentier.Desktop.ViewModels;
using Rentier.Domain.Enums;
using System.Reactive.Linq;
using Xunit;

namespace Rentier.UnitTests;

public class FilingRowViewModelTests
{
    private static FilingRowDto MakeDto(
        FilingStatus status = FilingStatus.Init,
        DateOnly? deadline = null,
        decimal taxPayable = 100m,
        string? paymentReference = null) =>
        new(
            Id: Guid.NewGuid(),
            Status: status,
            IncomeType: IncomeType.Dividend,
            PayingEntity: "ACME Corp",
            FilingDeadline: deadline ?? new DateOnly(2024, 4, 30),
            TaxPayable: taxPayable,
            PaymentReference: paymentReference);

    private static FilingRowViewModel MakeRowVm(FilingStatus status = FilingStatus.Init)
    {
        var dto = MakeDto(status: status);
        return FilingRowViewModel.From(dto, delegate { }, _ => { }, _ => { });
    }

    [Fact]
    public void DeadlineDisplay_FormatsAsYyyyMmDd()
    {
        var dto = MakeDto(deadline: new DateOnly(2025, 4, 30));
        var vm = FilingRowViewModel.From(dto, delegate { }, _ => { }, _ => { });

        vm.DeadlineDisplay.Should().Be("2025-04-30");
    }

    [Fact]
    public void TaxPayableDisplay_FormatsWithInvariantCultureAndRsdSuffix()
    {
        var dto = MakeDto(taxPayable: 1234.50m);
        var vm = FilingRowViewModel.From(dto, delegate { }, _ => { }, _ => { });

        vm.TaxPayableDisplay.Should().Be("1,234.50 RSD");
    }

    [Theory]
    [InlineData(FilingStatus.Init)]
    [InlineData(FilingStatus.Filed)]
    [InlineData(FilingStatus.Paid)]
    public void StatusDisplayText_ReturnsNonEmptyStringForEachStatus(FilingStatus status)
    {
        var vm = MakeRowVm(status);

        vm.StatusDisplayText.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void IsPaymentReferenceEditable_TrueOnlyWhenFiled()
    {
        var filedVm = MakeRowVm(FilingStatus.Filed);
        var initVm = MakeRowVm(FilingStatus.Init);
        var paidVm = MakeRowVm(FilingStatus.Paid);

        filedVm.IsPaymentReferenceEditable.Should().BeTrue();
        initVm.IsPaymentReferenceEditable.Should().BeFalse();
        paidVm.IsPaymentReferenceEditable.Should().BeFalse();
    }

    [Fact]
    public void AvailableNextStatuses_FromInit_ContainsFiled()
    {
        var vm = MakeRowVm(FilingStatus.Init);

        vm.AvailableNextStatuses.Should().BeEquivalentTo([FilingStatus.Filed]);
    }

    [Fact]
    public void AvailableNextStatuses_FromFiled_ContainsPaid()
    {
        var vm = MakeRowVm(FilingStatus.Filed);

        vm.AvailableNextStatuses.Should().BeEquivalentTo([FilingStatus.Paid]);
    }

    [Fact]
    public void AvailableNextStatuses_FromPaid_IsEmpty()
    {
        var vm = MakeRowVm(FilingStatus.Paid);

        vm.AvailableNextStatuses.Should().BeEmpty();
    }

    [Fact]
    public void From_MapsAllDtoProperties()
    {
        var id = Guid.NewGuid();
        var dto = new FilingRowDto(
            Id: id,
            Status: FilingStatus.Filed,
            IncomeType: IncomeType.Interest,
            PayingEntity: "Test Corp",
            FilingDeadline: new DateOnly(2025, 6, 15),
            TaxPayable: 500.00m,
            PaymentReference: "REF123");

        var vm = FilingRowViewModel.From(dto, delegate { }, _ => { }, _ => { });

        vm.Id.Should().Be(id);
        vm.Status.Should().Be(FilingStatus.Filed);
        vm.IncomeType.Should().Be(IncomeType.Interest);
        vm.PayingEntity.Should().Be("Test Corp");
        vm.FilingDeadline.Should().Be(new DateOnly(2025, 6, 15));
        vm.TaxPayable.Should().Be(500.00m);
        vm.PaymentReference.Should().Be("REF123");
    }

    [Fact]
    public void HasNextStatus_InitStatus_ReturnsTrue()
    {
        var vm = MakeRowVm(FilingStatus.Init);
        vm.HasNextStatus.Should().BeTrue();
    }

    [Fact]
    public void HasNextStatus_FiledStatus_ReturnsTrue()
    {
        var vm = MakeRowVm(FilingStatus.Filed);
        vm.HasNextStatus.Should().BeTrue();
    }

    [Fact]
    public void HasNextStatus_PaidStatus_ReturnsFalse()
    {
        var vm = MakeRowVm(FilingStatus.Paid);
        vm.HasNextStatus.Should().BeFalse();
    }

    [Fact]
    public void AdvanceStatusTooltip_InitStatus_ReturnsMarkAsFiled()
    {
        var vm = MakeRowVm(FilingStatus.Init);
        vm.AdvanceStatusTooltip.Should().Contain("Filed");
    }

    [Fact]
    public void AdvanceStatusTooltip_FiledStatus_ReturnsMarkAsPaid()
    {
        var vm = MakeRowVm(FilingStatus.Filed);
        vm.AdvanceStatusTooltip.Should().Contain("Paid");
    }

    [Fact]
    public void AdvanceStatusTooltip_PaidStatus_ReturnsNoFurtherTransitions()
    {
        var vm = MakeRowVm(FilingStatus.Paid);
        vm.AdvanceStatusTooltip.Should().Be("No further transitions");
    }

    [Fact]
    public void AdvanceStatusCommand_InitStatus_IsEnabled()
    {
        var vm = MakeRowVm(FilingStatus.Init);
        bool canExecute = false;
        vm.AdvanceStatusCommand.CanExecute.Take(1).Subscribe(v => canExecute = v);
        canExecute.Should().BeTrue();
    }

    [Fact]
    public void AdvanceStatusCommand_FiledStatus_IsEnabled()
    {
        var vm = MakeRowVm(FilingStatus.Filed);
        bool canExecute = false;
        vm.AdvanceStatusCommand.CanExecute.Take(1).Subscribe(v => canExecute = v);
        canExecute.Should().BeTrue();
    }

    [Fact]
    public void AdvanceStatusCommand_PaidStatus_IsDisabled()
    {
        var vm = MakeRowVm(FilingStatus.Paid);
        bool canExecute = true;
        vm.AdvanceStatusCommand.CanExecute.Take(1).Subscribe(v => canExecute = v);
        canExecute.Should().BeFalse();
    }

    [Fact]
    public void AdvanceStatusCommand_Execute_InvokesAdvanceStatusDelegate()
    {
        (Guid Id, FilingStatus NewStatus)? captured = null;
        var dto = MakeDto(status: FilingStatus.Init);
        var vm = FilingRowViewModel.From(dto, args => captured = args, _ => { }, _ => { });

        vm.AdvanceStatusCommand.Execute().Subscribe();

        captured.Should().NotBeNull();
        captured!.Value.Id.Should().Be(dto.Id);
        captured.Value.NewStatus.Should().Be(FilingStatus.Filed);
    }

    [Theory]
    [InlineData(FilingStatus.Init)]
    [InlineData(FilingStatus.Filed)]
    [InlineData(FilingStatus.Paid)]
    public void ExportCommand_AnyStatus_IsAlwaysEnabled(FilingStatus status)
    {
        var vm = MakeRowVm(status);
        bool canExecute = false;
        vm.ExportCommand.CanExecute.Take(1).Subscribe(v => canExecute = v);
        canExecute.Should().BeTrue();
    }

    [Theory]
    [InlineData(FilingStatus.Init)]
    [InlineData(FilingStatus.Filed)]
    [InlineData(FilingStatus.Paid)]
    public void DeleteCommand_AnyStatus_IsAlwaysEnabled(FilingStatus status)
    {
        var vm = MakeRowVm(status);
        bool canExecute = false;
        vm.DeleteCommand.CanExecute.Take(1).Subscribe(v => canExecute = v);
        canExecute.Should().BeTrue();
    }

    [Fact]
    public void ExportCommand_Execute_InvokesExportDelegate()
    {
        Guid? capturedId = null;
        var dto = MakeDto(status: FilingStatus.Init);
        var vm = FilingRowViewModel.From(dto, delegate { }, id => capturedId = id, _ => { });

        vm.ExportCommand.Execute().Subscribe();

        capturedId.Should().Be(dto.Id);
    }

    [Fact]
    public void DeleteCommand_Execute_InvokesDeleteDelegate()
    {
        Guid? capturedId = null;
        var dto = MakeDto(status: FilingStatus.Init);
        var vm = FilingRowViewModel.From(dto, delegate { }, _ => { }, id => capturedId = id);

        vm.DeleteCommand.Execute().Subscribe();

        capturedId.Should().Be(dto.Id);
    }
}
