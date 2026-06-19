using Xunit;

namespace Rentier.Infrastructure.Tests.Migrations;

/// <summary>
/// Serializes migration tests that write to the file system.
/// MigrationChainTests and MigrationUpgradeTests create real SQLite files via
/// Path.GetTempPath(); running them concurrently causes SQLITE_BUSY lock errors.
/// In-memory SQLite tests (repositories) live outside this collection and
/// run freely in parallel.
/// </summary>
[CollectionDefinition("Migrations", DisableParallelization = true)]
public sealed class MigrationsCollection { }
