using FluentAssertions;
using Rentier.Domain.Entities;
using Rentier.Domain.Enums;
using Rentier.Infrastructure.Serialization;
using Xunit;

namespace Rentier.Infrastructure.Tests.Serialization;

[Trait("Category", "Integration")]
public class PpOpoXmlSerializerUnsupportedTypeTests
{
    private readonly PpOpoXmlSerializer _sut = new();

    private static TaxpayerProfile MakeProfile()
        => new TaxpayerProfile(Guid.NewGuid(), "1234567890123", "Test User", "Main St 1", "018");

    /// <summary>
    /// IncomeType enum can be cast to an unrecognised value via integer cast.
    /// The serializer must return a structured failure rather than throw.
    /// </summary>
    [Fact]
    public void Serialize_UnsupportedIncomeType_ReturnsFailureWithCorrectErrorCode()
    {
        var unsupportedType = (IncomeType)999;
        var filing = Filing.CreateFromIncome(
            Guid.NewGuid(), unsupportedType, "ACME Corp",
            new DateOnly(2025, 3, 15), 10000m, 0m, 1500m, 1500m,
            new DateOnly(2025, 4, 30));

        var result = _sut.Serialize(filing, MakeProfile(), string.Empty);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("UNSUPPORTED_INCOME_TYPE");
    }

    [Fact]
    public void Serialize_UnsupportedIncomeType_ErrorMessageContainsIncomeType()
    {
        var unsupportedType = (IncomeType)42;
        var filing = Filing.CreateFromIncome(
            Guid.NewGuid(), unsupportedType, "ACME Corp",
            new DateOnly(2025, 3, 15), 10000m, 0m, 1500m, 1500m,
            new DateOnly(2025, 4, 30));

        var result = _sut.Serialize(filing, MakeProfile(), string.Empty);

        result.IsSuccess.Should().BeFalse();
        result.Error.Message.Should().Contain("42");
    }

    /// <summary>
    /// Verify both known income types are supported (regression guard).
    /// </summary>
    [Theory]
    [InlineData(IncomeType.Dividend)]
    [InlineData(IncomeType.Interest)]
    public void Serialize_KnownIncomeType_ReturnsSuccess(IncomeType incomeType)
    {
        var filing = Filing.CreateFromIncome(
            Guid.NewGuid(), incomeType, "ACME Corp",
            new DateOnly(2025, 3, 15), 10000m, 0m, 1500m, 1500m,
            new DateOnly(2025, 4, 30));

        var result = _sut.Serialize(filing, MakeProfile(), string.Empty);

        result.IsSuccess.Should().BeTrue();
    }

    /// <summary>
    /// Decimal values with a single fractional digit must be serialized with
    /// exactly two decimal places (1.5 → "1.50"), not truncated.
    /// </summary>
    [Fact]
    public void Serialize_DecimalWithSingleFractionalDigit_FormattedWithTwoDecimalPlaces()
    {
        // whtPaid=121.9 so taxPayable = Math.Max(123.4 - 121.9, 0) = 1.5  (satisfies domain invariant)
        var filing = Filing.CreateFromIncome(
            Guid.NewGuid(), IncomeType.Dividend, "ACME Corp",
            new DateOnly(2025, 3, 15), 1234.5m, 121.9m, 123.4m, 1.5m,
            new DateOnly(2025, 4, 30));

        var result = _sut.Serialize(filing, MakeProfile(), string.Empty);

        result.IsSuccess.Should().BeTrue();
        var xml = System.Text.Encoding.UTF8.GetString(result.Value);
        xml.Should().Contain("1234.50");
        xml.Should().Contain("123.40");
        xml.Should().Contain("1.50");
    }
}
