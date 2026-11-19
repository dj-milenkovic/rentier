using Microsoft.Extensions.Logging;
using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.Interfaces;
using Rentier.Application.Repositories;
using Rentier.Domain.Entities;
using Rentier.Domain.Exceptions;
using Rentier.Domain.ValueObjects;

namespace Rentier.Application.Handlers;

public sealed class AddImporterCommandHandler
    : ICommandHandler<AddImporterCommand, Result<Guid, Error>>
{
    private readonly IImporterRepository _repository;
    private readonly ILogger<AddImporterCommandHandler> _logger;

    public AddImporterCommandHandler(IImporterRepository repository, ILogger<AddImporterCommandHandler> logger)
    {
        _logger = logger;
        _repository = repository;
    }

    public async Task<Result<Guid, Error>> HandleAsync(
        AddImporterCommand command, CancellationToken ct = default)
    {
        // (1) Validate AttachmentRegex (empty = no attachment filter)
        if (AttachmentPattern.Validate(command.AttachmentRegex) is { } invalidPattern)
            return Result<Guid, Error>.Failure(invalidPattern);

        // (2) Create importer and update details via HandlerHelper
        return await HandlerHelper.ExecuteAsync<Guid>(
            async () =>
            {
                var importer = Importer.Create(command.DisplayName, command.ReportType);
                importer.UpdateDetails(new ImporterDetails(
                    command.DisplayName,
                    command.ReportType,
                    command.TaxpayerProfileId,
                    command.MailboxId,
                    command.FromFilter,
                    command.SubjectFilter,
                    command.AttachmentRegex,
                    command.PaymentNotes));
                await _repository.AddAsync(importer, ct);
                return Result<Guid, Error>.Success(importer.Id);
            },
            ErrorCodes.INFRASTRUCTURE_ERROR,
            _logger);
    }
}
