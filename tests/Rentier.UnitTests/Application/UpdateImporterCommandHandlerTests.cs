using FluentAssertions;
using NSubstitute;
using Rentier.Application.Commands;
using Rentier.Application.Handlers;
using Rentier.Application.Repositories;
using Rentier.Domain.Entities;
using Rentier.Domain.Enums;
using Xunit;

namespace Rentier.UnitTests.Application;

public sealed class UpdateImporterCommandHandlerTests
{
    private readonly IImporterRepository _repo = Substitute.For<IImporterRepository>();
    private readonly UpdateImporterCommandHandler _sut;

    public UpdateImporterCommandHandlerTests()
    {
        _sut = new UpdateImporterCommandHandler(_repo);
    }

    [Fact]
    public async Task HandleAsync_ValidUpdate_UpdatesAndReturnsSuccess()
    {
        var existing = Importer.Create("Original", ReportType.IbkrCsv);
        _repo.GetByIdAsync(existing.Id, Arg.Any<CancellationToken>()).Returns(existing);

        var cmd = new UpdateImporterCommand(existing.Id, "Updated", ReportType.IbkrCsv, null, null, "", "", "", "");

        var result = await _sut.HandleAsync(cmd, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        await _repo.Received(1).UpdateAsync(existing, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_NotFound_ReturnsFailure()
    {
        var id = Guid.NewGuid();
        _repo.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns((Importer?)null);

        var cmd = new UpdateImporterCommand(id, "Updated", ReportType.IbkrCsv, null, null, "", "", "", "");

        var result = await _sut.HandleAsync(cmd, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("IMPORTER_NOT_FOUND");
        await _repo.DidNotReceive().UpdateAsync(Arg.Any<Importer>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_InvalidRegex_ReturnsFailure()
    {
        var existing = Importer.Create("Test", ReportType.IbkrCsv);
        _repo.GetByIdAsync(existing.Id, Arg.Any<CancellationToken>()).Returns(existing);

        var cmd = new UpdateImporterCommand(existing.Id, "Test", ReportType.IbkrCsv, null, null, "", "", "[invalid", "");

        var result = await _sut.HandleAsync(cmd, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("IMPORTER_VALIDATION_INVALID_REGEX");
        await _repo.DidNotReceive().UpdateAsync(Arg.Any<Importer>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_InvalidDisplayName_ReturnsDomainError()
    {
        var existing = Importer.Create("Test", ReportType.IbkrCsv);
        _repo.GetByIdAsync(existing.Id, Arg.Any<CancellationToken>()).Returns(existing);

        var cmd = new UpdateImporterCommand(existing.Id, "", ReportType.IbkrCsv, null, null, "", "", "", "");

        var result = await _sut.HandleAsync(cmd, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("DOMAIN_ERROR");
        await _repo.DidNotReceive().UpdateAsync(Arg.Any<Importer>(), Arg.Any<CancellationToken>());
    }
}
