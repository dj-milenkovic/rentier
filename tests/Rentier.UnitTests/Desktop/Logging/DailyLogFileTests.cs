using FluentAssertions;
using Rentier.Desktop.Logging;
using Serilog.Core;
using Serilog.Events;
using Xunit;

namespace Rentier.UnitTests.Desktop.Logging;

/// <summary>
/// Exercises the real file sink built by <see cref="LoggingConfiguration.CreateFileLogger"/>
/// against a private temp folder: the "one file per day" contract is a property of the sink's
/// file handling, which an in-memory sink cannot show. Kept in the unit bucket (fast, local temp
/// folder, cleaned up) so it runs on the Windows/macOS/Linux matrix — file sharing differs by OS.
/// </summary>
public sealed class DailyLogFileTests : IDisposable
{
    private readonly string _logDirectory =
        Directory.CreateTempSubdirectory("rentier-log-tests-").FullName;

    public void Dispose()
    {
        try { Directory.Delete(_logDirectory, recursive: true); }
        catch (IOException) { /* best effort: a leftover temp folder must not fail the run */ }
    }

    /// <summary>
    /// Two sessions hold the log open at the same time (a second instance, or a first one that
    /// is still exiting or stopped in the debugger), each writes one line, then both close.
    /// </summary>
    private string[] WriteFromTwoOverlappingSessions()
    {
        var first = LoggingConfiguration.CreateFileLogger(_logDirectory, Switch(), "first1");
        var second = LoggingConfiguration.CreateFileLogger(_logDirectory, Switch(), "secnd2");

        first.Information("written by the first session");
        second.Information("written by the second session");
        first.Dispose();
        second.Dispose();

        return Directory.GetFiles(_logDirectory, "rentier-*.log");
    }

    private static LoggingLevelSwitch Switch() => new(LogEventLevel.Information);

    [Fact]
    public void CreateFileLogger_TwoSessionsOpenAtOnce_WriteToASingleFile()
    {
        WriteFromTwoOverlappingSessions().Should().ContainSingle();
    }

    [Fact]
    public void CreateFileLogger_TwoSessionsOpenAtOnce_FileHasNoSequenceSuffix()
    {
        var file = Path.GetFileName(WriteFromTwoOverlappingSessions().Single());

        file.Should().MatchRegex(@"^rentier-\d{8}\.log$");
    }

    [Fact]
    public void CreateFileLogger_TwoSessionsOpenAtOnce_KeepsBothSessionsLines()
    {
        var text = File.ReadAllText(WriteFromTwoOverlappingSessions().Single());

        text.Should().Contain("written by the first session").And.Contain("written by the second session");
    }

    [Fact]
    public void CreateFileLogger_AppRestartedSameDay_AppendsToTheExistingFile()
    {
        using (var morning = LoggingConfiguration.CreateFileLogger(_logDirectory, Switch(), "mornin"))
            morning.Information("before restart");
        using (var afternoon = LoggingConfiguration.CreateFileLogger(_logDirectory, Switch(), "aftrnn"))
            afternoon.Information("after restart");

        var text = File.ReadAllText(Directory.GetFiles(_logDirectory, "rentier-*.log").Single());

        text.Should().Contain("before restart").And.Contain("after restart");
    }
}
