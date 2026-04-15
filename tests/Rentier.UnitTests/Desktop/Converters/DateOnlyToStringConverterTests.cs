using System.Globalization;
using Avalonia.Data;
using FluentAssertions;
using Rentier.Desktop.Converters;
using Xunit;

namespace Rentier.UnitTests;

public class DateOnlyToStringConverterTests
{
    private readonly DateOnlyToStringConverter _converter = DateOnlyToStringConverter.Instance;

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
        _converter.ConvertBack("2025-03-15", typeof(DateOnly), null, CultureInfo.InvariantCulture)
            .Should().Be(new DateOnly(2025, 3, 15));
    }

    [Fact]
    public void ConvertBack_InvalidString_ReturnsBindingNotificationWithError()
    {
        _converter.ConvertBack("abc", typeof(DateOnly), null, CultureInfo.InvariantCulture)
            .Should().BeOfType<BindingNotification>()
            .Which.ErrorType.Should().Be(BindingErrorType.DataValidationError);
    }

    [Fact]
    public void ConvertBack_Null_ReturnsBindingNotificationWithError()
    {
        _converter.ConvertBack(null, typeof(DateOnly), null, CultureInfo.InvariantCulture)
            .Should().BeOfType<BindingNotification>()
            .Which.ErrorType.Should().Be(BindingErrorType.DataValidationError);
    }
}
