using FluentAssertions;
using NSubstitute;
using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.Handlers;
using Rentier.Application.Repositories;
using Rentier.Domain.Enums;
using Xunit;

namespace Rentier.UnitTests;

public sealed class AddImporterCommandHandlerTests
{
    private readonly IImporterRepository _repo = Substitute.For<IImporterRepository>();
    private readonly AddImporterCommandHandler _sut;

    public AddImporterCommandHandlerTests()
    {
        _sut = new AddImporterCommandHandler(_repo);
    }

    [Fact]
    public async Task HandleAsync_ValidCommand_AddsImporterAndReturnsGuid()
    {
        var cmd = new AddImporterCommand("My Importer", ReportType.IbkrCsv, null, null, "", "", "", "");

        var result = await _sut.HandleAsync(cmd);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
        await _repo.Received(1).AddAsync(Arg.Any<Rentier.Domain.Entities.Importer>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_InvalidRegex_ReturnsFailure()
    {
        var cmd = new AddImporterCommand("My Importer", ReportType.IbkrCsv, null, null, "", "", "[invalid", "");

        var result = await _sut.HandleAsync(cmd);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("IMPORTER_VALIDATION_INVALID_REGEX");
        await _repo.DidNotReceive().AddAsync(Arg.Any<Rentier.Domain.Entities.Importer>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_EmptyRegex_Succeeds()
    {
        var cmd = new AddImporterCommand("My Importer", ReportType.IbkrCsv, null, null, "", "", "", "");

        var result = await _sut.HandleAsync(cmd);

        result.IsSuccess.Should().BeTrue();
        await _repo.Received(1).AddAsync(Arg.Any<Rentier.Domain.Entities.Importer>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_InvalidDisplayName_ReturnsDomainError()
    {
        var cmd = new AddImporterCommand("", ReportType.IbkrCsv, null, null, "", "", "", "");

        var result = await _sut.HandleAsync(cmd);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("DOMAIN_ERROR");
        await _repo.DidNotReceive().AddAsync(Arg.Any<Rentier.Domain.Entities.Importer>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_NullForeignKeys_Succeeds()
    {
        var cmd = new AddImporterCommand("My Importer", ReportType.IbkrCsv, null, null, "", "", "", "");

        var result = await _sut.HandleAsync(cmd);

        result.IsSuccess.Should().BeTrue();
    }
}
