using System.Reactive;
using FluentAssertions;
using Rentier.Desktop.Models;
using Rentier.Desktop.ViewModels;
using Rentier.Domain.Enums;
using Xunit;

namespace Rentier.UnitTests.Desktop;

public class EnumFilterFlyoutViewModelTests
{
    private static EnumFilterFlyoutViewModel<FilingStatus> CreateVm() =>
        new(new[]
        {
            new CheckableItem<FilingStatus>("Init",  FilingStatus.Init),
            new CheckableItem<FilingStatus>("Filed", FilingStatus.Filed),
            new CheckableItem<FilingStatus>("Paid",  FilingStatus.Paid),
        });

    // ── Default state ────────────────────────────────────────────────────────

    [Fact]
    public void DefaultState_AllItemsChecked_IsActiveFalse()
    {
        var vm = CreateVm();

        vm.WorkingItems.Should().AllSatisfy(i => i.IsChecked.Should().BeTrue());
        vm.IsActive.Should().BeFalse();
        vm.GetCommittedValues().Should().BeNull();
    }

    [Fact]
    public void DefaultState_IsOpenFalse()
    {
        var vm = CreateVm();

        vm.IsOpen.Should().BeFalse();
    }

    // ── SelectAll / ClearAll ─────────────────────────────────────────────────

    [Fact]
    public void SelectAllCommand_ChecksAllWorkingItems()
    {
        var vm = CreateVm();
        vm.WorkingItems[0].IsChecked = false;
        vm.WorkingItems[1].IsChecked = false;

        vm.SelectAllCommand.Execute().Subscribe();

        vm.WorkingItems.Should().AllSatisfy(i => i.IsChecked.Should().BeTrue());
    }

    [Fact]
    public void ClearAllCommand_UncheckAllWorkingItems()
    {
        var vm = CreateVm();

        vm.ClearAllCommand.Execute().Subscribe();

        vm.WorkingItems.Should().AllSatisfy(i => i.IsChecked.Should().BeFalse());
    }

    [Fact]
    public void SelectAllClearAll_DoNotCommitOrCloseOrFireApplied()
    {
        var vm = CreateVm();
        var appliedCount = 0;
        vm.Applied.Subscribe(_ => appliedCount++);

        vm.ClearAllCommand.Execute().Subscribe();
        vm.SelectAllCommand.Execute().Subscribe();

        appliedCount.Should().Be(0, "SelectAll/ClearAll do not commit");
        vm.IsOpen.Should().BeFalse();
        vm.GetCommittedValues().Should().BeNull("committed state unchanged");
    }

    // ── Apply — all checked ──────────────────────────────────────────────────

