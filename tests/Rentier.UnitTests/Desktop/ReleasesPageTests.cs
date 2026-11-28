using FluentAssertions;
using Rentier.Desktop.Services;
using Xunit;

namespace Rentier.UnitTests.Desktop;

public class ReleasesPageTests
{
    [Fact]
    public void Uri_IsHttpsGitHubReleasesPage()
    {
        ReleasesPage.Uri.Should().NotBeNull();
        ReleasesPage.Uri!.Scheme.Should().Be(Uri.UriSchemeHttps);
        ReleasesPage.Uri.AbsolutePath.Should().EndWith("/releases");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a url")]
    [InlineData("http://example.com/releases")]
    [InlineData("file:///C:/releases")]
    public void Resolve_MissingOrUnsafeUrl_ReturnsNull(string? url)
    {
        ReleasesPage.Resolve(url).Should().BeNull();
    }
}
