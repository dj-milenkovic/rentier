using FluentAssertions;
using Rentier.Desktop.Logging;
using Xunit;

namespace Rentier.UnitTests.Desktop.Logging
{
    public class SafeExceptionRendererTests
    {
        [Fact]
        public void Render_OrdinaryException_KeepsTypeAndMessage()
        {
            var result = SafeExceptionRenderer.Render(new InvalidOperationException("database is locked"));

            result.Should().Contain("System.InvalidOperationException").And.Contain("database is locked");
        }

        [Fact]
        public void Render_FormatException_SuppressesMessage()
        {
            var result = SafeExceptionRenderer.Render(new FormatException("The input string '1234.56' was not in a correct format."));

            result.Should().Contain("System.FormatException").And.Contain("[message suppressed]");
            result.Should().NotContain("1234.56");
        }

        [Fact]
        public void Render_CsvHelperExceptionType_SuppressesMessage()
        {
            var result = SafeExceptionRenderer.Render(new CsvHelper.Fake.FakeCsvException("Raw record: U1234567,AAPL,100.00"));

            result.Should().Contain("[message suppressed]");
            result.Should().NotContain("U1234567").And.NotContain("AAPL");
        }

        [Fact]
        public void Render_TypeDerivedFromCsvHelperException_SuppressesMessage()
        {
            var result = SafeExceptionRenderer.Render(new CsvHelper.Fake.DerivedCsvException("secret row"));

            result.Should().NotContain("secret row");
        }

        [Fact]
        public void Render_InnerException_RendersWholeChain()
        {
            var ex = new InvalidOperationException("outer failure", new FormatException("inner 99.99"));

            var result = SafeExceptionRenderer.Render(ex);

            result.Should().Contain("outer failure").And.Contain("System.FormatException");
            result.Should().NotContain("99.99");
        }

        [Fact]
        public void Render_AggregateException_RendersEveryChild()
        {
            var ex = new AggregateException(
                new InvalidOperationException("first problem"),
                new TimeoutException("second problem"));

            var result = SafeExceptionRenderer.Render(ex);

            result.Should().Contain("first problem").And.Contain("second problem");
        }

        [Fact]
        public void Render_ThrownException_IncludesStackTrace()
        {
            Exception caught;
            try
            {
                throw new InvalidOperationException("boom");
            }
            catch (InvalidOperationException ex)
            {
                caught = ex;
            }

            SafeExceptionRenderer.Render(caught).Should().Contain(nameof(Render_ThrownException_IncludesStackTrace));
        }

        [Fact]
        public void Render_DeeplyNestedExceptions_StopsAtDepthLimit()
        {
            Exception ex = new InvalidOperationException("level-0");
            for (var i = 1; i <= 20; i++)
                ex = new InvalidOperationException($"level-{i}", ex);

            var result = SafeExceptionRenderer.Render(ex);

            result.Should().Contain("level-20");
            result.Should().NotContain("level-0\r").And.NotContain("level-0\n");
        }
    }
}

namespace CsvHelper.Fake
{
    internal class FakeCsvException(string message) : Exception(message);

    internal sealed class DerivedCsvException(string message) : FakeCsvException(message);
}
