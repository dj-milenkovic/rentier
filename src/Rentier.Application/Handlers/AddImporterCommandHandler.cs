using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.Interfaces;
using Rentier.Application.Repositories;
using Rentier.Domain.Entities;
using Rentier.Domain.Exceptions;

namespace Rentier.Application.Handlers;

public sealed class AddImporterCommandHandler
    : ICommandHandler<AddImporterCommand, Result<Guid, Error>>
{
    private readonly IImporterRepository _repository;

    public AddImporterCommandHandler(IImporterRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<Guid, Error>> HandleAsync(
        AddImporterCommand command, CancellationToken ct = default)
    {
        // (1) Validate AttachmentRegex if non-empty
        if (!string.IsNullOrEmpty(command.AttachmentRegex))
        {
            try
            {
                _ = new System.Text.RegularExpressions.Regex(command.AttachmentRegex);
            }
            catch (ArgumentException ex)
            {
                return Result<Guid, Error>.Failure(new Error("INVALID_REGEX", ex.Message));
            }
        }

        // (2) Create importer and update details
        try
        {
            var importer = Importer.Create(command.DisplayName, command.ReportType);
            importer.UpdateDetails(
                command.DisplayName,
                command.ReportType,
                command.TaxpayerProfileId,
                command.MailboxId,
                command.FromFilter,
                command.SubjectFilter,
                command.AttachmentRegex,
                command.PaymentNotes);
            await _repository.AddAsync(importer, ct);
            return Result<Guid, Error>.Success(importer.Id);
        }
        catch (DomainException ex)
        {
            return Result<Guid, Error>.Failure(new Error("DOMAIN_ERROR", ex.Message));
        }
    }
}