    [Fact]
    public void Apply_WhenAllChecked_CommitsNullAndIsActiveFalse()
    {
        var vm = CreateVm();
        // All items start checked
        vm.ApplyCommand.Execute().Subscribe();

        vm.GetCommittedValues().Should().BeNull("all checked = no filter");
        vm.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Apply_WhenAllChecked_ClosesPopup()
    {
        var vm = CreateVm();
        vm.IsOpen = true;

        vm.ApplyCommand.Execute().Subscribe();

        vm.IsOpen.Should().BeFalse();
    }

    // ── Apply — partial selection ────────────────────────────────────────────

    [Fact]
    public void Apply_WhenSomeChecked_CommitsCheckedValuesAndIsActiveTrue()
    {
        var vm = CreateVm();
        vm.WorkingItems[2].IsChecked = false; // Uncheck Paid

        vm.ApplyCommand.Execute().Subscribe();

        vm.GetCommittedValues().Should().NotBeNull();
        vm.GetCommittedValues()!.Should().Contain(FilingStatus.Init);
        vm.GetCommittedValues()!.Should().Contain(FilingStatus.Filed);
        vm.GetCommittedValues()!.Should().NotContain(FilingStatus.Paid);
        vm.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Apply_FiresAppliedObservable()
    {
        var vm = CreateVm();
        var appliedCount = 0;
        vm.Applied.Subscribe(_ => appliedCount++);

        vm.ApplyCommand.Execute().Subscribe();

        appliedCount.Should().Be(1);
    }

    // ── Open → sync working from committed ──────────────────────────────────

    [Fact]
    public void OpenFlyout_WhenFilterActive_SyncsWorkingFromCommitted()
    {
        var vm = CreateVm();
        // Apply a partial filter first
        vm.WorkingItems[2].IsChecked = false;
        vm.ApplyCommand.Execute().Subscribe();

        // Modify working items while flyout is closed (simulates external Clear)
        vm.WorkingItems[0].IsChecked = false;
        vm.WorkingItems[2].IsChecked = true;

        // Open the flyout — should reset to committed state
        vm.IsOpen = true;

        vm.WorkingItems[0].IsChecked.Should().BeTrue("Init was in committed set");
        vm.WorkingItems[1].IsChecked.Should().BeTrue("Filed was in committed set");
        vm.WorkingItems[2].IsChecked.Should().BeFalse("Paid was NOT in committed set");
    }

    [Fact]
    public void OpenFlyout_WhenNoFilter_AllItemsChecked()
    {
        var vm = CreateVm();
        // Uncheck some and apply (all checked = no filter)
        vm.ApplyCommand.Execute().Subscribe(); // commits null

        // Mess with working items
        vm.WorkingItems[0].IsChecked = false;

        // Open again
        vm.IsOpen = true;

        vm.WorkingItems.Should().AllSatisfy(i => i.IsChecked.Should().BeTrue());
    }

    // ── Dismiss without Apply ────────────────────────────────────────────────

    [Fact]
    public void DismissWithoutApply_DoesNotChangeCommittedValues()
    {
        var vm = CreateVm();
        // Apply Init+Filed filter
        vm.WorkingItems[2].IsChecked = false;
        vm.ApplyCommand.Execute().Subscribe();
        var committedBefore = vm.GetCommittedValues()!.ToHashSet();

        // Open, modify working, then dismiss (set IsOpen=false without Apply)
        vm.IsOpen = true;
        vm.WorkingItems[0].IsChecked = false; // modify working copy
        vm.IsOpen = false;

        vm.GetCommittedValues().Should().BeEquivalentTo(committedBefore);
        vm.IsActive.Should().BeTrue("committed filter still active");
    }

    // ── Clear ────────────────────────────────────────────────────────────────

    [Fact]
    public void Clear_ResetsCommittedToNullAndIsActiveFalse()
    {
        var vm = CreateVm();
        vm.WorkingItems[2].IsChecked = false;
        vm.ApplyCommand.Execute().Subscribe();

        vm.Clear();

        vm.GetCommittedValues().Should().BeNull();
        vm.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Clear_SyncsWorkingItemsToAllChecked()
    {
        var vm = CreateVm();
        vm.WorkingItems[2].IsChecked = false;
        vm.ApplyCommand.Execute().Subscribe();

        vm.Clear();

        vm.WorkingItems.Should().AllSatisfy(i => i.IsChecked.Should().BeTrue());
    }

    [Fact]
    public void Clear_DoesNotFireApplied()
    {
        var vm = CreateVm();
        var appliedCount = 0;
        vm.Applied.Subscribe(_ => appliedCount++);

        vm.Clear();

        appliedCount.Should().Be(0);
    }

    // ── ToggleOpen ───────────────────────────────────────────────────────────

    [Fact]
    public void ToggleOpenCommand_TogglesIsOpen()
    {
        var vm = CreateVm();

        vm.ToggleOpenCommand.Execute().Subscribe();
        vm.IsOpen.Should().BeTrue();

        vm.ToggleOpenCommand.Execute().Subscribe();
        vm.IsOpen.Should().BeFalse();
    }
}
