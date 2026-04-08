using FluentAssertions;
using NSubstitute;
using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Application.Handlers;
using Rentier.Application.Queries;
using Rentier.Application.Repositories;
using Rentier.Domain.Entities;
using Rentier.Domain.Enums;
using Xunit;

namespace Rentier.Application.Tests;

public sealed class GetImportersQueryHandlerTests
{
    private readonly IImporterRepository _repo = Substitute.For<IImporterRepository>();
    private readonly GetImportersQueryHandler _sut;

    public GetImportersQueryHandlerTests()
    {
        _sut = new GetImportersQueryHandler(_repo);
    }

    [Fact]
    public async Task HandleAsync_NoImporters_ReturnsEmptyList()
    {
        _repo.GetAllAsync(default).ReturnsForAnyArgs(new List<Importer>());

        var result = await _sut.HandleAsync(new GetImportersQuery());

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_WithImporters_ReturnsMappedDtos()
    {
        var profileId = Guid.NewGuid();
        var mailboxId = Guid.NewGuid();

        var a = Importer.Create("Alpha", ReportType.IbkrCsv);
        a.UpdateDetails("Alpha", ReportType.IbkrCsv, profileId, null, "f1", "s1", "r1", "p1");

        var b = Importer.Create("Beta", ReportType.IbkrCsv);
        b.UpdateDetails("Beta", ReportType.IbkrCsv, null, mailboxId, "", "", "", "");

        _repo.GetAllAsync(default).ReturnsForAnyArgs(new List<Importer> { a, b });

        var result = await _sut.HandleAsync(new GetImportersQuery());

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);

        var dtoA = result.Value.First(d => d.DisplayName == "Alpha");
        dtoA.ReportType.Should().Be(ReportType.IbkrCsv);
        dtoA.TaxpayerProfileId.Should().Be(profileId);
        dtoA.MailboxId.Should().BeNull();
        dtoA.FromFilter.Should().Be("f1");

        var dtoB = result.Value.First(d => d.DisplayName == "Beta");
        dtoB.TaxpayerProfileId.Should().BeNull();
        dtoB.MailboxId.Should().Be(mailboxId);
    }
}
