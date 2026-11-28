using FluentAssertions;
using Rentier.Application.ReleaseNotes;
using Rentier.Infrastructure.ReleaseNotes;
using Xunit;

namespace Rentier.Infrastructure.Tests.ReleaseNotes;

public class EmbeddedReleaseNotesSourceTests
{
    [Fact]
    public async Task ReadAsync_BuildWithoutReleaseNotes_ReturnsNull()
    {
        // Only the release workflow passes -p:RentierReleaseNotesPath; this build does not.
        var markdown = await new EmbeddedReleaseNotesSource().ReadAsync(TestContext.Current.CancellationToken);

        markdown.Should().BeNull();
    }

    [Fact]
    public async Task ReadAsync_EmbeddedDocument_ReturnsItsMarkdown()
    {
        var source = new EmbeddedReleaseNotesSource(typeof(EmbeddedReleaseNotesSourceTests).Assembly, "Rentier.Tests.ReleaseNotes.md");

        var markdown = await source.ReadAsync(TestContext.Current.CancellationToken);

        var entry = ReleaseNotesDocument.Parse(markdown).Should().ContainSingle().Subject;
        entry.DisplayVersion.Should().Be("v1.4.0");
        entry.Serbian.Should().Be("- Četiri");
    }

    [Fact]
    public async Task ReadAsync_MissingResource_ReturnsNull()
    {
        var source = new EmbeddedReleaseNotesSource(typeof(EmbeddedReleaseNotesSource).Assembly, "No.Such.Resource");

        (await source.ReadAsync(TestContext.Current.CancellationToken)).Should().BeNull();
    }
}
