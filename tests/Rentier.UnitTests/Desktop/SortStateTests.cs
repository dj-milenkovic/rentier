using FluentAssertions;
using Rentier.Application.Enums;
using Rentier.Desktop.ViewModels;
using Xunit;

namespace Rentier.UnitTests;

public class SortStateTests
{
    private const FilingSortColumn COLUMN_A = FilingSortColumn.FilingDeadline;
    private const FilingSortColumn COLUMN_B = FilingSortColumn.PayingEntity;

    [Fact]
    public void Constructor_NoArguments_IsUnsorted()
    {
        var sort = new SortState<FilingSortColumn>();

        sort.Should().BeEquivalentTo(new { Column = (FilingSortColumn?)null, Descending = false });
    }

    [Fact]
    public void Cycle_FromUnsorted_SortsAscendingAndReportsNewColumn()
    {
        var sort = new SortState<FilingSortColumn>();

        var columnChanged = sort.Cycle(COLUMN_A);

        new { columnChanged, sort.Column, sort.Descending }
            .Should().BeEquivalentTo(new { columnChanged = true, Column = (FilingSortColumn?)COLUMN_A, Descending = false });
    }

    [Fact]
    public void Cycle_SameColumnAscending_SwitchesToDescendingAndKeepsPage()
    {
        var sort = new SortState<FilingSortColumn>(COLUMN_A, descending: false);

        var columnChanged = sort.Cycle(COLUMN_A);

        new { columnChanged, sort.Column, sort.Descending }
            .Should().BeEquivalentTo(new { columnChanged = false, Column = (FilingSortColumn?)COLUMN_A, Descending = true });
    }

    [Fact]
    public void Cycle_SameColumnDescending_ClearsSortAndKeepsPage()
    {
        var sort = new SortState<FilingSortColumn>(COLUMN_A, descending: true);

        var columnChanged = sort.Cycle(COLUMN_A);

        new { columnChanged, sort.Column, sort.Descending }
            .Should().BeEquivalentTo(new { columnChanged = false, Column = (FilingSortColumn?)null, Descending = false });
    }

    [Fact]
    public void Cycle_DifferentColumnWhileDescending_SortsNewColumnAscendingAndReportsChange()
    {
        var sort = new SortState<FilingSortColumn>(COLUMN_A, descending: true);

        var columnChanged = sort.Cycle(COLUMN_B);

        new { columnChanged, sort.Column, sort.Descending }
            .Should().BeEquivalentTo(new { columnChanged = true, Column = (FilingSortColumn?)COLUMN_B, Descending = false });
    }

    [Fact]
    public void Cycle_ThreeClicksOnSameColumn_ReturnsToUnsorted()
    {
        var sort = new SortState<FilingSortColumn>();

        sort.Cycle(COLUMN_A);
        sort.Cycle(COLUMN_A);
        sort.Cycle(COLUMN_A);

        sort.Column.Should().BeNull();
    }

    [Fact]
    public void Cycle_DirectionChange_RaisesDescendingNotification()
    {
        var sort = new SortState<FilingSortColumn>(COLUMN_A, descending: false);
        var raised = new List<string?>();
        sort.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        sort.Cycle(COLUMN_A);

        raised.Should().Equal(nameof(SortState<FilingSortColumn>.Descending));
    }
}
