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
            html = await _http.GetStringAsync(url, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return Result<IReadOnlyList<HolidayEntryDto>, Error>.Failure(
                new Error("FETCH_FAILED", $"Failed to fetch holidays: {ex.Message}"));
        }

        try
        {
            var config = Configuration.Default.WithDefaultLoader();
            var context = BrowsingContext.New(config);
            var document = await context.OpenAsync(req => req.Content(html), cancellationToken);

            var rows = document.QuerySelectorAll("table.table tr");
            var results = new List<HolidayEntryDto>();
            var formats = new[] { "MMM d", "d MMM", "MMM dd", "dd MMM" };

            foreach (var row in rows)
            {
                if (row.ClassList.Contains("noshow") || row.ClassList.Contains("js-holiday-private"))
                    continue;

                var cells = row.QuerySelectorAll("td");
                if (cells.Length < 2) continue;

                var dateText = cells[0].TextContent.Trim();
                var nameCell = row.QuerySelector("td.ce") ?? cells[1];
                var name = nameCell.TextContent.Trim();

                if (string.IsNullOrWhiteSpace(dateText) || string.IsNullOrWhiteSpace(name))
                    continue;

                DateOnly date;
                try
                {
                    var parsed = DateOnly.ParseExact(dateText, formats,
                        CultureInfo.InvariantCulture, DateTimeStyles.None);
                    date = new DateOnly(year, parsed.Month, parsed.Day);
                }
                catch
                {
                    continue;
                }

                results.Add(new HolidayEntryDto(date, name));
            }

            if (results.Count == 0)
                return Result<IReadOnlyList<HolidayEntryDto>, Error>.Failure(
                    new Error("NO_HOLIDAYS_FOUND", $"No holidays found for year {year}"));

            return Result<IReadOnlyList<HolidayEntryDto>, Error>.Success(results);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<HolidayEntryDto>, Error>.Failure(
                new Error("PARSE_FAILED", $"Failed to parse holidays: {ex.Message}"));
        }
    }
}
