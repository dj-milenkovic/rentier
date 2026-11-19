using FluentAssertions;
using Rentier.Desktop.Logging;
using Xunit;

namespace Rentier.UnitTests.Desktop.Logging;

public class PiiRedactorTests
{
    private const string HOME = @"C:\Users\alice";

    [Fact]
    public void Redact_HomeFolderInPath_ReplacesWithTilde()
    {
        var result = PiiRedactor.Redact(@"Could not open C:\Users\alice\AppData\Local\Rentier\rentier.db", HOME);

        result.Should().Be(@"Could not open ~\AppData\Local\Rentier\rentier.db");
    }

    [Theory]
    [InlineData(@"c:\users\ALICE\x.csv", @"~\x.csv")]
    [InlineData("C:/Users/alice/x.csv", "~/x.csv")]
    public void Redact_HomeFolderDifferentCaseOrSlashes_ReplacesWithTilde(string input, string expected)
    {
        PiiRedactor.Redact(input, HOME).Should().Be(expected);
    }

    [Theory]
    [InlineData(@"open D:\Users\bob\Documents\a.csv", @"open D:\Users\<user>\Documents\a.csv")]
    [InlineData("open /home/bob/docs/a.csv", "open /home/<user>/docs/a.csv")]
    [InlineData("open /Users/bob/docs/a.csv", "open /Users/<user>/docs/a.csv")]
    public void Redact_OtherUsersProfilePath_ReplacesUserName(string input, string expected)
    {
        PiiRedactor.Redact(input, HOME).Should().Be(expected);
    }

    [Fact]
    public void Redact_EmailAddress_ReplacesWithPlaceholder()
    {
        PiiRedactor.Redact("login failed for john.doe+tax@example.co.rs on host", HOME)
            .Should().Be("login failed for [email] on host");
    }

    [Theory]
    [InlineData("account U1234567 rejected", "account [account] rejected")]
    [InlineData("account u12345678 rejected", "account [account] rejected")]
    [InlineData("paper account DU1234567 rejected", "paper account [account] rejected")]
    [InlineData("paper account DF1234567 rejected", "paper account [account] rejected")]
    [InlineData("imported U1234567_20250101_20251231.csv", "imported [account]_20250101_20251231.csv")]
    [InlineData("imported stmt_DU1234567.csv", "imported stmt_[account].csv")]
    public void Redact_BrokerAccountId_ReplacesWithPlaceholder(string input, string expected)
    {
        PiiRedactor.Redact(input, HOME).Should().Be(expected);
    }

    [Theory]
    [InlineData("JMBG 0101990710123 invalid", "JMBG [number] invalid")]
    [InlineData("IBAN 12345678901234", "IBAN [number]")]
    [InlineData("value 12345678", "value [number]")]
    public void Redact_LongDigitRun_ReplacesWithPlaceholder(string input, string expected)
    {
        PiiRedactor.Redact(input, HOME).Should().Be(expected);
    }

    [Theory]
    [InlineData("IBAN RS35260005601001611379 invalid", "IBAN [number] invalid")]
    [InlineData("IBAN RS35 2600 0560 1001 6113 79 invalid", "IBAN [number] invalid")]
    [InlineData("IBAN DE89370400440532013000.", "IBAN [number].")]
    public void Redact_Iban_ReplacesWithPlaceholder(string input, string expected)
    {
        PiiRedactor.Redact(input, HOME).Should().Be(expected);
    }

    [Theory]
    [InlineData("account 160-0000000123456-78 invalid", "account [number] invalid")]
    [InlineData("account 265-1234-56 invalid", "account [number] invalid")]
    public void Redact_SerbianDashedBankAccount_ReplacesWithPlaceholder(string input, string expected)
    {
        PiiRedactor.Redact(input, HOME).Should().Be(expected);
    }

    [Theory]
    [InlineData("isin US0378331005 parsed")]
    [InlineData("deadline 2026-12-21 computed")]
    [InlineData("2026-11-19 12:00:00.123 +01:00 [INF] started")]
    [InlineData("report 3f2504e0-4f89-11d3-9a0c-0305e82c3301 imported")]
    [InlineData("report 12345678-4f89-11d3-9a0c-030512345678 imported")]
    [InlineData("rentier-20261119.log opened")]
    [InlineData("fetched 3 rates for EUR on 2026-11-19, rate 117.4521")]
    [InlineData("1234567 rows")]
    public void Redact_NonSensitiveText_IsUnchanged(string input)
    {
        PiiRedactor.Redact(input, HOME).Should().Be(input);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Redact_NullOrEmpty_ReturnsEmpty(string? input)
    {
        PiiRedactor.Redact(input, HOME).Should().BeEmpty();
    }

    [Fact]
    public void Redact_NoHomeFolderSupplied_UsesCurrentUsersProfile()
    {
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        Assert.SkipWhen(string.IsNullOrWhiteSpace(profile) || profile.Length <= 3, "No profile folder on this machine.");

        var result = PiiRedactor.Redact($"opening {profile}{Path.DirectorySeparatorChar}x.txt");

        result.Should().NotContain(profile);
        result.Should().StartWith("opening ~");
    }
}
