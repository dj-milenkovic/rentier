using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
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
    private readonly IServiceScopeFactory _scopeFactory;

    public ReportImportDialogService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task<ReportImportSelection?> PickReportAsync()
    {
        // ReportsViewModel holds this service for the whole session. Resolving the handler per call
        // (in its own scope) gives every import a fresh, short-lived DbContext that is disposed
        // here instead of one context captured for the app's lifetime.
        IReadOnlyList<ImporterDto> importers;
        using (var scope = _scopeFactory.CreateScope())
        {
            var getImporters = scope.ServiceProvider.GetRequiredService<
                IQueryHandler<GetImportersQuery, Result<IReadOnlyList<ImporterDto>, Error>>>();
            var importersResult = await getImporters.HandleAsync(new GetImportersQuery());
            importers = importersResult.IsSuccess ? importersResult.Value : [];
        }

        var picked = await ImportDialogHelper.ShowAsync(importers);
        return picked is null
            ? null
            : new ReportImportSelection(picked.Value.ImporterId, picked.Value.FileName, picked.Value.Content);
    }
}
