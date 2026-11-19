using FluentAssertions;
using Rentier.Desktop.Logging;
using Xunit;

namespace Rentier.UnitTests.Desktop.Logging;

public class GlobalExceptionLoggingTests
{
    [Fact]
    public void MarkLogged_FirstHook_ReturnsTrue()
    {
        GlobalExceptionLogging.MarkLogged(new InvalidOperationException()).Should().BeTrue();
    }

    [Fact]
    public void MarkLogged_SameExceptionReachesLaterHook_ReturnsFalse()
    {
        var crash = new InvalidOperationException();
        GlobalExceptionLogging.MarkLogged(crash);

        GlobalExceptionLogging.MarkLogged(crash).Should().BeFalse();
    }

    [Fact]
    public void MarkLogged_DifferentException_ReturnsTrue()
    {
        GlobalExceptionLogging.MarkLogged(new InvalidOperationException());

        GlobalExceptionLogging.MarkLogged(new InvalidOperationException()).Should().BeTrue();
    }

    [Fact]
    public void MarkLogged_NullException_ReturnsTrue()
    {
        GlobalExceptionLogging.MarkLogged(null).Should().BeTrue();
    }
}
