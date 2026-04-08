namespace Rentier.Application.Interfaces;

/// <summary>
/// Execute a read-only query. Returns structured result; never throws for empty-result cases.
/// </summary>
/// <typeparam name="TQuery">The query input type.</typeparam>
/// <typeparam name="TResult">The result type.</typeparam>
public interface IQueryHandler<TQuery, TResult>
{
    Task<TResult> HandleAsync(TQuery query, CancellationToken ct = default);
}
