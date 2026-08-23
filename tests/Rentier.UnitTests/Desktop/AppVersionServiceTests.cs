using FluentAssertions;
using Rentier.Desktop.Services;
using Xunit;

namespace Rentier.UnitTests.Desktop;

public class AppVersionServiceTests
{
    [Fact]
    public void DisplayVersion_WhenInformationalVersionIsRealSemver_ReturnsVPrefixedVersion()
    {
        var service = new AppVersionService("1.4.2");

        service.DisplayVersion.Should().Be("v1.4.2");
    }

    [Fact]
    public void DisplayVersion_WhenInformationalVersionIsSdkDefault_ReturnsDev()
    {
        var service = new AppVersionService("1.0.0");

        service.DisplayVersion.Should().Be("dev");
    }

    [Fact]
    public void DisplayVersion_WhenInformationalVersionIsNull_ReturnsDev()
    {
        var service = new AppVersionService(null);

        service.DisplayVersion.Should().Be("dev");
    }

    [Fact]
    public void DisplayVersion_WhenInformationalVersionIsEmpty_ReturnsDev()
    {
        var service = new AppVersionService(string.Empty);

        service.DisplayVersion.Should().Be("dev");
    }
}
