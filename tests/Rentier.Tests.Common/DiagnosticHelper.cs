using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Rentier.Infrastructure.Persistence;

namespace Rentier.Tests.Common;

/// <summary>
/// Captures diagnostic context (database state, fixture state, logs) on test failure.
/// Logs are printed to Debug output and visible in CI job logs with distinctive formatting.
/// </summary>
public class DiagnosticHelper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = false,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// Logs the current state of specified database tables.
    /// Useful for debugging failed integration tests.
    /// </summary>
    public static async Task LogDatabaseStateAsync(
        AppDbContext context,
        string testName,
        IEnumerable<string> tablesToCapture,
        CancellationToken cancellationToken = default)
    {
        var diagnostics = new Dictionary<string, object>();

        foreach (var table in tablesToCapture)
        {
            try
            {
                // Use raw SQL to query each table and convert to dictionary
                var sql = $"SELECT * FROM {table};";
                var result = await context.Database
                    .SqlQuery<dynamic>(FormattableStringFactory.Create(sql))
                    .ToListAsync(cancellationToken);

                diagnostics[table] = result.Count == 0
                    ? "(empty)"
                    : result;
            }
            catch (Exception ex)
            {
                diagnostics[table] = $"ERROR: {ex.GetType().Name} — {ex.Message}";
            }
        }

        var json = JsonSerializer.Serialize(diagnostics, JsonOptions);

        System.Diagnostics.Debug.WriteLine(
            $"\n╔════════════════════════════════════════════════════════════════════════════════╗\n" +
            $"║ TEST FAILURE DIAGNOSTIC: {testName,-75} ║\n" +
            $"╚════════════════════════════════════════════════════════════════════════════════╝\n" +
            $"{json}\n"
        );
    }

    /// <summary>
    /// Logs the state of a fixture or test context object.
    /// Serializes the object to JSON for inspection.
    /// </summary>
    public static void LogFixtureState(string fixtureName, object? state)
    {
        try
        {
            var json = JsonSerializer.Serialize(state, JsonOptions);
            System.Diagnostics.Debug.WriteLine(
                $"\n╔════════════════════════════════════════════════════════════════════════════════╗\n" +
                $"║ FIXTURE STATE: {fixtureName,-70} ║\n" +
                $"╚════════════════════════════════════════════════════════════════════════════════╝\n" +
                $"{json}\n"
            );
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"\n[FIXTURE STATE ERROR] {fixtureName}: Failed to serialize — {ex.Message}\n"
            );
        }
    }

    /// <summary>
    /// Logs an arbitrary diagnostic message with formatted header.
    /// </summary>
    public static void LogMessage(string title, string message)
    {
        System.Diagnostics.Debug.WriteLine(
            $"\n╔════════════════════════════════════════════════════════════════════════════════╗\n" +
            $"║ {title,-86} ║\n" +
            $"╚════════════════════════════════════════════════════════════════════════════════╝\n" +
            $"{message}\n"
        );
    }

    /// <summary>
    /// Logs a key-value pair dictionary as formatted diagnostic output.
    /// </summary>
    public static void LogDiagnostics(string title, Dictionary<string, string> data)
    {
        var lines = string.Join(
            Environment.NewLine,
            data.Select(kv => $"  {kv.Key}: {kv.Value}")
        );

        LogMessage(title, lines);
    }
}
