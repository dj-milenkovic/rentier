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

public class UpdatePaymentReferenceCommandHandlerTests
{
    private readonly IFilingRepository _repo = Substitute.For<IFilingRepository>();
    private readonly UpdatePaymentReferenceCommandHandler _sut;

    public UpdatePaymentReferenceCommandHandlerTests()
    {
        _sut = new UpdatePaymentReferenceCommandHandler(_repo);
    }

    private static Filing MakeFiling()
    {
        var f = Filing.CreateFromIncome(
            Guid.NewGuid(), IncomeType.Dividend, "ACME",
            new DateOnly(2024, 3, 1), 1000m, 150m, 150m, 0m,
            new DateOnly(2024, 4, 30));
        f.AdvanceStatus(FilingStatus.Filed);
        return f;
    }

    [Fact]
    public async Task HandleAsync_WithValidReference_PersistsValueAndReturnsSuccess()
    {
        var filing = MakeFiling();
        _repo.GetByIdAsync(filing.Id, Arg.Any<CancellationToken>()).Returns(filing);

        var result = await _sut.HandleAsync(new UpdatePaymentReferenceCommand(filing.Id, "REF-2024-001"));

        result.IsSuccess.Should().BeTrue();
        filing.PaymentReference.Should().Be("REF-2024-001");
        await _repo.Received(1).UpdateAsync(filing, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithNullReference_ClearsValueAndReturnsSuccess()
    {
        var filing = MakeFiling();
        filing.SetPaymentReference("EXISTING");
        _repo.GetByIdAsync(filing.Id, Arg.Any<CancellationToken>()).Returns(filing);

        var result = await _sut.HandleAsync(new UpdatePaymentReferenceCommand(filing.Id, null));

        result.IsSuccess.Should().BeTrue();
        filing.PaymentReference.Should().BeNull();
        await _repo.Received(1).UpdateAsync(filing, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithOver200CharReference_ReturnsFailureWithoutPersisting()
    {
        var filing = MakeFiling();
        _repo.GetByIdAsync(filing.Id, Arg.Any<CancellationToken>()).Returns(filing);
        var tooLong = new string('x', 201);

        var result = await _sut.HandleAsync(new UpdatePaymentReferenceCommand(filing.Id, tooLong));

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("DOMAIN_ERROR");
        await _repo.DidNotReceive().UpdateAsync(Arg.Any<Filing>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_FilingNotFound_ReturnsFailure()
    {
        var id = Guid.NewGuid();
        _repo.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns((Filing?)null);

        var result = await _sut.HandleAsync(new UpdatePaymentReferenceCommand(id, "REF-001"));

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("NOT_FOUND");
        await _repo.DidNotReceive().UpdateAsync(Arg.Any<Filing>(), Arg.Any<CancellationToken>());
    }
}
