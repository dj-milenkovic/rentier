using System.Net;
using FluentAssertions;
using NSubstitute;
using Rentier.Application.Repositories;
using Rentier.Domain.ValueObjects;
using Rentier.Infrastructure.ExchangeRates;
using Xunit;

namespace Rentier.Infrastructure.Tests;

internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly string _response;
    private readonly HttpStatusCode _statusCode;
    public int CallCount { get; private set; }

    internal FakeHttpMessageHandler(string response,
        HttpStatusCode statusCode = HttpStatusCode.OK)
    { _response = response; _statusCode = statusCode; }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken ct)
    {
        CallCount++;
        return Task.FromResult(new HttpResponseMessage(_statusCode)
        { Content = new StringContent(_response) });
    }
}

public class NbsExchangeRateFetcherTests
{
    private static readonly DateOnly TestDate = new(2024, 1, 15);

    private const string ValidXml = """
        <?xml version="1.0" encoding="utf-8"?>
        <ArrayOfExchangeRateXml xmlns="http://www.nbs.rs/kursnaListaModul/schema">
          <ExchangeRateXml>
            <CurrencyCodeCo>EUR</CurrencyCodeCo>
            <Unit>1</Unit>
            <Middle_Rate>117.5952</Middle_Rate>
          </ExchangeRateXml>
          <ExchangeRateXml>
            <CurrencyCodeCo>USD</CurrencyCodeCo>
            <Unit>1</Unit>
            <Middle_Rate>108.4321</Middle_Rate>
          </ExchangeRateXml>
          <ExchangeRateXml>
            <CurrencyCodeCo>JPY</CurrencyCodeCo>
            <Unit>100</Unit>
            <Middle_Rate>77.3500</Middle_Rate>
          </ExchangeRateXml>
        </ArrayOfExchangeRateXml>
        """;

    private const string JpyOnlyXml = """
        <?xml version="1.0" encoding="utf-8"?>
        <ArrayOfExchangeRateXml xmlns="http://www.nbs.rs/kursnaListaModul/schema">
          <ExchangeRateXml>
            <CurrencyCodeCo>JPY</CurrencyCodeCo>
            <Unit>100</Unit>
            <Middle_Rate>77.35</Middle_Rate>
          </ExchangeRateXml>
        </ArrayOfExchangeRateXml>
        """;

    private const string EmptyXml = """
        <?xml version="1.0" encoding="utf-8"?>
        <ArrayOfExchangeRateXml xmlns="http://www.nbs.rs/kursnaListaModul/schema">
        </ArrayOfExchangeRateXml>
        """;

    private static NbsExchangeRateFetcher CreateFetcher(
        FakeHttpMessageHandler handler, IExchangeRateCacheRepository repo)
    {
        var client = new HttpClient(handler);
        return new NbsExchangeRateFetcher(client, repo);
    }

