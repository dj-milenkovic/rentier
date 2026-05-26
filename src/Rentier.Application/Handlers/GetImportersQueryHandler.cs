using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Application.Interfaces;
using Rentier.Application.Queries;
using Rentier.Application.Repositories;

namespace Rentier.Application.Handlers;

public sealed class GetImportersQueryHandler
    : IQueryHandler<GetImportersQuery, Result<IReadOnlyList<ImporterDto>, Error>>
{
    private readonly IImporterRepository _repository;

    public GetImportersQueryHandler(IImporterRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<IReadOnlyList<ImporterDto>, Error>> HandleAsync(
        GetImportersQuery query, CancellationToken ct = default)
    {
        var importers = await _repository.GetAllAsync(ct);
        var list = importers.Select(i => new ImporterDto(
            i.Id,
            i.DisplayName,
            i.ReportType,
            i.TaxpayerProfileId,
            i.MailboxId,
            i.FromFilter,
            i.SubjectFilter,
            i.AttachmentRegex,
            i.PaymentNotes)).ToList();
        return Result<IReadOnlyList<ImporterDto>, Error>.Success(list.AsReadOnly());
    }
}
