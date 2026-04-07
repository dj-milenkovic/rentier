using FluentAssertions;
using NSubstitute;
using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Application.Enums;
using Rentier.Application.Handlers;
using Rentier.Application.Queries;
using Rentier.Application.Repositories;
using Rentier.Domain.Entities;
using Rentier.Domain.Enums;
using Xunit;

namespace Rentier.Application.Tests;

public class GetFilingsQueryHandlerTests
{
    private readonly IFilingRepository _repo = Substitute.For<IFilingRepository>();
    private readonly GetFilingsQueryHandler _sut;

    public GetFilingsQueryHandlerTests()
    {
        _sut = new GetFilingsQueryHandler(_repo);
    }

    private static Filing MakeFiling(DateOnly? deadline = null, FilingStatus status = FilingStatus.Init)
    {
        var f = Filing.CreateFromIncome(
            Guid.NewGuid(), IncomeType.Dividend, "ACME Corp",
            new DateOnly(2024, 3, 1), 1000m, 150m, 150m, 0m,
            deadline ?? new DateOnly(2024, 4, 30));
        if (status == FilingStatus.Filed) f.AdvanceStatus(FilingStatus.Filed);
        if (status == FilingStatus.Paid) { f.AdvanceStatus(FilingStatus.Filed); f.AdvanceStatus(FilingStatus.Paid); }
        return f;
    }

    [Fact]
    public async Task HandleAsync_WithUnpaidFilter_PassesUnpaidToRepository()
    {
        _repo.GetPagedAsync(Arg.Any<FilingFilterMode>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns((Array.Empty<Filing>().ToList().AsReadOnly() as IReadOnlyList<Filing>, 0));

        await _sut.HandleAsync(new GetFilingsQuery(FilingFilterMode.Unpaid, 1, 20));

        await _repo.Received(1).GetPagedAsync(
            FilingFilterMode.Unpaid, Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithAllFilter_PassesAllToRepository()
    {
        _repo.GetPagedAsync(Arg.Any<FilingFilterMode>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns((Array.Empty<Filing>().ToList().AsReadOnly() as IReadOnlyList<Filing>, 0));

        await _sut.HandleAsync(new GetFilingsQuery(FilingFilterMode.All, 1, 20));

        await _repo.Received(1).GetPagedAsync(
            FilingFilterMode.All, Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_MapsFilingTaxPayableRsdToTaxPayable()
    {
        var filing = MakeFiling();
        _repo.GetPagedAsync(Arg.Any<FilingFilterMode>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns((new List<Filing> { filing }.AsReadOnly() as IReadOnlyList<Filing>, 1));

        var result = await _sut.HandleAsync(new GetFilingsQuery(FilingFilterMode.All, 1, 20));

        result.IsSuccess.Should().BeTrue();
        result.Value.Rows[0].TaxPayable.Should().Be(filing.TaxPayableRsd);
    }

    [Fact]
    public async Task HandleAsync_ComputesCorrectTotalPages()
    {
        _repo.GetPagedAsync(Arg.Any<FilingFilterMode>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns((Array.Empty<Filing>().ToList().AsReadOnly() as IReadOnlyList<Filing>, 45));

        var result = await _sut.HandleAsync(new GetFilingsQuery(FilingFilterMode.All, 1, 20));

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalPages.Should().Be(3); // ceil(45/20) = 3
    }

    [Fact]
    public async Task HandleAsync_WhenNoResults_ReturnsTotalPagesOfOne()
    {
        _repo.GetPagedAsync(Arg.Any<FilingFilterMode>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns((Array.Empty<Filing>().ToList().AsReadOnly() as IReadOnlyList<Filing>, 0));

        var result = await _sut.HandleAsync(new GetFilingsQuery(FilingFilterMode.All, 1, 20));

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalPages.Should().Be(1);
    }

    [Fact]
    public async Task HandleAsync_WhenPageLessThan1_ReturnsFailure()
    {
        var result = await _sut.HandleAsync(new GetFilingsQuery(FilingFilterMode.All, 0, 20));

        result.IsSuccess.Should().BeFalse();
        result.Error.Message.Should().Contain("1");
    }

    [Fact]
    public async Task HandleAsync_WhenPageSizeOutOfRange_ReturnsFailure()
    {
        var result = await _sut.HandleAsync(new GetFilingsQuery(FilingFilterMode.All, 1, 0));

        result.IsSuccess.Should().BeFalse();
        result.Error.Message.Should().NotBeEmpty();
    }

    [Fact]
    public async Task HandleAsync_PassesCorrectSkipToRepository()
    {
        _repo.GetPagedAsync(Arg.Any<FilingFilterMode>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns((Array.Empty<Filing>().ToList().AsReadOnly() as IReadOnlyList<Filing>, 0));

        await _sut.HandleAsync(new GetFilingsQuery(FilingFilterMode.All, 3, 20));

        // page 3, page size 20 => skip = (3-1)*20 = 40
        await _repo.Received(1).GetPagedAsync(
            Arg.Any<FilingFilterMode>(), 40, 20, Arg.Any<CancellationToken>());
    }
}
