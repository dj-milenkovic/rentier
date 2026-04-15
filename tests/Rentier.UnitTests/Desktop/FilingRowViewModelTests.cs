using FluentAssertions;
using Rentier.Application.DTOs;
using Rentier.Desktop.ViewModels;
using Rentier.Domain.Entities;
using Rentier.Domain.Enums;
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

    [Fact]
    public void DeadlineDisplay_FormatsAsYyyyMmDd()
    {
        var dto = MakeDto(deadline: new DateOnly(2025, 4, 30));
        var vm = FilingRowViewModel.From(dto);

        vm.DeadlineDisplay.Should().Be("2025-04-30");
    }

    [Fact]
    public void TaxPayableDisplay_FormatsWithInvariantCultureAndRsdSuffix()
    {
        var dto = MakeDto(taxPayable: 1234.50m);
        var vm = FilingRowViewModel.From(dto);

        vm.TaxPayableDisplay.Should().Be("1,234.50 RSD");
    }

    [Theory]
    [InlineData(FilingStatus.Init)]
    [InlineData(FilingStatus.Filed)]
    [InlineData(FilingStatus.Paid)]
    public void StatusDisplayText_ReturnsNonEmptyStringForEachStatus(FilingStatus status)
    {
        var dto = MakeDto(status: status);
        var vm = FilingRowViewModel.From(dto);

        vm.StatusDisplayText.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void IsPaymentReferenceEditable_TrueOnlyWhenFiled()
    {
        var filedVm = FilingRowViewModel.From(MakeDto(status: FilingStatus.Filed));
        var initVm = FilingRowViewModel.From(MakeDto(status: FilingStatus.Init));
        var paidVm = FilingRowViewModel.From(MakeDto(status: FilingStatus.Paid));

        filedVm.IsPaymentReferenceEditable.Should().BeTrue();
        initVm.IsPaymentReferenceEditable.Should().BeFalse();
        paidVm.IsPaymentReferenceEditable.Should().BeFalse();
    }

    [Fact]
    public void AvailableNextStatuses_FromInit_ContainsFiled()
    {
        var vm = FilingRowViewModel.From(MakeDto(status: FilingStatus.Init));

        vm.AvailableNextStatuses.Should().BeEquivalentTo([FilingStatus.Filed]);
    }

    [Fact]
    public void AvailableNextStatuses_FromFiled_ContainsPaid()
    {
        var vm = FilingRowViewModel.From(MakeDto(status: FilingStatus.Filed));

        vm.AvailableNextStatuses.Should().BeEquivalentTo([FilingStatus.Paid]);
    }

    [Fact]
    public void AvailableNextStatuses_FromPaid_IsEmpty()
    {
        var vm = FilingRowViewModel.From(MakeDto(status: FilingStatus.Paid));

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

        var vm = FilingRowViewModel.From(dto);

        vm.Id.Should().Be(id);
        vm.Status.Should().Be(FilingStatus.Filed);
        vm.IncomeType.Should().Be(IncomeType.Interest);
        vm.PayingEntity.Should().Be("Test Corp");
        vm.FilingDeadline.Should().Be(new DateOnly(2025, 6, 15));
        vm.TaxPayable.Should().Be(500.00m);
        vm.PaymentReference.Should().Be("REF123");
    }
}
