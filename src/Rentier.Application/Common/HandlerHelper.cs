using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Rentier.Domain.Exceptions;

namespace Rentier.Application.Common;

/// <summary>
/// Shared error-handling helper for CQRS handlers.
/// Provides a standard try-catch pattern that:
/// <list type="bullet">
///   <item>Re-throws <see cref="OperationCanceledException"/> to propagate cancellation correctly.</item>
///   <item>Maps <see cref="DomainException"/> to a domain failure result.</item>
///   <item>Maps all other exceptions to an infrastructure failure result using the supplied error code.</item>
/// </list>
/// </summary>
public static class HandlerHelper
{
    /// <summary>
    /// Executes <paramref name="operation"/> inside a standard try-catch guard.
    /// </summary>
    /// <typeparam name="TValue">The success value type.</typeparam>
    /// <param name="operation">The async handler body to execute.</param>
    /// <param name="errorCode">Error code constant to use for unexpected failures.</param>
    /// <param name="logger">Optional logger — logs unexpected exceptions at Error level.</param>
    /// <param name="caller">Auto-filled caller member name (for log context).</param>
    public static async Task<Result<TValue, Error>> ExecuteAsync<TValue>(
        Func<Task<Result<TValue, Error>>> operation,
        string errorCode,
        ILogger? logger = null,
        [CallerMemberName] string? caller = null)
    {
        try
        {
            return await operation();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (DomainException ex)
        {
            return Result<TValue, Error>.Failure(Error.Domain(ex.Message));
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "[{Caller}] Unexpected failure: {Message}", caller, ex.Message);
            return Result<TValue, Error>.Failure(new Error(errorCode, ex.Message));
        }
    }

    /// <summary>
    /// Runs optional pre-validation before delegating to <see cref="ExecuteAsync{TValue}"/>.
    /// If <paramref name="validation"/> returns a non-<c>null</c> result, that result is returned
    /// immediately and the operation is not executed.
    /// </summary>
    /// <typeparam name="TValue">The success value type.</typeparam>
    /// <param name="validation">
    /// A synchronous validation function returning <c>null</c> when valid, or a failure result.
    /// </param>
    /// <param name="operation">The async handler body to execute when validation passes.</param>
    /// <param name="errorCode">Error code constant to use for unexpected failures.</param>
    /// <param name="logger">Optional logger.</param>
    /// <param name="caller">Auto-filled caller member name.</param>
    public static async Task<Result<TValue, Error>> ExecuteWithValidationAsync<TValue>(
        Func<Result<TValue, Error>?> validation,
        Func<Task<Result<TValue, Error>>> operation,
        string errorCode,
        ILogger? logger = null,
        [CallerMemberName] string? caller = null)
    {
        var failure = validation();
        if (failure is not null)
            return failure;

        return await ExecuteAsync(operation, errorCode, logger, caller);
    }
}
