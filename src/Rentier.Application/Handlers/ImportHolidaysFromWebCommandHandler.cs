using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Application.Interfaces;

namespace Rentier.Application.Handlers;

public sealed class ImportHolidaysFromWebCommandHandler
    : ICommandHandler<ImportHolidaysFromWebCommand, Result<IReadOnlyList<HolidayEntryDto>, Error>>
{
    private readonly IHolidayImporter _importer;

    public ImportHolidaysFromWebCommandHandler(IHolidayImporter importer)
    {
        _importer = importer;
    }

    public async Task<Result<IReadOnlyList<HolidayEntryDto>, Error>> HandleAsync(
        ImportHolidaysFromWebCommand cmd, CancellationToken ct = default)
    {
        return await _importer.ImportAsync(cmd.Year, ct);
    }
}
