using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Rentier.Desktop.Logging;
using Rentier.Desktop.Services;
using Serilog;

namespace Rentier.Desktop.Composition;

internal static class LoggingServiceExtensions
{
    /// <summary>
    /// Routes every <see cref="ILogger{T}"/> in the container — Application, Infrastructure and
    /// Desktop alike — to the session's Serilog logger, and registers <see cref="IDiagnosticsService"/>.
    /// Call before the Infrastructure registrations so they resolve the same provider.
    /// </summary>
    public static IServiceCollection AddRentierLogging(this IServiceCollection services, LoggingSession session)
    {
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            // Serilog's level switch is the single gate. Leaving the Microsoft default (Information)
            // here would silently drop Debug events even when the switch allows them.
            builder.SetMinimumLevel(LogLevel.Trace);
            builder.AddSerilog(session.Logger, dispose: false);
        });

        services.AddSingleton<IDiagnosticsService>(new DiagnosticsService(session));
        return services;
    }
}
