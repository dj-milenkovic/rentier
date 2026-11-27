using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Rentier.Application.Common;
using Rentier.Application.Handlers;
using Rentier.Application.Interfaces;
using Rentier.Application.Queries;
using Xunit;

namespace Rentier.UnitTests.Application.ReleaseNotes;

public class GetInstalledReleaseNotesQueryHandlerTests
{
    private const string DOCUMENT = "## v1.4.0\n\n### EN\n- New\n\n### SR\n- Novo\n\n## v1.3.1\n\n### EN\n- Old\n";

    private static GetInstalledReleaseNotesQueryHandler Build(IInstalledReleaseNotesSource source) =>
        new(source, NullLogger<GetInstalledReleaseNotesQueryHandler>.Instance);

    private static IInstalledReleaseNotesSource Source(string? markdown)
    {
        var source = Substitute.For<IInstalledReleaseNotesSource>();
        source.ReadAsync(Arg.Any<CancellationToken>()).Returns(markdown);
        return source;
    }

    [Fact]
    public async Task HandleAsync_Serbian_ReturnsSerbianNotesNewestFirst()
    {
        var result = await Build(Source(DOCUMENT)).HandleAsync(
            new GetInstalledReleaseNotesQuery("sr-Latn"), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Select(n => (n.Version, n.Markdown)).Should().Equal(
            ("v1.4.0", "- Novo"),
            ("v1.3.1", "- Old"));
    }

    [Fact]
    public async Task HandleAsync_English_ReturnsEnglishNotes()
    {
        var result = await Build(Source(DOCUMENT)).HandleAsync(
            new GetInstalledReleaseNotesQuery("en"), TestContext.Current.CancellationToken);

        result.Value[0].Markdown.Should().Be("- New");
    }

    [Fact]
    public async Task HandleAsync_NoDocument_ReturnsEmptySuccess()
    {
        var result = await Build(Source(null)).HandleAsync(
            new GetInstalledReleaseNotesQuery("en"), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_SourceThrows_ReturnsInfrastructureError()
    {
        var source = Substitute.For<IInstalledReleaseNotesSource>();
        source.ReadAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new IOException("broken"));

        var result = await Build(source).HandleAsync(
            new GetInstalledReleaseNotesQuery("en"), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(ErrorCodes.INFRASTRUCTURE_ERROR);
    }
}
