using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Rentier.Application.Repositories;
using Rentier.Domain.ValueObjects;
using Rentier.Infrastructure.ExchangeRates;
using Rentier.Tests.Common.Fakes;
using Xunit;

namespace Rentier.Infrastructure.Tests.ExchangeRates;

/// <summary>What <see cref="NbsWebScraper"/> writes to the diagnostic log on its failure and cache paths.</summary>
[Trait("Category", "Integration")]
public class NbsWebScraperLoggingTests
{
    private static readonly DateOnly TEST_DATE = new(2024, 1, 15);

    private const string VALID_HTML = """
        <html><body><table><tbody>
        <tr><td>USD</td><td>Dollar</td><td>US</td><td>1</td><td>107,0539</td><td>107,0539</td></tr>
        </tbody></table></body></html>
        """;

    private sealed class FakeHandler(Func<HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
            => Task.FromResult(respond());
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
            => throw new HttpRequestException("Connection refused");
    }

    private static (NbsWebScraper Scraper, CapturingLogger<NbsWebScraper> Log, IExchangeRateCacheRepository Cache) Build(
        HttpMessageHandler handler, ExchangeRate? cached = null)
    {
        var cache = Substitute.For<IExchangeRateCacheRepository>();
        cache.GetAsync(Arg.Any<DateOnly>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(cached);
        var log = new CapturingLogger<NbsWebScraper>();
        return (new NbsWebScraper(new HttpClient(handler), cache, log), log, cache);
    }

    private static FakeHandler Ok() =>
        new(() => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(VALID_HTML) });

    [Fact]
    public async Task FetchRateAsync_ServerError_LogsWarningWithStatusCode()
    {
        var (scraper, log, _) = Build(new FakeHandler(() => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));

        await scraper.FetchRateAsync(TEST_DATE, "USD", TestContext.Current.CancellationToken);

        log.Entries.Should().ContainSingle(e => e.Level == LogLevel.Warning && e.Message.Contains("503"));
    }

    [Fact]
    public async Task FetchRateAsync_NetworkFailure_LogsWarningWithException()
    {
        var (scraper, log, _) = Build(new ThrowingHandler());

        await scraper.FetchRateAsync(TEST_DATE, "USD", TestContext.Current.CancellationToken);

        log.Entries.Should().ContainSingle(e => e.Level == LogLevel.Warning && e.Exception is HttpRequestException);
    }

    [Fact]
    public async Task FetchRateAsync_CacheWriteFails_StillReturnsRate()
    {
        var (scraper, _, cache) = Build(Ok());
        cache.SaveBatchAsync(Arg.Any<IReadOnlyList<ExchangeRate>>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new InvalidOperationException("disk full"));

        var result = await scraper.FetchRateAsync(TEST_DATE, "USD", TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task FetchRateAsync_CacheWriteFails_LogsWarningInsteadOfSwallowingSilently()
    {
        var (scraper, log, cache) = Build(Ok());
        cache.SaveBatchAsync(Arg.Any<IReadOnlyList<ExchangeRate>>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new InvalidOperationException("disk full"));

        await scraper.FetchRateAsync(TEST_DATE, "USD", TestContext.Current.CancellationToken);

        log.Entries.Should().ContainSingle(e => e.Level == LogLevel.Warning && e.Exception is InvalidOperationException);
    }

    [Fact]
    public async Task FetchRateAsync_CacheHit_LogsDebugOnly()
    {
        var cached = new ExchangeRate(TEST_DATE, "USD", 107.0539m);
        var (scraper, log, _) = Build(Ok(), cached);

        await scraper.FetchRateAsync(TEST_DATE, "USD", TestContext.Current.CancellationToken);

        log.Entries.Should().OnlyContain(e => e.Level == LogLevel.Debug);
    }

    [Fact]
    public async Task FetchRateAsync_Success_LogsNoWarningsOrErrors()
    {
        var (scraper, log, _) = Build(Ok());

        await scraper.FetchRateAsync(TEST_DATE, "USD", TestContext.Current.CancellationToken);

        log.Entries.Should().NotContain(e => e.Level >= LogLevel.Warning);
    }
}
