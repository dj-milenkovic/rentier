using FluentAssertions;
using Rentier.Application.Common;
using Rentier.Domain.Exceptions;
using Xunit;

namespace Rentier.UnitTests;

public class HandlerHelperTests
{
    // ── ExecuteAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_SuccessfulOperation_ReturnsOperationResult()
    {
        var expected = Result<int, Error>.Success(42);

        var result = await HandlerHelper.ExecuteAsync<int>(
            () => Task.FromResult(expected),
            "TEST_ERROR");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
    }

    [Fact]
    public async Task ExecuteAsync_OperationCanceledException_IsRethrown()
    {
        var act = async () => await HandlerHelper.ExecuteAsync<int>(
            () => throw new OperationCanceledException(),
            "TEST_ERROR");

        await act.Should().ThrowAsync<OperationCanceledException>(
            "OperationCanceledException must propagate — not be swallowed");
    }

    [Fact]
    public async Task ExecuteAsync_DomainException_ReturnsDomainFailure()
    {
        const string domainMessage = "Domain rule violated";

        var result = await HandlerHelper.ExecuteAsync<int>(
            () => throw new DomainException(domainMessage),
            "TEST_ERROR");

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(ErrorCodes.DOMAIN_ERROR);
        result.Error.Message.Should().Be(domainMessage);
    }

    [Fact]
    public async Task ExecuteAsync_UnexpectedException_ReturnsInfrastructureFailureWithSuppliedCode()
    {
        const string errorCode = "MY_HANDLER_FAILED";
        const string exceptionMessage = "Something went wrong";

        var result = await HandlerHelper.ExecuteAsync<int>(
            () => throw new InvalidOperationException(exceptionMessage),
            errorCode);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(errorCode);
        result.Error.Message.Should().Be(exceptionMessage);
    }

    [Fact]
    public async Task ExecuteAsync_OperationReturnsFailure_ReturnsItUnchanged()
    {
        var failure = Result<int, Error>.Failure(new Error("CUSTOM_CODE", "custom message"));

        var result = await HandlerHelper.ExecuteAsync<int>(
            () => Task.FromResult(failure),
            "TEST_ERROR");

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("CUSTOM_CODE");
        result.Error.Message.Should().Be("custom message");
    }

    // ── ExecuteWithValidationAsync ────────────────────────────────────────

    [Fact]
    public async Task ExecuteWithValidationAsync_ValidationReturnsNonNull_ShortCircuitsBeforeOperation()
    {
        var validationFailure = Result<int, Error>.Failure(
            new Error("VALIDATION_FAILED", "invalid input"));
        var operationExecuted = false;

        var result = await HandlerHelper.ExecuteWithValidationAsync<int>(
            () => validationFailure,
            () =>
            {
                operationExecuted = true;
                return Task.FromResult(Result<int, Error>.Success(99));
            },
            "TEST_ERROR");

        result.Should().Be(validationFailure);
        operationExecuted.Should().BeFalse("operation must not run when validation fails");
    }

    [Fact]
    public async Task ExecuteWithValidationAsync_ValidationReturnsNull_ProceedsToOperation()
    {
        var operationExecuted = false;

        var result = await HandlerHelper.ExecuteWithValidationAsync<int>(
            () => null,
            () =>
            {
                operationExecuted = true;
                return Task.FromResult(Result<int, Error>.Success(7));
            },
            "TEST_ERROR");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(7);
        operationExecuted.Should().BeTrue("operation runs when validation passes");
    }

    [Fact]
    public async Task ExecuteWithValidationAsync_ValidationPassesButOperationThrows_ReturnsFailure()
    {
        const string errorCode = "OP_FAILED";

        var result = await HandlerHelper.ExecuteWithValidationAsync<int>(
            () => null,
            () => throw new InvalidOperationException("boom"),
            errorCode);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(errorCode);
    }
}
