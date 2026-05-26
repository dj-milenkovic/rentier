using System.Reactive;
using FluentAssertions;
using Rentier.Desktop.ViewModels;
using Xunit;

namespace Rentier.UnitTests.Desktop;

public class TextFilterFlyoutViewModelTests
{
    // ── Default state ────────────────────────────────────────────────────────

    [Fact]
    public void DefaultState_WorkingTextNull_IsActiveFalse_IsOpenFalse()
    {
        var vm = new TextFilterFlyoutViewModel();

        vm.WorkingText.Should().BeNull();
        vm.IsActive.Should().BeFalse();
        vm.IsOpen.Should().BeFalse();
        vm.GetCommittedValue().Should().BeNull();
    }

    // ── Apply ────────────────────────────────────────────────────────────────

    [Fact]
    public void Apply_WithNonEmptyText_CommitsTextAndIsActiveTrue()
    {
        var vm = new TextFilterFlyoutViewModel { WorkingText = "ACME" };

        vm.ApplyCommand.Execute().Subscribe();

        vm.GetCommittedValue().Should().Be("ACME");
        vm.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Apply_WithEmptyText_ClearsCommittedAndIsActiveFalse()
    {
        var vm = new TextFilterFlyoutViewModel();
        vm.WorkingText = "ACME";
        vm.ApplyCommand.Execute().Subscribe(); // commit ACME

        vm.IsOpen = true;           // re-open
        vm.WorkingText = "";        // clear text in flyout
        vm.ApplyCommand.Execute().Subscribe();

        vm.GetCommittedValue().Should().BeNull();
        vm.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Apply_WithNullText_ClearsCommittedAndIsActiveFalse()
    {
        var vm = new TextFilterFlyoutViewModel();
        vm.WorkingText = "ACME";
        vm.ApplyCommand.Execute().Subscribe();

        vm.IsOpen = true;
        vm.WorkingText = null;
        vm.ApplyCommand.Execute().Subscribe();

        vm.GetCommittedValue().Should().BeNull();
        vm.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Apply_FiresAppliedObservable()
    {
        var vm = new TextFilterFlyoutViewModel { WorkingText = "Test" };
        var appliedCount = 0;
        vm.Applied.Subscribe(_ => appliedCount++);

        vm.ApplyCommand.Execute().Subscribe();

        appliedCount.Should().Be(1);
    }

    [Fact]
    public void Apply_ClosesPopup()
    {
        var vm = new TextFilterFlyoutViewModel { WorkingText = "ACME" };
        vm.IsOpen = true;

        vm.ApplyCommand.Execute().Subscribe();

        vm.IsOpen.Should().BeFalse();
    }

    // ── Open → sync working from committed ──────────────────────────────────

    [Fact]
    public void OpenFlyout_WhenFilterActive_SyncsWorkingTextFromCommitted()
    {
        var vm = new TextFilterFlyoutViewModel { WorkingText = "ACME" };
        vm.ApplyCommand.Execute().Subscribe(); // commit "ACME"

        vm.WorkingText = "Changed";  // modify working externally

        vm.IsOpen = true;  // opening should reset WorkingText to committed

        vm.WorkingText.Should().Be("ACME");
    }

    [Fact]
    public void OpenFlyout_WhenNoFilter_WorkingTextIsNull()
    {
        var vm = new TextFilterFlyoutViewModel();
        // No committed value
        vm.WorkingText = "Pending";

        vm.IsOpen = true;

        vm.WorkingText.Should().BeNull("no committed value, working text resets");
    }

    // ── Dismiss without Apply ────────────────────────────────────────────────

    [Fact]
    public void DismissWithoutApply_DoesNotChangeCommittedValue()
    {
        var vm = new TextFilterFlyoutViewModel { WorkingText = "ACME" };
        vm.ApplyCommand.Execute().Subscribe(); // commit "ACME"

        vm.IsOpen = true;
        vm.WorkingText = "Different"; // edit in flyout
        vm.IsOpen = false;            // dismiss without apply

        vm.GetCommittedValue().Should().Be("ACME");
        vm.IsActive.Should().BeTrue();
    }

    // ── Clear ────────────────────────────────────────────────────────────────

    [Fact]
    public void Clear_ResetsCommittedAndWorkingTextAndIsActive()
    {
        var vm = new TextFilterFlyoutViewModel { WorkingText = "ACME" };
        vm.ApplyCommand.Execute().Subscribe();

        vm.Clear();

        vm.GetCommittedValue().Should().BeNull();
        vm.WorkingText.Should().BeNull();
        vm.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Clear_DoesNotFireApplied()
    {
        var vm = new TextFilterFlyoutViewModel();
        var appliedCount = 0;
        vm.Applied.Subscribe(_ => appliedCount++);

        vm.Clear();

        appliedCount.Should().Be(0);
    }

    // ── ToggleOpen ───────────────────────────────────────────────────────────

    [Fact]
    public void ToggleOpenCommand_TogglesIsOpen()
    {
        var vm = new TextFilterFlyoutViewModel();

        vm.ToggleOpenCommand.Execute().Subscribe();
        vm.IsOpen.Should().BeTrue();

        vm.ToggleOpenCommand.Execute().Subscribe();
        vm.IsOpen.Should().BeFalse();
    }
}
