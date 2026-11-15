using System.Diagnostics.CodeAnalysis;
using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Application.Interfaces;
using Rentier.Application.Queries;
using Rentier.Desktop.Dialogs;

namespace Rentier.Desktop.Services;

/// <summary>Loads the configured importers and shows the file picker + importer selection dialogs.</summary>
[ExcludeFromCodeCoverage]
internal sealed class ReportImportDialogService : IReportImportDialogService
{
    private readonly IQueryHandler<GetImportersQuery, Result<IReadOnlyList<ImporterDto>, Error>> _getImporters;

    public ReportImportDialogService(
        IQueryHandler<GetImportersQuery, Result<IReadOnlyList<ImporterDto>, Error>> getImporters)
    {
        _getImporters = getImporters;
    }

    public async Task<ReportImportSelection?> PickReportAsync()
    {
        var importersResult = await _getImporters.HandleAsync(new GetImportersQuery());
        IReadOnlyList<ImporterDto> importers = importersResult.IsSuccess
            ? importersResult.Value
            : [];

        var picked = await ImportDialogHelper.ShowAsync(importers);
        return picked is null
            ? null
            : new ReportImportSelection(picked.Value.ImporterId, picked.Value.FileName, picked.Value.Content);
    }
}
