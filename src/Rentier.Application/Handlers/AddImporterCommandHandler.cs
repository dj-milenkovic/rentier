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
                // Validate the pattern compiles; static IsMatch uses an internal cache
                System.Text.RegularExpressions.Regex.IsMatch(string.Empty, command.AttachmentRegex);
            }
            catch (ArgumentException ex)
            {
                return Result<Guid, Error>.Failure(new Error(ErrorCodes.IMPORTER_VALIDATION_INVALID_REGEX, ex.Message));
            }
        }

        // (2) Create importer and update details via HandlerHelper
        return await HandlerHelper.ExecuteAsync<Guid>(
            async () =>
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
            },
            ErrorCodes.IMPORTER_VALIDATION_INVALID_REGEX);
    }
}
