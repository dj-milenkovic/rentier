using Microsoft.Extensions.Logging;
using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Application.Interfaces;
using Rentier.Application.Queries;
using Rentier.Application.ReleaseNotes;

namespace Rentier.Application.Handlers;

public sealed class GetInstalledReleaseNotesQueryHandler
    : IQueryHandler<GetInstalledReleaseNotesQuery, Result<IReadOnlyList<ReleaseNoteDto>, Error>>
{
    private readonly IInstalledReleaseNotesSource _source;
    private readonly ILogger<GetInstalledReleaseNotesQueryHandler> _logger;

    public GetInstalledReleaseNotesQueryHandler(
        IInstalledReleaseNotesSource source,
        ILogger<GetInstalledReleaseNotesQueryHandler> logger)
    {
        _source = source;
        _logger = logger;
    }

    public Task<Result<IReadOnlyList<ReleaseNoteDto>, Error>> HandleAsync(
        GetInstalledReleaseNotesQuery query, CancellationToken ct = default) =>
        HandlerHelper.ExecuteAsync<IReadOnlyList<ReleaseNoteDto>>(
            async () =>
            {
                var markdown = await _source.ReadAsync(ct);
                IReadOnlyList<ReleaseNoteDto> notes =
                [
                    .. ReleaseNotesDocument.Parse(markdown)
                        .Select(e => new ReleaseNoteDto(e.DisplayVersion, e.For(query.CultureCode))),
                ];
                return Result<IReadOnlyList<ReleaseNoteDto>, Error>.Success(notes);
            },
            ErrorCodes.INFRASTRUCTURE_ERROR,
            _logger);
}
