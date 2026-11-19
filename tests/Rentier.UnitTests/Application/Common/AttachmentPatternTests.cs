using System.Text.RegularExpressions;
using FluentAssertions;
using Rentier.Application.Common;
using Xunit;

namespace Rentier.UnitTests.Application.Common;

public class AttachmentPatternTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Validate_EmptyPattern_IsValid(string? pattern)
    {
        AttachmentPattern.Validate(pattern).Should().BeNull();
    }

    [Fact]
    public void Validate_ValidPattern_IsValid()
    {
        AttachmentPattern.Validate(@"^U\d+_.*\.csv$").Should().BeNull();
    }

    [Fact]
    public void Validate_InvalidPattern_ReturnsInvalidRegexError()
    {
        AttachmentPattern.Validate("[unclosed").Should().BeEquivalentTo(
            new { Code = ErrorCodes.IMPORTER_VALIDATION_INVALID_REGEX });
    }

    [Fact]
    public void Create_AnyPattern_HasMatchTimeout()
    {
        AttachmentPattern.Create(".*").MatchTimeout.Should().Be(AttachmentPattern.MATCH_TIMEOUT);
    }

    [Fact]
    public void Create_AnyPattern_MatchesCaseInsensitively()
    {
        AttachmentPattern.Create(@"\.csv$").IsMatch("STATEMENT.CSV").Should().BeTrue();
    }

    [Fact]
    public void Create_CatastrophicBacktrackingPattern_TimesOutInsteadOfHanging()
    {
        var regex = AttachmentPattern.Create("^(a+)+$");
        var hostileFileName = new string('a', 64) + "!";

        var act = () => regex.IsMatch(hostileFileName);

        act.Should().Throw<RegexMatchTimeoutException>();
    }
}
