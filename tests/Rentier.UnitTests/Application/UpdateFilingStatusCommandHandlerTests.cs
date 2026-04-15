using FluentAssertions;
using NSubstitute;
using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.Handlers;
using Rentier.Application.Repositories;
using Rentier.Domain.Entities;
using Rentier.Domain.Enums;
using Xunit;

namespace Rentier.UnitTests;

public class UpdateFilingStatusCommandHandlerTests
{
    private readonly IFilingRepository _repo = Substitute.For<IFilingRepository>();
    private readonly UpdateFilingStatusCommandHandler _sut;

    public UpdateFilingStatusCommandHandlerTests()
    {
        _sut = new UpdateFilingStatusCommandHandler(_repo);
    }

    private static Filing MakeInitFiling()
    {
        return Filing.CreateFromIncome(
            Guid.NewGuid(), IncomeType.Dividend, "ACME",
            new DateOnly(2024, 3, 1), 1000m, 150m, 150m, 0m,
            new DateOnly(2024, 4, 30));
    }

    [Fact]
    public async Task HandleAsync_ValidInitToFiled_UpdatesStatusAndReturnsSuccess()
    {
        var filing = MakeInitFiling();
        _repo.GetByIdAsync(filing.Id, Arg.Any<CancellationToken>()).Returns(filing);

        var result = await _sut.HandleAsync(new UpdateFilingStatusCommand(filing.Id, FilingStatus.Filed));

        result.IsSuccess.Should().BeTrue();
        filing.Status.Should().Be(FilingStatus.Filed);
        await _repo.Received(1).UpdateAsync(filing, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ValidFiledToPaid_UpdatesStatusAndReturnsSuccess()
    {
        var filing = MakeInitFiling();
        filing.AdvanceStatus(FilingStatus.Filed);
        _repo.GetByIdAsync(filing.Id, Arg.Any<CancellationToken>()).Returns(filing);

        var result = await _sut.HandleAsync(new UpdateFilingStatusCommand(filing.Id, FilingStatus.Paid));

        result.IsSuccess.Should().BeTrue();
        filing.Status.Should().Be(FilingStatus.Paid);
        await _repo.Received(1).UpdateAsync(filing, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_InvalidTransition_ReturnsFailureWithoutPersisting()
    {
        var filing = MakeInitFiling();
        filing.AdvanceStatus(FilingStatus.Filed);
        filing.AdvanceStatus(FilingStatus.Paid);
        _repo.GetByIdAsync(filing.Id, Arg.Any<CancellationToken>()).Returns(filing);

        // Paid -> Init is invalid
        var result = await _sut.HandleAsync(new UpdateFilingStatusCommand(filing.Id, FilingStatus.Init));

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("DOMAIN_ERROR");
        await _repo.DidNotReceive().UpdateAsync(Arg.Any<Filing>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_InitToPaidTransition_ReturnsFailureWithoutPersisting()
    {
        // SC-005: Init -> Paid is an explicit skip of the state machine
        var filing = MakeInitFiling();
        _repo.GetByIdAsync(filing.Id, Arg.Any<CancellationToken>()).Returns(filing);

        var result = await _sut.HandleAsync(new UpdateFilingStatusCommand(filing.Id, FilingStatus.Paid));

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("DOMAIN_ERROR");
        await _repo.DidNotReceive().UpdateAsync(Arg.Any<Filing>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_FiledToInitTransition_ReturnsFailureWithoutPersisting()
    {
        // SC-005: backward transitions must be rejected
        var filing = MakeInitFiling();
        filing.AdvanceStatus(FilingStatus.Filed);
        _repo.GetByIdAsync(filing.Id, Arg.Any<CancellationToken>()).Returns(filing);

        var result = await _sut.HandleAsync(new UpdateFilingStatusCommand(filing.Id, FilingStatus.Init));

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("DOMAIN_ERROR");
        await _repo.DidNotReceive().UpdateAsync(Arg.Any<Filing>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_FilingNotFound_ReturnsFailure()
    {
        var id = Guid.NewGuid();
        _repo.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns((Filing?)null);

        var result = await _sut.HandleAsync(new UpdateFilingStatusCommand(id, FilingStatus.Filed));

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("NOT_FOUND");
        await _repo.DidNotReceive().UpdateAsync(Arg.Any<Filing>(), Arg.Any<CancellationToken>());
    }
}
