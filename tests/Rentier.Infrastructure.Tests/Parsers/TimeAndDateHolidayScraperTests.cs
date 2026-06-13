using System.Net;
using FluentAssertions;
using Rentier.Infrastructure.Scraping;

namespace Rentier.Infrastructure.Tests.Parsers;

[Trait("Category", "Integration")]
public class TimeAndDateHolidayScraperTests
{
    private static string LoadFixtureHtml()
    {
        var name = "Rentier.Infrastructure.Tests.Parsers.Fixtures.holiday-scraped.txt";
        using var stream = typeof(TimeAndDateHolidayScraperTests).Assembly.GetManifestResourceStream(name)
                           ?? throw new InvalidOperationException($"Fixture not found: {name}");
        return new StreamReader(stream).ReadToEnd();
    }

    private static TimeAndDateHolidayScraper CreateScraper(string html, HttpStatusCode status = HttpStatusCode.OK)
        => new(new HttpClient(new FakeHttpMessageHandler(html, status)));

    [Fact]
    public async Task ImportAsync_WithRealFixture_Returns11UniqueNationalHolidays()
    {
        var result = await CreateScraper(LoadFixtureHtml()).ImportAsync(2016, TestContext.Current.CancellationToken);
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(11);
        result.Value.Select(e => e.Date).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task ImportAsync_WithRealFixture_FirstEntryIsNewYear()
    {
        var result = await CreateScraper(LoadFixtureHtml()).ImportAsync(2016, TestContext.Current.CancellationToken);
        result.IsSuccess.Should().BeTrue();
        result.Value[0].Date.Should().Be(new DateOnly(2016, 1, 1));
        result.Value[0].Name.Should().Be("Western New Year's Day");
    }

    [Fact]
    public async Task ImportAsync_WithRealFixture_ContainsStatehoodDay()
    {
        var result = await CreateScraper(LoadFixtureHtml()).ImportAsync(2016, TestContext.Current.CancellationToken);
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain(e => e.Date == new DateOnly(2016, 2, 15) && e.Name == "Statehood Day of the Republic of Serbia");
    }

    [Fact]
    public async Task ImportAsync_WithRealFixture_ContainsArmisticeDay()
    {
        var result = await CreateScraper(LoadFixtureHtml()).ImportAsync(2016, TestContext.Current.CancellationToken);
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain(e => e.Date == new DateOnly(2016, 11, 11) && e.Name == "Armistice Day");
    }

    [Fact]
    public async Task ImportAsync_HtmlWithoutHolidaysTable_ReturnsParseError()
    {
        var result = await CreateScraper("<html><body><p>No table</p></body></html>").ImportAsync(2016, TestContext.Current.CancellationToken);
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("HOLIDAY_PARSE_ERROR");
    }

    [Fact]
    public async Task ImportAsync_HolidaysTableWithOnlyHideRows_ReturnsNotFound()
    {
        var html = """
            <html><body><table id="holidays-table">
              <tr class="hiderow"><th>1 Jan</th><td>Fri</td><td><a>X</a></td><td>Observance</td></tr>
            </table></body></html>
            """;
        var result = await CreateScraper(html).ImportAsync(2016, TestContext.Current.CancellationToken);
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("HOLIDAY_NOT_FOUND");
    }

    [Fact]
    public async Task ImportAsync_DuplicateDates_OnlyFirstEntryKept()
    {
        var html = """
            <html><body><table id="holidays-table">
              <tr class="showrow"><th>1 Jan</th><td>Fri</td><td><a>Western New Year's Day</a></td><td>National Holiday</td></tr>
              <tr class="showrow"><th>1 Jan</th><td>Fri</td><td><a>Duplicate Holiday</a></td><td>National Holiday</td></tr>
            </table></body></html>
            """;
        var result = await CreateScraper(html).ImportAsync(2016, TestContext.Current.CancellationToken);
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].Name.Should().Be("Western New Year's Day");
    }
}
