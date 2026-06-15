using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.Interfaces;
using Rentier.Application.Repositories;
using Rentier.Domain.Exceptions;

namespace Rentier.Application.Handlers;

public sealed class UpdateImporterCommandHandler
    : ICommandHandler<UpdateImporterCommand, Result<VoidResult, Error>>
{
    private readonly IImporterRepository _repository;

    public UpdateImporterCommandHandler(IImporterRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<VoidResult, Error>> HandleAsync(
        UpdateImporterCommand command, CancellationToken ct = default)
    {
        // (1) Find existing importer
        var importer = await _repository.GetByIdAsync(command.Id, ct);
        if (importer is null)
            return Result<VoidResult, Error>.Failure(
                new Error(ErrorCodes.IMPORTER_NOT_FOUND, $"Importer {command.Id} not found."));

        // (2) Validate AttachmentRegex if non-empty
        if (!string.IsNullOrEmpty(command.AttachmentRegex))
        {
            try
            {
                // Validate the pattern compiles; static IsMatch uses an internal cache
                System.Text.RegularExpressions.Regex.IsMatch(string.Empty, command.AttachmentRegex);
            }
            catch (ArgumentException ex)
            {
                return Result<VoidResult, Error>.Failure(new Error(ErrorCodes.IMPORTER_VALIDATION_INVALID_REGEX, ex.Message));
            }
        }

        // (3) Update details via HandlerHelper
        return await HandlerHelper.ExecuteAsync<VoidResult>(
            async () =>
            {
                importer.UpdateDetails(
                    command.DisplayName,
                    command.ReportType,
                    command.TaxpayerProfileId,
                    command.MailboxId,
                    command.FromFilter,
                    command.SubjectFilter,
                    command.AttachmentRegex,
                    command.PaymentNotes);

                // (4) Persist
                await _repository.UpdateAsync(importer, ct);
                return Result<VoidResult, Error>.Success(VoidResult.Value);
            },
            ErrorCodes.INFRASTRUCTURE_ERROR);
    }
}
