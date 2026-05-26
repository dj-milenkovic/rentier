using FluentAssertions;
using Rentier.Application.Common;
using Rentier.Application.Queries;
using Xunit;

namespace Rentier.UnitTests;

public class PaginationValidatorTests
{
    private record FakeQuery(int Page, int PageSize) : IPaginatedQuery;

    [Fact]
    public void Page_Zero_ReturnsFailure()
    {
        var result = PaginationValidator.Validate<string>(new FakeQuery(0, 10));

        result.Should().NotBeNull();
        result!.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(ErrorCodes.PAGINATION_VALIDATION_FAILED);
        result.Error.Message.Should().Contain("Page");
    }

    [Fact]
    public void Page_Negative_ReturnsFailure()
    {
        var result = PaginationValidator.Validate<string>(new FakeQuery(-1, 10));

        result.Should().NotBeNull();
        result!.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(ErrorCodes.PAGINATION_VALIDATION_FAILED);
    }

    [Fact]
    public void Page_One_ReturnsNull()
    {
        var result = PaginationValidator.Validate<string>(new FakeQuery(1, 10));

        result.Should().BeNull("page=1 is the minimum valid page number");
    }

    [Fact]
    public void Page_Two_ReturnsNull()
    {
        var result = PaginationValidator.Validate<string>(new FakeQuery(2, 30));

        result.Should().BeNull();
    }

    [Fact]
    public void PageSize_Zero_ReturnsFailure()
    {
        var result = PaginationValidator.Validate<string>(new FakeQuery(1, 0));

        result.Should().NotBeNull();
        result!.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(ErrorCodes.PAGINATION_VALIDATION_FAILED);
        result.Error.Message.Should().Contain("PageSize");
    }

    [Fact]
    public void PageSize_Negative_ReturnsFailure()
    {
        var result = PaginationValidator.Validate<string>(new FakeQuery(1, -5));

        result.Should().NotBeNull();
        result!.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(ErrorCodes.PAGINATION_VALIDATION_FAILED);
    }

    [Fact]
    public void PageSize_One_ReturnsNull()
    {
        var result = PaginationValidator.Validate<string>(new FakeQuery(1, 1));

        result.Should().BeNull("page size=1 is the minimum valid page size");
    }

    [Fact]
    public void PageSize_Hundred_ReturnsNull()
    {
        var result = PaginationValidator.Validate<string>(new FakeQuery(1, 100));

        result.Should().BeNull("page size=100 is the maximum valid page size");
    }

    [Fact]
    public void PageSize_OneHundredOne_ReturnsFailure()
    {
        var result = PaginationValidator.Validate<string>(new FakeQuery(1, 101));

        result.Should().NotBeNull();
        result!.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(ErrorCodes.PAGINATION_VALIDATION_FAILED);
        result.Error.Message.Should().Contain("PageSize");
    }

    [Fact]
    public void ValidPagination_Page2_Size30_ReturnsNull()
    {
        var result = PaginationValidator.Validate<string>(new FakeQuery(2, 30));

        result.Should().BeNull("page=2, size=30 is a valid pagination combination");
    }

    [Fact]
    public void Page_ValidatedFirst_BeforePageSize()
    {
        // When both are invalid, Page is checked first
        var result = PaginationValidator.Validate<string>(new FakeQuery(0, 0));

        result.Should().NotBeNull();
        result!.Error.Message.Should().Contain("Page");
    }
}
