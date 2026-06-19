using Xunit;

namespace Rentier.UnitTests;

/// <summary>
/// Unit tests (pure logic, no I/O) run in full parallel.
/// No shared state or external dependencies.
/// </summary>
[CollectionDefinition("Unit Tests", DisableParallelization = false)]
public class UnitTestCollection { }

/// <summary>
/// Integration tests with in-memory SQLite.
/// Run serially to avoid connection/database contention.
/// Each test is responsible for its own data cleanup or isolation.
/// </summary>
[CollectionDefinition("Integration Tests", DisableParallelization = true)]
public class IntegrationTestCollection { }

/// <summary>
/// E2E and external service tests that may interact with real APIs or filesystems.
/// Run serially to avoid rate limiting or resource exhaustion.
/// Skipped on normal PR runs; used for nightly/scheduled testing.
/// </summary>
[CollectionDefinition("E2E Tests", DisableParallelization = true)]
public class E2ETestCollection { }

/// <summary>
/// Platform-specific tests (OS credential stores, platform detection).
/// Run serially and are typically skipped in CI (marked [Explicit]).
/// Useful for local validation on a specific OS.
/// </summary>
[CollectionDefinition("Platform Tests", DisableParallelization = true)]
public class PlatformTestCollection { }
