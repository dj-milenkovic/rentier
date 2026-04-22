using System.Globalization;
using AngleSharp;
using AngleSharp.Html.Dom;
using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Application.Interfaces;

namespace Rentier.Infrastructure.Scraping;

internal sealed class TimeAndDateHolidayScraper : IHolidayImporter
{
    private readonly HttpClient _http;

    public TimeAndDateHolidayScraper(HttpClient http)
    {
        _http = http;
    }

    public async Task<Result<IReadOnlyList<HolidayEntryDto>, Error>> ImportAsync(
        int year, CancellationToken cancellationToken = default)
    {
        string html;
        try
        {
            var url = $"https://www.timeanddate.com/holidays/serbia/{year}?hol=1";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.AcceptLanguage.ParseAdd("en-US");
            using var response = await _http.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            html = await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return Result<IReadOnlyList<HolidayEntryDto>, Error>.Failure(
                new Error("HOLIDAY_IMPORT_FAILED", $"Failed to fetch holidays: {ex.Message}"));
        }

        try
        {
            var config = Configuration.Default;
            var context = BrowsingContext.New(config);
            var document = await context.OpenAsync(req => req.Content(html), cancellationToken);

            var table = document.QuerySelector("#holidays-table");
            if (table == null)
                return Result<IReadOnlyList<HolidayEntryDto>, Error>.Failure(
                    new Error("HOLIDAY_PARSE_ERROR", "Could not find #holidays-table in the response"));

            var rows = table.QuerySelectorAll("tr");
            var results = new List<HolidayEntryDto>();
            var seenDates = new HashSet<DateOnly>();

            foreach (var row in rows)
            {
                if (!row.ClassList.Contains("showrow"))
                    continue;

                var dateText = row.QuerySelector("th")?.TextContent.Trim();
                if (string.IsNullOrWhiteSpace(dateText))
                    continue;

                var tds = row.QuerySelectorAll("td");
                if (tds.Length < 3)
                    continue;

                var name = tds[1].QuerySelector("a")?.TextContent.Trim()
                    ?? tds[1].TextContent.Trim();
                var type = tds[2].TextContent.Trim();

                if (!type.Contains("National Holiday") || string.IsNullOrWhiteSpace(name))
                    continue;

                DateOnly date;
                if (!TryParseHolidayDate(dateText, year, out date))
                    continue;

                if (!seenDates.Add(date))
                    continue;

                results.Add(new HolidayEntryDto(date, name));
            }

            if (results.Count == 0)
                return Result<IReadOnlyList<HolidayEntryDto>, Error>.Failure(
                    new Error("HOLIDAY_NOT_FOUND", $"No national holidays found for year {year}"));

            return Result<IReadOnlyList<HolidayEntryDto>, Error>.Success(results);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<HolidayEntryDto>, Error>.Failure(
                new Error("HOLIDAY_PARSE_ERROR", $"Failed to parse holidays: {ex.Message}"));
        }
    }

    private static bool TryParseHolidayDate(string text, int year, out DateOnly date)
    {
        if (DateTime.TryParseExact(text, ["d MMM", "dd MMM"], CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var parsed))
        {
            date = new DateOnly(year, parsed.Month, parsed.Day);
            return true;
        }
        date = default;
        return false;
    }
}
