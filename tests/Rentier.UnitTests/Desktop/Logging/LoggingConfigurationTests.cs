using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Rentier.Desktop.Composition;
using Rentier.Desktop.Logging;
using Rentier.Desktop.Services;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Xunit;
using ILogger = Serilog.ILogger;

namespace Rentier.UnitTests.Desktop.Logging;

public class LoggingConfigurationTests
{
    private const string SESSION_ID = "abc123";
    private const string EF_SOURCE = "Microsoft.EntityFrameworkCore.Database.Command";
    private const string HTTP_SOURCE = "System.Net.Http.HttpClient.NbsWebScraper.LogicalHandler";
    private const string APP_SOURCE = "Rentier.Infrastructure.ExchangeRates.NbsWebScraper";

    private static (ILogger Logger, CapturingSink Sink, LoggingLevelSwitch Switch) Build(
        LogEventLevel level = LogEventLevel.Information, bool withFormatter = false)
    {
        var levelSwitch = new LoggingLevelSwitch(level);
        var sink = new CapturingSink(withFormatter ? new RedactingTextFormatter(@"C:\Users\alice") : null);
        var logger = LoggingConfiguration.Create(levelSwitch, SESSION_ID).WriteTo.Sink(sink).CreateLogger();
        return (logger, sink, levelSwitch);
    }

    [Fact]
    public void Create_InformationLevel_DropsDebugEvents()
    {
        var (logger, sink, _) = Build(LogEventLevel.Information);

        logger.ForContext("SourceContext", APP_SOURCE).Debug("detail");

        sink.Events.Should().BeEmpty();
    }

    [Fact]
    public void Create_InformationLevel_KeepsInformationEvents()
    {
        var (logger, sink, _) = Build(LogEventLevel.Information);

        logger.ForContext("SourceContext", APP_SOURCE).Information("hello");

        sink.Events.Should().ContainSingle();
    }

    [Fact]
    public void Create_SwitchRaisedToDebug_KeepsDebugEventsWithoutRebuildingLogger()
    {
        var (logger, sink, levelSwitch) = Build(LogEventLevel.Information);

        levelSwitch.MinimumLevel = LogEventLevel.Debug;
        logger.ForContext("SourceContext", APP_SOURCE).Debug("detail");

        sink.Events.Should().ContainSingle();
    }

    [Theory]
    [InlineData(EF_SOURCE)]
    [InlineData(HTTP_SOURCE)]
    [InlineData("Microsoft.Extensions.Http.DefaultHttpClientFactory")]
    public void Create_NoisyNamespaceInformationEvent_IsDroppedEvenAtDebugLevel(string sourceContext)
    {
        var (logger, sink, _) = Build(LogEventLevel.Debug);

        logger.ForContext("SourceContext", sourceContext).Information("Executed DbCommand");

        sink.Events.Should().BeEmpty();
    }

    [Fact]
    public void Create_NoisyNamespaceWarning_IsKept()
    {
        var (logger, sink, _) = Build(LogEventLevel.Debug);

        logger.ForContext("SourceContext", EF_SOURCE).Warning("slow query");

        sink.Events.Should().ContainSingle();
    }

    [Fact]
    public void Create_SensitivePropertyName_ReplacesValueWithPlaceholder()
    {
        var (logger, sink, _) = Build();

        logger.Information("Login failed for {Username}", "john.doe");

        sink.Events.Single().RenderMessage().Should().Be("Login failed for \"[redacted]\"");
    }

    [Fact]
    public void Create_AnyEvent_CarriesSessionId()
    {
        var (logger, sink, _) = Build();

        logger.Information("hello");

        sink.Events.Single().Properties["SessionId"].ToString().Should().Be($"\"{SESSION_ID}\"");
    }

    [Fact]
    public void Formatter_Event_WritesTimestampLevelSessionSourceAndMessage()
    {
        var (logger, sink, _) = Build(withFormatter: true);

        logger.ForContext("SourceContext", APP_SOURCE).Information("Fetched {Count} rates", 3);

        sink.Lines.Single().Should().MatchRegex(
            @"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3} [+-]\d{2}:\d{2} \[INF\] abc123 NbsWebScraper Fetched 3 rates$");
    }

