using System.Globalization;
using FluentAssertions;
using Rentier.Desktop.Converters;
using Xunit;

namespace Rentier.UnitTests;

public class NullableDateOnlyConverterTests
{
    private readonly NullableDateOnlyConverter _converter = NullableDateOnlyConverter.Instance;

    [Fact]
    public void Convert_ValidDateOnly_ReturnsIsoString()
    {
        _converter.Convert(new DateOnly(2025, 1, 1), typeof(string), null, CultureInfo.InvariantCulture)
            .Should().Be("2025-01-01");
    }

    [Fact]
    public void Convert_Null_ReturnsEmptyString()
    {
        _converter.Convert(null, typeof(string), null, CultureInfo.InvariantCulture)
            .Should().Be(string.Empty);
    }

    [Fact]
    public void ConvertBack_ValidIsoString_ReturnsDateOnly()
    {
        _converter.ConvertBack("2025-03-15", typeof(DateOnly?), null, CultureInfo.InvariantCulture)
            .Should().Be(new DateOnly(2025, 3, 15));
    }

    [Fact]
    public void ConvertBack_InvalidString_ReturnsNull()
    {
        _converter.ConvertBack("abc", typeof(DateOnly?), null, CultureInfo.InvariantCulture)
            .Should().BeNull();
    }

    [Fact]
    public void ConvertBack_Null_ReturnsNull()
    {
        _converter.ConvertBack(null, typeof(DateOnly?), null, CultureInfo.InvariantCulture)
            .Should().BeNull();
    }

    [Fact]
    public void ConvertBack_IsoStringUnderNonIsoCurrentCulture_ReturnsDateOnly()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            // A culture whose default date pattern is not yyyy-MM-dd must not affect parsing
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            _converter.ConvertBack("2025-03-15", typeof(DateOnly?), null, CultureInfo.CurrentCulture)
                .Should().Be(new DateOnly(2025, 3, 15));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}
