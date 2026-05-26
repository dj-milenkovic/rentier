using FluentAssertions;
using Rentier.Application.DTOs;
using Xunit;

namespace Rentier.UnitTests.Application;

public class UpdateCheckResultTests
{
    [Fact]
    public void Constructor_WhenUpdateAvailable_SetsProperties()
    {
        var result = new UpdateCheckResult(IsUpdateAvailable: true, TargetVersion: "1.2.3");

        result.IsUpdateAvailable.Should().BeTrue();
        result.TargetVersion.Should().Be("1.2.3");
    }

    [Fact]
    public void Constructor_WhenNoUpdateAvailable_TargetVersionIsNull()
    {
        var result = new UpdateCheckResult(IsUpdateAvailable: false, TargetVersion: null);

        result.IsUpdateAvailable.Should().BeFalse();
        result.TargetVersion.Should().BeNull();
    }

    [Fact]
    public void Constructor_WhenNotAvailable_IsUpdateAvailableIsFalse()
    {
        var result = new UpdateCheckResult(false, null);

        result.IsUpdateAvailable.Should().BeFalse();
    }

    [Fact]
    public void Equality_TwoIdenticalResults_AreEqual()
    {
        var r1 = new UpdateCheckResult(true, "2.0.0");
        var r2 = new UpdateCheckResult(true, "2.0.0");

        r1.Should().Be(r2);
    }

    [Fact]
    public void Equality_DifferentVersions_AreNotEqual()
    {
        var r1 = new UpdateCheckResult(true, "1.0.0");
        var r2 = new UpdateCheckResult(true, "2.0.0");

        r1.Should().NotBe(r2);
    }

    [Fact]
    public void Equality_AvailableVsNotAvailable_AreNotEqual()
    {
        var r1 = new UpdateCheckResult(true, "1.0.0");
        var r2 = new UpdateCheckResult(false, null);

        r1.Should().NotBe(r2);
    }

    [Fact]
    public void ToString_IncludesProperties()
    {
        var result = new UpdateCheckResult(true, "3.0.0");

        result.ToString().Should().Contain("3.0.0");
    }
}