    [Fact]
    public void Formatter_MessageContainingEmail_ScrubsEmail()
    {
        var (logger, sink, _) = Build(withFormatter: true);

        logger.Information("Could not reach mailbox owned by jane@example.com");

        sink.Lines.Single().Should().NotContain("jane@example.com").And.Contain("[email]");
    }

    [Fact]
    public void Formatter_ExceptionWithPathAndSensitiveMessage_ScrubsBoth()
    {
        var (logger, sink, _) = Build(withFormatter: true);
        var ex = new InvalidOperationException(@"cannot open C:\Users\alice\Documents\statement.csv");

        logger.Error(ex, "Import failed");

        sink.Lines.Single().Should().NotContain("alice").And.Contain("~");
    }

    [Fact]
    public void Formatter_FormatExceptionWithAmount_DoesNotWriteAmount()
    {
        var (logger, sink, _) = Build(withFormatter: true);

        logger.Error(new FormatException("input '98765.43' invalid"), "Parse failed");

        sink.Lines.Single().Should().NotContain("98765.43");
    }

    // ── End to end through Microsoft.Extensions.Logging ────────────────────────

    [Fact]
    public void AddRentierLogging_DebugCallWhileSwitchIsInformation_ReachesNoSink()
    {
        var (provider, sink, _) = BuildProvider();

        provider.GetRequiredService<ILogger<LoggingConfigurationTests>>().LogDebug("detail");

        sink.Events.Should().BeEmpty();
    }

    [Fact]
    public void AddRentierLogging_DebugCallAfterEnablingDebugLogging_ReachesSink()
    {
        var (provider, sink, _) = BuildProvider();

        provider.GetRequiredService<IDiagnosticsService>().SetDebugLogging(true);
        provider.GetRequiredService<ILogger<LoggingConfigurationTests>>().LogDebug("detail");

        sink.Events.Should().ContainSingle();
    }

    [Fact]
    public void AddRentierLogging_InformationCall_ReachesSinkWithSourceContext()
    {
        var (provider, sink, _) = BuildProvider();

        provider.GetRequiredService<ILogger<LoggingConfigurationTests>>().LogInformation("hello");

        sink.Events.Single().Properties["SourceContext"].ToString()
            .Should().Contain(nameof(LoggingConfigurationTests));
    }

    private static (ServiceProvider Provider, CapturingSink Sink, LoggingLevelSwitch Switch) BuildProvider()
    {
        var (logger, sink, levelSwitch) = Build();
        var session = LoggingSession.Create(Path.GetTempPath(), levelSwitch, logger);

        var services = new ServiceCollection();
        services.AddRentierLogging(session);
        return (services.BuildServiceProvider(), sink, levelSwitch);
    }
}

public class DiagnosticsServiceTests
{
    private static DiagnosticsService Create(LogEventLevel level, out LoggingLevelSwitch levelSwitch)
    {
        levelSwitch = new LoggingLevelSwitch(level);
        return new DiagnosticsService(
            LoggingSession.Create(@"C:\logs", levelSwitch, Serilog.Core.Logger.None));
    }

    [Fact]
    public void IsDebugLoggingEnabled_InformationLevel_IsFalse()
    {
        Create(LogEventLevel.Information, out _).IsDebugLoggingEnabled.Should().BeFalse();
    }

    [Fact]
    public void SetDebugLogging_True_LowersSwitchToDebug()
    {
        var service = Create(LogEventLevel.Information, out var levelSwitch);

        service.SetDebugLogging(true);

        levelSwitch.MinimumLevel.Should().Be(LogEventLevel.Debug);
    }

    [Fact]
    public void SetDebugLogging_FalseAfterTrue_RestoresInformation()
    {
        var service = Create(LogEventLevel.Debug, out var levelSwitch);

        service.SetDebugLogging(false);

        levelSwitch.MinimumLevel.Should().Be(LogEventLevel.Information);
    }

    [Fact]
    public void LogDirectory_ReturnsSessionDirectory()
    {
        Create(LogEventLevel.Information, out _).LogDirectory.Should().Be(@"C:\logs");
    }
}
