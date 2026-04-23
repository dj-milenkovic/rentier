using FluentAssertions;
using Rentier.Desktop.ViewModels;
using Xunit;

namespace Rentier.UnitTests.Desktop;

/// <summary>
/// Unit tests for <see cref="NavigationEntry"/> group/child behaviour introduced in feature 036.
/// These tests exercise <see cref="NavigationEntry.ToggleExpanded"/>, parent references,
/// and backwards-compatibility defaults — all without any Avalonia UI infrastructure.
/// </summary>
public class NavigationEntryGroupTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static NavigationEntry MakeChild(string label, NavigationEntry? parent = null) =>
        new(label)
        {
            IndentLevel = 1,
            ParentGroup = parent,
        };

    private static NavigationEntry MakeGroup(string label, IReadOnlyList<NavigationEntry>? children = null) =>
        new(label)
        {
            IsGroup    = true,
            IsExpanded = true,
            Children   = children ?? [],
        };

    // ── ToggleExpanded tests ──────────────────────────────────────────────────

    [Fact]
    public void ToggleExpanded_WhenExpandedTrue_SetsChildrenIsVisibleFalse()
    {
        var child1 = new NavigationEntry("Child 1") { IsVisible = true };
        var child2 = new NavigationEntry("Child 2") { IsVisible = true };
        var group  = new NavigationEntry("Group")
        {
            IsGroup    = true,
            IsExpanded = true,
            Children   = [child1, child2],
        };

        group.ToggleExpanded();

        child1.IsVisible.Should().BeFalse("children should be hidden when group collapses");
        child2.IsVisible.Should().BeFalse("children should be hidden when group collapses");
    }

    [Fact]
    public void ToggleExpanded_WhenExpandedFalse_SetsChildrenIsVisibleTrue()
    {
        var child1 = new NavigationEntry("Child 1") { IsVisible = false };
        var child2 = new NavigationEntry("Child 2") { IsVisible = false };
        var group  = new NavigationEntry("Group")
        {
            IsGroup    = true,
            IsExpanded = false,
            Children   = [child1, child2],
        };

        group.ToggleExpanded();

        child1.IsVisible.Should().BeTrue("children should become visible when group expands");
        child2.IsVisible.Should().BeTrue("children should become visible when group expands");
    }

    [Fact]
    public void ToggleExpanded_FlipsIsExpanded()
    {
        var group = new NavigationEntry("Group")
        {
            IsGroup    = true,
            IsExpanded = true,
        };

        group.ToggleExpanded();
        group.IsExpanded.Should().BeFalse("first toggle collapses the group");

        group.ToggleExpanded();
        group.IsExpanded.Should().BeTrue("second toggle re-expands the group");
    }

    [Fact]
    public void ToggleExpanded_WithNoChildren_DoesNotThrow()
    {
        var group = new NavigationEntry("EmptyGroup")
        {
            IsGroup    = true,
            IsExpanded = true,
        };

        var act = () => group.ToggleExpanded();

        act.Should().NotThrow("toggling a group with no children is a valid no-op");
    }

    // ── Parent reference tests ────────────────────────────────────────────────

    [Fact]
    public void ChildEntry_HasCorrectParentGroupReference()
    {
        var group = MakeGroup("Settings");
        var child = MakeChild("Profile", parent: group);

        child.ParentGroup.Should().BeSameAs(group);
    }

    [Fact]
    public void ChildEntry_ParentGroup_CanBeNull_ForTopLevelEntries()
    {
        var topLevel = new NavigationEntry("Dashboard");

        topLevel.ParentGroup.Should().BeNull();
    }

    // ── Order and structure tests ─────────────────────────────────────────────

    [Fact]
    public void GroupEntry_Children_AreInCorrectOrder_ByLabel()
    {
        // Simulate the Settings group children in production order (Profile → Language)
        var profile   = new NavigationEntry("Profile")   { IndentLevel = 1 };
        var holidays  = new NavigationEntry("Holidays")  { IndentLevel = 1 };
        var mailboxes = new NavigationEntry("Mailboxes") { IndentLevel = 1 };
        var importers = new NavigationEntry("Importers") { IndentLevel = 1 };
        var language  = new NavigationEntry("Language")  { IndentLevel = 1 };

        var group = new NavigationEntry("Settings")
        {
            IsGroup    = true,
            IsExpanded = true,
            Children   = [profile, holidays, mailboxes, importers, language],
        };

        group.Children.Should().HaveCount(5);
        group.Children[0].Label.Should().Be("Profile");
        group.Children[1].Label.Should().Be("Holidays");
        group.Children[2].Label.Should().Be("Mailboxes");
        group.Children[3].Label.Should().Be("Importers");
        group.Children[4].Label.Should().Be("Language");
    }

    // ── Backwards-compatibility guard ─────────────────────────────────────────

    [Fact]
    public void NavigationEntry_DefaultIsGroup_IsFalse()
    {
        // Any existing navigation entry created without specifying IsGroup
        // must default to false — this is a backwards-compatibility guard.
        var entry = new NavigationEntry("Dashboard");

        entry.IsGroup.Should().BeFalse("IsGroup must default to false for backwards compatibility");
    }

    [Fact]
    public void NavigationEntry_DefaultIsExpanded_IsFalse()
    {
        var entry = new NavigationEntry("Dashboard");

        entry.IsExpanded.Should().BeFalse("IsExpanded must default to false for non-group entries");
    }

    [Fact]
    public void NavigationEntry_DefaultChildren_IsEmpty()
    {
        var entry = new NavigationEntry("Dashboard");

        entry.Children.Should().BeEmpty("non-group entries have no children by default");
    }

    [Fact]
    public void NavigationEntry_DefaultIndentLevel_IsZero()
    {
        var entry = new NavigationEntry("Dashboard");

        entry.IndentLevel.Should().Be(0, "top-level entries have no indentation by default");
    }
}