    [Fact]
    public async Task FetchRateAsync_UnsupportedCurrency_ReturnsFailureWithoutHttpCall()
    {
        var handler = new FakeHttpMessageHandler(ValidXml);
        var repo = Substitute.For<IExchangeRateCacheRepository>();
        var fetcher = CreateFetcher(handler, repo);

        var result = await fetcher.FetchRateAsync(TestDate, "XYZ", TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("UNSUPPORTED_CURRENCY");
        handler.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task FetchRateAsync_CacheHit_ReturnsCachedRateWithoutHttpCall()
    {
        var handler = new FakeHttpMessageHandler(ValidXml);
        var repo = Substitute.For<IExchangeRateCacheRepository>();
        var cachedRate = new ExchangeRate(TestDate, "EUR", 117m);
        repo.GetAsync(TestDate, "EUR", Arg.Any<CancellationToken>())
            .Returns(cachedRate);

        var fetcher = CreateFetcher(handler, repo);
        var result = await fetcher.FetchRateAsync(TestDate, "EUR", TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.RateToRsd.Should().Be(117m);
        handler.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task FetchRateAsync_CacheMiss_ParsesXmlAndCachesAllRates()
    {
        var handler = new FakeHttpMessageHandler(ValidXml);
        var repo = Substitute.For<IExchangeRateCacheRepository>();
        repo.GetAsync(Arg.Any<DateOnly>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((ExchangeRate?)null);

        var fetcher = CreateFetcher(handler, repo);
        var result = await fetcher.FetchRateAsync(TestDate, "EUR", TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Currency.Should().Be("EUR");
        result.Value.RateToRsd.Should().Be(117.5952m);

        await repo.Received(1).SaveBatchAsync(
            Arg.Is<IReadOnlyList<ExchangeRate>>(list => list!.Count == 3),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FetchRateAsync_CurrencyNotInXml_ReturnsRateNotFoundError()
    {
        // ValidXml has EUR, USD, JPY — CHF is supported but not in this response
        var handler = new FakeHttpMessageHandler(ValidXml);
        var repo = Substitute.For<IExchangeRateCacheRepository>();
        repo.GetAsync(Arg.Any<DateOnly>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((ExchangeRate?)null);

        var fetcher = CreateFetcher(handler, repo);
        var result = await fetcher.FetchRateAsync(TestDate, "CHF", TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("RATE_NOT_FOUND");
    }

    [Fact]
    public async Task FetchRateAsync_HttpError_ReturnsNbsHttpErrorFailure()
    {
        var handler = new FakeHttpMessageHandler("", HttpStatusCode.InternalServerError);
        var repo = Substitute.For<IExchangeRateCacheRepository>();
        repo.GetAsync(Arg.Any<DateOnly>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((ExchangeRate?)null);

        var fetcher = CreateFetcher(handler, repo);
        var result = await fetcher.FetchRateAsync(TestDate, "EUR", TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("NBS_HTTP_ERROR");
    }

    [Fact]
    public async Task FetchRateAsync_MalformedXml_ReturnsParseError()
    {
        var handler = new FakeHttpMessageHandler("this is not xml");
        var repo = Substitute.For<IExchangeRateCacheRepository>();
        repo.GetAsync(Arg.Any<DateOnly>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((ExchangeRate?)null);

        var fetcher = CreateFetcher(handler, repo);
        var result = await fetcher.FetchRateAsync(TestDate, "EUR", TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("NBS_PARSE_ERROR");
    }

    [Fact]
    public async Task FetchRateAsync_UnitIsHundred_ComputesCorrectRate()
    {
        // JPY: Unit=100, Middle_Rate=77.35 → RateToRsd = 77.35 / 100 = 0.7735
        var handler = new FakeHttpMessageHandler(JpyOnlyXml);
        var repo = Substitute.For<IExchangeRateCacheRepository>();
        repo.GetAsync(Arg.Any<DateOnly>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((ExchangeRate?)null);

        var fetcher = CreateFetcher(handler, repo);
        var result = await fetcher.FetchRateAsync(TestDate, "JPY", TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.RateToRsd.Should().Be(0.7735m);
    }

    [Fact]
    public async Task FetchRateAsync_CurrencyCodeCaseInsensitive()
    {
        // Lowercase "eur" should match "EUR" in the XML
        var handler = new FakeHttpMessageHandler(ValidXml);
        var repo = Substitute.For<IExchangeRateCacheRepository>();
        repo.GetAsync(Arg.Any<DateOnly>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((ExchangeRate?)null);

        var fetcher = CreateFetcher(handler, repo);
        var result = await fetcher.FetchRateAsync(TestDate, "eur", TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Currency.Should().Be("EUR");
    }

    [Fact]
    public async Task FetchRateAsync_EmptyXmlResponse_ReturnsRateNotFound()
    {
        var handler = new FakeHttpMessageHandler(EmptyXml);
        var repo = Substitute.For<IExchangeRateCacheRepository>();
        repo.GetAsync(Arg.Any<DateOnly>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((ExchangeRate?)null);

        var fetcher = CreateFetcher(handler, repo);
        var result = await fetcher.FetchRateAsync(TestDate, "EUR", TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("RATE_NOT_FOUND");
    }

    [Fact]
    public async Task FetchRateAsync_SaveBatchThrows_StillReturnsRateFromParsedBatch()
    {
        // Arrange — cache miss; SaveBatchAsync throws a DbUpdateException (e.g. concurrency).
        // The fetch result must still succeed — cache write failure is non-fatal.
        var handler = new FakeHttpMessageHandler(ValidXml);
        var repo = Substitute.For<IExchangeRateCacheRepository>();
        repo.GetAsync(Arg.Any<DateOnly>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((ExchangeRate?)null);
        repo.SaveBatchAsync(Arg.Any<IReadOnlyList<ExchangeRate>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new Microsoft.EntityFrameworkCore.DbUpdateException("concurrency", new Exception())));

        var fetcher = new NbsExchangeRateFetcher(new HttpClient(handler), repo);

        // Act
        var result = await fetcher.FetchRateAsync(TestDate, "EUR", TestContext.Current.CancellationToken);

        // Assert — rate from in-memory batch is returned even though cache write failed
        result.IsSuccess.Should().BeTrue();
        result.Value.Currency.Should().Be("EUR");
        result.Value.RateToRsd.Should().Be(117.5952m);
    }
}

// Requires live NBS HTTP endpoint — run manually: dotnet test --filter "Category=Live"
[Trait("Category", "Live")]
public class NbsIntegrationTests
{
    [Fact(Skip = "Requires live NBS endpoint — set no env var needed, just remove Skip")]
    public async Task FetchRateAsync_RealNbs_ReturnsEurRate()
    {
        var repo = Substitute.For<IExchangeRateCacheRepository>();
        repo.GetAsync(Arg.Any<DateOnly>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((ExchangeRate?)null);
        repo.SaveBatchAsync(Arg.Any<IReadOnlyList<ExchangeRate>>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var fetcher = new NbsExchangeRateFetcher(new HttpClient(), repo);
        var result = await fetcher.FetchRateAsync(new DateOnly(2024, 1, 15), "EUR", TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.RateToRsd.Should().BeGreaterThan(100m);
    }

    [Fact(Skip = "Requires live NBS endpoint — set no env var needed, just remove Skip")]
    public async Task FetchRateAsync_RealNbs_ReturnsUsdRate()
    {
        var repo = Substitute.For<IExchangeRateCacheRepository>();
        repo.GetAsync(Arg.Any<DateOnly>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((ExchangeRate?)null);
        repo.SaveBatchAsync(Arg.Any<IReadOnlyList<ExchangeRate>>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var fetcher = new NbsExchangeRateFetcher(new HttpClient(), repo);
        var result = await fetcher.FetchRateAsync(new DateOnly(2024, 1, 15), "USD", TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.RateToRsd.Should().BeGreaterThan(0m);
    }
}
