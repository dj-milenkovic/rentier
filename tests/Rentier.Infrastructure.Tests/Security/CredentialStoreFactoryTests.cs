using System.Runtime.InteropServices;
using FluentAssertions;
using Rentier.Application.Common;
using Rentier.Infrastructure.Security;
using Xunit;

namespace Rentier.Infrastructure.Tests.Security;

[Trait("Category", "Integration")]
public class CredentialStoreFactoryTests
{
    [Fact]
    public async Task CreateAsync_OnWindows_ReturnsWindowsCredentialStore()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

        var result = await CredentialStoreFactory.CreateAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.Store.Should().BeOfType<WindowsCredentialStore>();
        result.Value.Info.ProviderName.Should().Be("Windows Credential Manager");
        result.Value.Info.Platform.Should().Be("Windows");
    }

    [Fact]
    public async Task CreateAsync_OnMacOs_ReturnsMacOsCredentialStore()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return;

        var result = await CredentialStoreFactory.CreateAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.Store.Should().BeOfType<MacOsCredentialStore>();
        result.Value.Info.ProviderName.Should().Be("macOS Keychain");
        result.Value.Info.Platform.Should().Be("macOS");
    }

    [Fact]
    public async Task CreateAsync_OnLinuxWithDaemon_ReturnsLinuxCredentialStore()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return;

        var result = await CredentialStoreFactory.CreateAsync();

        // On Linux with daemon: success with LinuxCredentialStore
        // On Linux without daemon: failure with PROVIDER_UNAVAILABLE
        if (result.IsSuccess)
        {
            result.Value.Store.Should().BeOfType<LinuxCredentialStore>();
            result.Value.Info.ProviderName.Should().Be("Linux Secret Service");
            result.Value.Info.Platform.Should().Be("Linux");
        }
        else
        {
            result.Error.Code.Should().Be("PROVIDER_UNAVAILABLE");
            result.Error.Message.Should().NotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public async Task CreateAsync_OnLinuxWithoutDaemon_ReturnsProviderUnavailable()
    {
        // This test verifies error contract — can only run meaningfully on headless Linux
        // On other platforms, the test is skipped via the platform guard
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return;

        var result = await CredentialStoreFactory.CreateAsync();
        if (result.IsSuccess) return; // daemon is running, can't test unavailable path

        result.Error.Code.Should().Be("PROVIDER_UNAVAILABLE");
        result.Error.Message.Should().Contain("Secret Service");
    }

    [Fact]
    public void ProviderInfo_ToString_ReturnsNameAndPlatform()
    {
        var info = new ProviderInfo("Windows Credential Manager", "Windows");

        info.ToString().Should().Be("Windows Credential Manager (Windows)");
    }

    [Fact]
    public async Task CreateAsync_SuccessResult_ProviderInfoToStringIsCorrect()
    {
        var result = await CredentialStoreFactory.CreateAsync();

        if (!result.IsSuccess) return; // platform unsupported / daemon unavailable

        result.Value.Info.ToString().Should()
            .MatchRegex(@"^.+ \(.+\)$"); // Format: "Provider Name (Platform)"
    }

    [Fact]
    public async Task CreateAsync_UnsupportedPlatform_ErrorIncludesOsDescription()
    {
        // This test can only fire on a truly unsupported platform.
        // On Windows/macOS/Linux it will return success or PROVIDER_UNAVAILABLE.
        // We test the UnsupportedPlatform error factory directly instead.
        var error = Error.UnsupportedPlatform("FreeBSD 14.0");

        error.Code.Should().Be("UNSUPPORTED_PLATFORM");
        error.Message.Should().Contain("FreeBSD 14.0");
    }
}
