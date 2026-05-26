namespace Rentier.Application.Interfaces;

/// <summary>
/// Execute a command that mutates domain state.
/// Never use .Result or .Wait() on the returned Task.
/// </summary>
/// <typeparam name="TCommand">The command input type.</typeparam>
/// <typeparam name="TResult">The result type indicating outcome.</typeparam>
public interface ICommandHandler<TCommand, TResult>
{
    Task<TResult> HandleAsync(TCommand command, CancellationToken ct = default);
}
