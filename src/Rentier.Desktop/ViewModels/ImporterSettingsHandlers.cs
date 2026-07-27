using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Application.Interfaces;
using Rentier.Application.Queries;

namespace Rentier.Desktop.ViewModels;

/// <summary>
/// Bundles the Application-layer handlers <see cref="ImporterSettingsViewModel"/> depends on.
/// Keeps the ViewModel constructor within the 7-parameter limit (Sonar S107) without
/// hiding any dependency: every member is resolved from DI exactly as before.
/// </summary>
public sealed record ImporterSettingsHandlers(
    IQueryHandler<GetImportersQuery, Result<IReadOnlyList<ImporterDto>, Error>> GetImporters,
    IQueryHandler<GetTaxpayerProfileQuery, Result<TaxpayerProfileDto?, Error>> GetProfile,
    IQueryHandler<GetMailboxesQuery, Result<IReadOnlyList<MailboxDto>, Error>> GetMailboxes,
    ICommandHandler<AddImporterCommand, Result<Guid, Error>> AddImporter,
    ICommandHandler<UpdateImporterCommand, Result<VoidResult, Error>> UpdateImporter,
    ICommandHandler<DeleteImporterCommand, Result<VoidResult, Error>> DeleteImporter);
