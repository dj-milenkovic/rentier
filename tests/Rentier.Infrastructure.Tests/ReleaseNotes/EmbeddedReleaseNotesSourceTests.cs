using FluentAssertions;
using Rentier.Application.ReleaseNotes;
using Rentier.Infrastructure.ReleaseNotes;
using Xunit;

namespace Rentier.Infrastructure.Tests.ReleaseNotes;

public class EmbeddedReleaseNotesSourceTests
{
    [Fact]
    public async Task ReadAsync_DefaultBuild_ReturnsEmbeddedHistory()
    {
        var markdown = await new EmbeddedReleaseNotesSource().ReadAsync(TestContext.Current.CancellationToken);

        markdown.Should().NotBeNullOrWhiteSpace();
        ReleaseNotesDocument.Parse(markdown).Should().NotBeEmpty(
            "local builds embed release-notes/history.md (RentierReleaseNotesPath)");
    }

    [Fact]
    public async Task ReadAsync_MissingResource_ReturnsNull()
    {
        var source = new EmbeddedReleaseNotesSource(typeof(EmbeddedReleaseNotesSource).Assembly, "No.Such.Resource");

        (await source.ReadAsync(TestContext.Current.CancellationToken)).Should().BeNull();
    }
}
