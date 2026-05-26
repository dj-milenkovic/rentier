using System.Net;
using FluentAssertions;
using NSubstitute;
using Rentier.Application.Repositories;
using Rentier.Domain.ValueObjects;
using Rentier.Infrastructure.ExchangeRates;
using Xunit;

namespace Rentier.Infrastructure.Tests.ExchangeRates;

[Trait("Category", "Integration")]
public class NbsWebScraperTests
{
    private static readonly DateOnly TestDate = new(2024, 1, 15);

    private const string ValidHtmlTable = """
        <html><body>
        <table>
        <thead><tr><th>Sifra</th><th>Naziv</th><th>Zemlja</th><th>Jedinica</th><th>Kupovni</th><th>Prodajni</th></tr></thead>
        <tbody>
        <tr><td>EUR</td><td>Euro</td><td>EU</td><td>1</td><td>116,9765</td><td>117,1139</td></tr>
        <tr><td>USD</td><td>Dollar</td><td>US</td><td>1</td><td>107,0539</td><td>107,0539</td></tr>
        <tr><td>GBP</td><td>Pound</td><td>GB</td><td>1</td><td>136,0000</td><td>138,0000</td></tr>
        </tbody>
        </table>
        </body></html>
        """;

    private sealed class FakeHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
            => Task.FromResult(response);
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
            => throw new HttpRequestException("Connection refused");
    }

    private static NbsWebScraper Make(HttpResponseMessage response)
    {
        var cache = Substitute.For<IExchangeRateCacheRepository>();
        cache.GetAsync(Arg.Any<DateOnly>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((ExchangeRate?)null);
        return new NbsWebScraper(new HttpClient(new FakeHandler(response)), cache);
    }

    [Fact]
    public async Task FetchRateAsync_ValidHtmlTable_ParsesMiddleRateCorrectly()
    {
        var cache = Substitute.For<IExchangeRateCacheRepository>();
        cache.GetAsync(Arg.Any<DateOnly>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((ExchangeRate?)null);
        var scraper = new NbsWebScraper(
            new HttpClient(new FakeHandler(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent(ValidHtmlTable) })), cache);

        var result = await scraper.FetchRateAsync(TestDate, "USD");

        result.IsSuccess.Should().BeTrue();
        result.Value.Currency.Should().Be("USD");
        result.Value.RateToRsd.Should().Be(107.0539m);
        await cache.Received(1).SaveBatchAsync(
            Arg.Is<IReadOnlyList<ExchangeRate>>(l => l.Count == 3), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FetchRateAsync_CommaDecimalSeparator_ParsedCorrectly()
    {
        const string html = "<html><body><table><tbody>" +
            "<tr><td>EUR</td><td>E</td><td>EU</td><td>1</td><td>117,0539</td><td>117,0539</td></tr>" +
            "</tbody></table></body></html>";
        var result = await Make(new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new StringContent(html) }).FetchRateAsync(TestDate, "EUR");
        result.IsSuccess.Should().BeTrue();
        result.Value.RateToRsd.Should().Be(117.0539m);
    }

    [Fact]
    public async Task FetchRateAsync_EmptyTableRows_ReturnsRateNotFound()
    {
        const string html = "<html><body><table><thead><tr><th>A</th><th>B</th><th>C</th><th>D</th><th>E</th><th>F</th></tr></thead><tbody></tbody></table></body></html>";
        var result = await Make(new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new StringContent(html) }).FetchRateAsync(TestDate, "USD");
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("RATE_NOT_FOUND");
    }

    [Fact]
    public async Task FetchRateAsync_NoTableFound_ReturnsNbsScrapeError()
    {
        var result = await Make(new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new StringContent("<html><body><p>no table</p></body></html>") })
            .FetchRateAsync(TestDate, "USD");
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().BeOneOf("RATE_NOT_FOUND", "NBS_SCRAPE_ERROR");
    }

    [Fact]
    public async Task FetchRateAsync_WrongColumnCount_ReturnsRateNotFound()
    {
        const string html = "<html><body><table><tbody><tr><td>EUR</td><td>E</td><td>EU</td></tr></tbody></table></body></html>";
        var result = await Make(new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new StringContent(html) }).FetchRateAsync(TestDate, "USD");
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("RATE_NOT_FOUND");
    }

    [Fact]
    public async Task FetchRateAsync_Http500Response_ReturnsNbsHttpError()
    {
        var result = await Make(new HttpResponseMessage(HttpStatusCode.InternalServerError))
            .FetchRateAsync(TestDate, "USD");
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("NBS_HTTP_ERROR");
    }

    [Fact]
    public async Task FetchRateAsync_HttpRequestException_ReturnsNbsHttpError()
    {
        var cache = Substitute.For<IExchangeRateCacheRepository>();
        cache.GetAsync(Arg.Any<DateOnly>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((ExchangeRate?)null);
        var scraper = new NbsWebScraper(new HttpClient(new ThrowingHandler()), cache);
        var result = await scraper.FetchRateAsync(TestDate, "USD");
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("NBS_HTTP_ERROR");
    }
}
