using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Application.Handlers;
using Rentier.Application.Interfaces;
using Rentier.Infrastructure;
using Rentier.Tests.Common.Fakes;

namespace Rentier.Scenarios.Tests;

/// <summary>
/// Bootstraps real DI with the production InfrastructureRegistrar and a SQLite temp file.
/// Applies migrations via IDatabaseInitializer (MigrateAsync) — not EnsureCreated.
/// Each test class creates its own instance for full data isolation.
/// </summary>
public sealed class ScenarioFixture : IAsyncLifetime
{
    private string? _dbPath;

    public IServiceProvider Services { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"rentier_e2e_{Guid.NewGuid():N}.db");

        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));

        await new InfrastructureRegistrar().RegisterServicesAsync(services, _dbPath);

        // Override externals — last registration wins in .NET DI
        services.AddSingleton<ICredentialStore>(new FakeCredentialStore());
        services.AddSingleton(NSubstitute.Substitute.For<IMailboxSyncService>());

        // Application layer: command handlers
        services.AddTransient<
            ICommandHandler<ProcessReportsCommand, Result<ProcessReportsResult, Error>>,
            ProcessReportsCommandHandler>();
        services.AddTransient<
            ICommandHandler<ImportReportCommand, Result<Guid, Error>>,
            ImportReportCommandHandler>();
        services.AddTransient<
            ICommandHandler<ExportFilingCommand, Result<ExportFilingResult, Error>>,
            ExportFilingCommandHandler>();
        services.AddTransient<
            ICommandHandler<UpdateFilingStatusCommand, Result<VoidResult, Error>>,
            UpdateFilingStatusCommandHandler>();
        services.AddTransient<
            ICommandHandler<SaveTaxpayerProfileCommand, Result<VoidResult, Error>>,
            SaveTaxpayerProfileCommandHandler>();
        services.AddTransient<
            ICommandHandler<AddImporterCommand, Result<Guid, Error>>,
            AddImporterCommandHandler>();

        Services = services.BuildServiceProvider();

        var dbInit = Services.GetRequiredService<IDatabaseInitializer>();
        await dbInit.InitializeAsync();
    }

    public T Get<T>() where T : notnull => Services.GetRequiredService<T>();

    public async ValueTask DisposeAsync()
    {
        if (Services is IAsyncDisposable asyncDisposable)
            await asyncDisposable.DisposeAsync();

        SqliteConnection.ClearAllPools();

        if (_dbPath is null) return;
        foreach (var path in new[] { _dbPath, $"{_dbPath}-wal", $"{_dbPath}-shm" })
        {
            for (var attempt = 1; attempt <= 3; attempt++)
            {
                if (!File.Exists(path)) break;
                try { File.Delete(path); break; }
                catch (IOException) when (attempt < 3) { await Task.Delay(50); }
                catch (UnauthorizedAccessException) when (attempt < 3) { await Task.Delay(50); }
            }
        }
    }
}
