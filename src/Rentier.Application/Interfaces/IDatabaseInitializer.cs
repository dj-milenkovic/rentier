namespace Rentier.Application.Interfaces;

/// <summary>
/// Contract for initializing the database (applying migrations, creating schema).
/// Implemented in Rentier.Infrastructure and resolved from DI at startup.
/// </summary>
public interface IDatabaseInitializer
{
    /// <summary>
    /// Applies pending database migrations. Called once at application startup.
    /// </summary>
    Task InitializeAsync(CancellationToken ct = default);
}
